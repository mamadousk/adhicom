using Adhicom.Application.Administration;
using Adhicom.Application.Common;
using Adhicom.Domain.Common;
using Adhicom.Domain.Stores;
using Adhicom.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Web.Areas.Admin.Controllers;

public class StoresController : AdminControllerBase
{
    private readonly Adhicom.Infrastructure.Persistence.AdhicomDbContext _db;
    private readonly IAuditService _audit;

    public StoresController(Adhicom.Infrastructure.Persistence.AdhicomDbContext db, IAuditService audit, ITenantContext tenant, ICurrentUser currentUser)
        : base(tenant, currentUser)
        => (_db, _audit) = (db, audit);

    public async Task<IActionResult> Index(string? status, string? search)
    {
        var query = _db.Stores.AsNoTracking().AsQueryable();
        if (Enum.TryParse<StoreStatus>(status, true, out var parsed)) query = query.Where(s => s.Status == parsed);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.Name.Contains(search) || s.Slug.Contains(search) || (s.BusinessEmail != null && s.BusinessEmail.Contains(search)));

        var stores = await query.OrderByDescending(s => s.CreatedAtUtc).Take(300).ToListAsync();

        var ownerIds = stores.Select(s => s.OwnerId).Distinct().ToList();
        var ownerIdStrings = ownerIds.Select(o => o.ToString()).ToList();
        var ownerUsers = await _db.Users.AsNoTracking().Where(u => ownerIdStrings.Contains(u.Id)).ToListAsync();
        var ownerEmails = ownerUsers.ToDictionary(u => Guid.Parse(u.Id), u => u.Email ?? "");

        var storeIds = stores.Select(s => s.Id).ToList();
        var orderCounts = await _db.Orders.AsNoTracking().Where(o => storeIds.Contains(o.StoreId))
            .GroupBy(o => o.StoreId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);

        var vm = new AdminStoresViewModel
        {
            StatusFilter = status,
            Search = search,
            Stores = stores.Select(s => new AdminStoreRowViewModel
            {
                Id = s.Id,
                Name = s.Name,
                Slug = s.Slug,
                Status = s.Status,
                OwnerEmail = ownerEmails.TryGetValue(s.OwnerId, out var email) ? email : "—",
                CreatedAtUtc = s.CreatedAtUtc,
                OrderCount = orderCounts.TryGetValue(s.Id, out var c) ? c : 0
            }).ToList()
        };
        return View(vm);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var store = await _db.Stores.AsNoTracking().Include(s => s.Domains).FirstOrDefaultAsync(s => s.Id == id);
        if (store is null) return NotFound();
        var owner = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == store.OwnerId.ToString());
        ViewData["OwnerEmail"] = owner?.Email;
        ViewData["ProductCount"] = await _db.Products.CountAsync(p => p.StoreId == id);
        ViewData["OrderCount"] = await _db.Orders.CountAsync(o => o.StoreId == id);
        ViewData["CustomerCount"] = await _db.Customers.CountAsync(c => c.StoreId == id);
        return View(store);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(Guid id, string? reason)
    {
        var store = await _db.Stores.FirstOrDefaultAsync(s => s.Id == id);
        if (store is not null && store.Status != StoreStatus.Suspended)
        {
            store.Status = StoreStatus.Suspended;
            store.SuspendedReason = string.IsNullOrWhiteSpace(reason) ? "Suspension par l'administration." : reason.Trim();
            store.SuspendedAtUtc = DateTime.UtcNow;
            store.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("store.suspended", "Store", store.Id.ToString(), store.SuspendedReason, store.Id);
        }
        TempData["Toast"] = "Boutique suspendue. Elle n'est plus accessible au public.";
        return Redirect($"/admin/stores/details/{id}");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid id)
    {
        var store = await _db.Stores.FirstOrDefaultAsync(s => s.Id == id);
        if (store is not null)
        {
            store.Status = StoreStatus.Active;
            store.SuspendedReason = null;
            store.SuspendedAtUtc = null;
            store.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("store.reactivated", "Store", store.Id.ToString(), null, store.Id);
        }
        TempData["Toast"] = "Boutique réactivée.";
        return Redirect($"/admin/stores/details/{id}");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(Guid id, string? reason)
    {
        var store = await _db.Stores.FirstOrDefaultAsync(s => s.Id == id);
        if (store is not null)
        {
            store.Status = StoreStatus.Closed;
            store.SuspendedReason = string.IsNullOrWhiteSpace(reason) ? "Fermeture par l'administration." : reason.Trim();
            store.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("store.closed", "Store", store.Id.ToString(), store.SuspendedReason, store.Id);
        }
        TempData["Toast"] = "Boutique fermée définitivement. Les données sont conservées (non supprimées).";
        return Redirect("/admin/stores");
    }
}
