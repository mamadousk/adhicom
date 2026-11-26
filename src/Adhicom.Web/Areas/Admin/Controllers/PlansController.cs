using Adhicom.Application.Administration;
using Adhicom.Application.Common;
using Adhicom.Domain.Subscriptions;
using Adhicom.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Web.Areas.Admin.Controllers;

public class PlansController : AdminControllerBase
{
    private readonly Adhicom.Infrastructure.Persistence.AdhicomDbContext _db;
    private readonly IAuditService _audit;

    public PlansController(Adhicom.Infrastructure.Persistence.AdhicomDbContext db, IAuditService audit, ITenantContext tenant, ICurrentUser currentUser)
        : base(tenant, currentUser)
        => (_db, _audit) = (db, audit);

    public async Task<IActionResult> Index()
        => View(await _db.Plans.AsNoTracking().Include(p => p.Features).OrderBy(p => p.SortOrder).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View("Form", new PlanFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PlanFormViewModel model)
    {
        if (!ModelState.IsValid) return View("Form", model);
        if (await _db.Plans.AnyAsync(p => p.Key == model.Key.Trim().ToLowerInvariant()))
        {
            ModelState.AddModelError(nameof(model.Key), "Cette clé de plan existe déjà.");
            return View("Form", model);
        }

        var plan = Map(model);
        plan.Features = new List<PlanFeature>();
        _db.Plans.Add(plan);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("plan.created", "Plan", plan.Id.ToString(), plan.Key);
        TempData["Toast"] = "Plan créé.";
        return Redirect("/admin/plans");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var plan = await _db.Plans.Include(p => p.Features).FirstOrDefaultAsync(p => p.Id == id);
        if (plan is null) return NotFound();
        return View("Form", Map(plan));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, PlanFormViewModel model)
    {
        if (!ModelState.IsValid) return View("Form", model);
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == id);
        if (plan is null) return NotFound();

        plan.Key = model.Key.Trim().ToLowerInvariant();
        plan.Name = model.Name.Trim();
        plan.Description = model.Description?.Trim();
        plan.MonthlyPrice = model.MonthlyPrice;
        plan.Currency = string.IsNullOrWhiteSpace(model.Currency) ? "USD" : model.Currency.Trim().ToUpperInvariant();
        plan.IsActive = model.IsActive;
        plan.SortOrder = model.SortOrder;
        plan.MaxStores = model.MaxStores;
        plan.MaxProducts = model.MaxProducts;
        plan.MaxUsers = model.MaxUsers;
        plan.MaxStorageMb = model.MaxStorageMb;
        plan.AllowPremiumThemes = model.AllowPremiumThemes;
        plan.AllowAdvancedAnalytics = model.AllowAdvancedAnalytics;
        plan.AllowCustomDomain = model.AllowCustomDomain;
        plan.AllowMarketing = model.AllowMarketing;
        plan.AllowCoupons = model.AllowCoupons;
        plan.AllowExports = model.AllowExports;
        plan.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("plan.updated", "Plan", plan.Id.ToString(), plan.Key);
        TempData["Toast"] = "Plan mis à jour. Les limites s'appliquent immédiatement (contrôlées côté serveur).";
        return Redirect("/admin/plans");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid id)
    {
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == id);
        if (plan is not null)
        {
            plan.IsActive = !plan.IsActive;
            plan.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _audit.LogAsync(plan.IsActive ? "plan.enabled" : "plan.disabled", "Plan", plan.Id.ToString(), plan.Key);
        }
        return Redirect("/admin/plans");
    }

    private static Plan Map(PlanFormViewModel model) => new()
    {
        Key = model.Key.Trim().ToLowerInvariant(),
        Name = model.Name.Trim(),
        Description = model.Description?.Trim(),
        MonthlyPrice = model.MonthlyPrice,
        Currency = string.IsNullOrWhiteSpace(model.Currency) ? "USD" : model.Currency.Trim().ToUpperInvariant(),
        IsActive = model.IsActive,
        SortOrder = model.SortOrder,
        MaxStores = model.MaxStores,
        MaxProducts = model.MaxProducts,
        MaxUsers = model.MaxUsers,
        MaxStorageMb = model.MaxStorageMb,
        AllowPremiumThemes = model.AllowPremiumThemes,
        AllowAdvancedAnalytics = model.AllowAdvancedAnalytics,
        AllowCustomDomain = model.AllowCustomDomain,
        AllowMarketing = model.AllowMarketing,
        AllowCoupons = model.AllowCoupons,
        AllowExports = model.AllowExports
    };

    private static PlanFormViewModel Map(Plan plan) => new()
    {
        Id = plan.Id,
        Key = plan.Key,
        Name = plan.Name,
        Description = plan.Description,
        MonthlyPrice = plan.MonthlyPrice,
        Currency = plan.Currency,
        IsActive = plan.IsActive,
        SortOrder = plan.SortOrder,
        MaxStores = plan.MaxStores,
        MaxProducts = plan.MaxProducts,
        MaxUsers = plan.MaxUsers,
        MaxStorageMb = plan.MaxStorageMb,
        AllowPremiumThemes = plan.AllowPremiumThemes,
        AllowAdvancedAnalytics = plan.AllowAdvancedAnalytics,
        AllowCustomDomain = plan.AllowCustomDomain,
        AllowMarketing = plan.AllowMarketing,
        AllowCoupons = plan.AllowCoupons,
        AllowExports = plan.AllowExports
    };
}
