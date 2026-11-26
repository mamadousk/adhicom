using System.Text.Json;
using Adhicom.Application.Common;
using Adhicom.Application.Notifications;
using Adhicom.Application.Stores;
using Adhicom.Application.Subscriptions;
using Adhicom.Domain.Common;
using Adhicom.Domain.Stores;
using Adhicom.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Adhicom.Web.Areas.App.Controllers;

public class StoresController : MerchantControllerBase
{
    public StoresController(IStoreService storeService, ITenantContext tenant, ICurrentUser currentUser, INotificationService notifications, ISubscriptionLimitService limits)
        : base(storeService, tenant, currentUser, notifications, limits) { }

    public async Task<IActionResult> Index()
    {
        var stores = await StoreService.ListMyStoresAsync(CurrentUserId);
        var plan = await Limits.GetEffectivePlanAsync(CurrentUserId);
        return View(new StoresIndexViewModel { Stores = stores.ToList(), ActiveStoreId = CurrentStore?.Id, MaxStores = plan.MaxStores });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var check = await Limits.CanCreateStoreAsync(CurrentUserId);
        if (!check.Allowed) TempData["Toast"] = check.Reason;
        ViewData["SlugAvailableProbe"] = true;
        return View(new CreateStoreViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateStoreViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var request = new CreateStoreRequest(CurrentUserId, model.Name, model.RequestedSlug, model.Description, model.BusinessCategory,
            model.Country, model.City, model.Address, model.WhatsappNumber, model.BusinessEmail);

        var result = await StoreService.CreateStoreAsync(request);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        Response.Cookies.Append("adhicom_active_store", result.Value!.Id.ToString(), new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Expires = DateTimeOffset.UtcNow.AddYears(1) });
        TempData["Toast"] = $"Boutique « {result.Value.Name} » créée. Bienvenue !";
        return Redirect("/app/onboarding");
    }

    [HttpGet("/app/stores/check-slug")]
    public async Task<IActionResult> CheckSlug(string slug)
    {
        var available = await StoreService.IsSlugAvailableAsync(slug ?? string.Empty);
        return Json(new { available });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Select(Guid id)
    {
        // Vérifier l'accès : seule une boutique du commerçant peut être activée
        var myStores = await StoreService.ListMyStoresAsync(CurrentUserId);
        if (myStores.Any(s => s.Id == id))
        {
            Response.Cookies.Append("adhicom_active_store", id.ToString(), new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Expires = DateTimeOffset.UtcNow.AddYears(1) });
            TempData["Toast"] = "Boutique active mise à jour.";
        }
        return Redirect("/app");
    }
}
