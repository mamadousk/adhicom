using Adhicom.Application.Common;
using Adhicom.Application.Notifications;
using Adhicom.Application.Stores;
using Adhicom.Application.Subscriptions;
using Adhicom.Domain.Common;
using Adhicom.Domain.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Adhicom.Web.Areas.App.Controllers;

/// <summary>
/// Base de l'espace commerçant. À chaque requête, détermine la boutique active
/// STRICTEMENT à partir des appartenances (StoreMembers) de l'utilisateur authentifié —
/// jamais à partir d'un paramètre d'URL ou de formulaire — et positionne le tenant EF Core.
/// </summary>
[Area("App")]
[Authorize(Roles = AppRoles.Merchant + "," + AppRoles.SuperAdmin)]
public abstract class MerchantControllerBase : Controller
{
    protected IStoreService StoreService { get; }
    protected ITenantContext Tenant { get; }
    protected ICurrentUser CurrentUserCtx { get; }
    protected INotificationService Notifications { get; }
    protected ISubscriptionLimitService Limits { get; }

    protected Store? CurrentStore { get; private set; }
    protected Guid CurrentUserId => CurrentUserCtx.UserId!.Value;

    protected MerchantControllerBase(IStoreService storeService, ITenantContext tenant, ICurrentUser currentUser, INotificationService notifications, ISubscriptionLimitService limits)
        => (StoreService, Tenant, CurrentUserCtx, Notifications, Limits) = (storeService, tenant, currentUser, notifications, limits);

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userId = CurrentUserCtx.UserId;
        if (userId is null)
        {
            context.Result = RedirectToAction("Login", "Account", new { area = "" });
            return;
        }

        var myStores = await StoreService.ListMyStoresAsync(userId.Value);

        // Boutique active : cookie positionné via /app/stores/select et RE-VÉRIFIÉ à chaque requête
        var activeId = Request.Cookies.TryGetValue("adhicom_active_store", out var raw) && Guid.TryParse(raw, out var parsed) ? parsed : (Guid?)null;
        CurrentStore = activeId is { } active && myStores.Any(s => s.Id == active)
            ? myStores.First(s => s.Id == active)
            : myStores.FirstOrDefault();

        // Le tenant est LE SEUL filtre appliqué aux requêtes de boutique
        Tenant.SetStore(CurrentStore?.Id, bypass: CurrentStore is null);

        ViewData["CurrentStore"] = CurrentStore;
        ViewData["MyStores"] = myStores;
        ViewData["UnreadNotifications"] = await Notifications.GetUnreadCountAsync(userId.Value);

        await next();
    }
}
