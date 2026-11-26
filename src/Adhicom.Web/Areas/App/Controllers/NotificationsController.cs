using Adhicom.Application.Common;
using Adhicom.Application.Notifications;
using Adhicom.Application.Stores;
using Adhicom.Application.Subscriptions;
using Microsoft.AspNetCore.Mvc;

namespace Adhicom.Web.Areas.App.Controllers;

public class NotificationsController : MerchantControllerBase
{
    public NotificationsController(IStoreService storeService, ITenantContext tenant, ICurrentUser currentUser, INotificationService notifications, ISubscriptionLimitService limits)
        : base(storeService, tenant, currentUser, notifications, limits) { }

    public async Task<IActionResult> Index()
        => View(await Notifications.ListAsync(CurrentUserId, 50));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Open(Guid id)
    {
        var list = await Notifications.ListAsync(CurrentUserId, 100);
        var notification = list.FirstOrDefault(n => n.Id == id);
        if (notification is not null)
        {
            await Notifications.MarkReadAsync(CurrentUserId, id);
            if (!string.IsNullOrWhiteSpace(notification.Link) && notification.Link.StartsWith('/'))
                return Redirect(notification.Link);
        }
        return Redirect("/app/notifications");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        await Notifications.MarkAllReadAsync(CurrentUserId);
        TempData["Toast"] = "Toutes les notifications sont marquées comme lues.";
        return Redirect("/app/notifications");
    }
}
