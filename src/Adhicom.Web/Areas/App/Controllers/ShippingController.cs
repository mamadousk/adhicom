using Adhicom.Application.Common;
using Adhicom.Application.Notifications;
using Adhicom.Application.Stores;
using Adhicom.Application.Subscriptions;
using Adhicom.Domain.Shipping;
using Adhicom.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Web.Areas.App.Controllers;

public class ShippingController : MerchantControllerBase
{
    private readonly Adhicom.Infrastructure.Persistence.AdhicomDbContext _db;

    public ShippingController(Adhicom.Infrastructure.Persistence.AdhicomDbContext db,
        IStoreService storeService, ITenantContext tenant, ICurrentUser currentUser, INotificationService notifications, ISubscriptionLimitService limits)
        : base(storeService, tenant, currentUser, notifications, limits)
        => _db = db;

    public async Task<IActionResult> Index()
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        ViewData["Methods"] = await _db.ShippingMethods.Where(m => m.StoreId == CurrentStore.Id).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name).ToListAsync();
        return View(new ShippingMethodFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ShippingMethodFormViewModel model)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        if (!ModelState.IsValid) { TempData["Toast"] = "Vérifiez le formulaire."; TempData["ToastType"] = "error"; return Redirect("/app/settings/shipping"); }

        _db.ShippingMethods.Add(new ShippingMethod
        {
            StoreId = CurrentStore.Id,
            Name = model.Name.Trim(),
            Type = model.Type,
            Price = model.Type == Domain.Common.ShippingMethodType.Flat ? model.Price : 0m,
            FreeOverAmount = model.FreeOverAmount,
            EstimatedDelivery = model.EstimatedDelivery?.Trim()
        });
        await _db.SaveChangesAsync();
        TempData["Toast"] = "Mode de livraison ajouté.";
        return Redirect("/app/settings/shipping");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid id)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        var method = await _db.ShippingMethods.FirstOrDefaultAsync(m => m.Id == id && m.StoreId == CurrentStore.Id);
        if (method is not null)
        {
            method.IsActive = !method.IsActive;
            method.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return Redirect("/app/settings/shipping");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        var method = await _db.ShippingMethods.FirstOrDefaultAsync(m => m.Id == id && m.StoreId == CurrentStore.Id);
        if (method is not null)
        {
            _db.ShippingMethods.Remove(method);
            await _db.SaveChangesAsync();
            TempData["Toast"] = "Mode de livraison supprimé.";
        }
        return Redirect("/app/settings/shipping");
    }
}
