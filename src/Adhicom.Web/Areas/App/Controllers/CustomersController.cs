using System.Text;
using Adhicom.Application.Common;
using Adhicom.Application.Notifications;
using Adhicom.Application.Stores;
using Adhicom.Application.Subscriptions;
using Adhicom.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Web.Areas.App.Controllers;

public class CustomersController : MerchantControllerBase
{
    private const int PageSize = 25;

    private readonly Adhicom.Infrastructure.Persistence.AdhicomDbContext _db;

    public CustomersController(Adhicom.Infrastructure.Persistence.AdhicomDbContext db,
        IStoreService storeService, ITenantContext tenant, ICurrentUser currentUser, INotificationService notifications, ISubscriptionLimitService limits)
        : base(storeService, tenant, currentUser, notifications, limits)
        => _db = db;

    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        var storeId = CurrentStore.Id;
        page = Math.Max(1, page);

        var query = _db.Customers.Where(c => c.StoreId == storeId).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => c.Name.Contains(q) || c.Phone.Contains(q) || (c.Email != null && c.Email.Contains(q)));

        var total = await query.CountAsync();
        var customers = await query.OrderByDescending(c => c.LastOrderAtUtc ?? c.CreatedAtUtc).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();

        ViewData["Query"] = q;
        ViewData["Page"] = page;
        ViewData["TotalPages"] = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        ViewData["Total"] = total;
        return View(customers);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id && c.StoreId == CurrentStore.Id);
        if (customer is null) return NotFound();
        var orders = await _db.Orders.Where(o => o.CustomerId == customer.Id && o.StoreId == CurrentStore.Id)
            .OrderByDescending(o => o.CreatedAtUtc).Take(50).ToListAsync();
        ViewData["Orders"] = orders;
        return View(customer);
    }

    [HttpGet]
    public async Task<IActionResult> Export()
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");

        var canExport = await Limits.CanExportCustomersAsync(CurrentUserId);
        if (!canExport)
        {
            TempData["Toast"] = "L'export des clients est réservé aux plans Starter et supérieurs.";
            TempData["ToastType"] = "error";
            return Redirect("/app/customers");
        }

        var customers = await _db.Customers.Where(c => c.StoreId == CurrentStore.Id).OrderBy(c => c.Name).ToListAsync();
        var sb = new StringBuilder("Nom;Téléphone;Email;Ville;Adresse;Commandes;Total acheté;Dernière commande\n");
        foreach (var c in customers)
        {
            sb.Append(CsvEscape(c.Name)).Append(';')
              .Append(CsvEscape(c.Phone)).Append(';')
              .Append(CsvEscape(c.Email)).Append(';')
              .Append(CsvEscape(c.City)).Append(';')
              .Append(CsvEscape(c.Address)).Append(';')
              .Append(c.TotalOrders).Append(';')
              .Append(c.TotalSpent.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)).Append(';')
              .Append(c.LastOrderAtUtc?.ToString("yyyy-MM-dd")).Append('\n');
        }
        // BOM UTF-8 pour Excel (FR)
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(),
            "text/csv", $"clients-{CurrentStore.Slug}.csv");
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var v = value.Replace("\"", "\"\"");
        return v.Contains(';') || v.Contains('"') || v.Contains('\n') ? $"\"{v}\"" : v;
    }
}
