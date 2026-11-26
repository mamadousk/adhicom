using Adhicom.Application.Administration;
using Adhicom.Application.Common;
using Adhicom.Application.Notifications;
using Adhicom.Application.Stores;
using Adhicom.Application.Subscriptions;
using Adhicom.Domain.Subscriptions;
using Adhicom.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Web.Areas.App.Controllers;

public class SubscriptionController : MerchantControllerBase
{
    private readonly Adhicom.Infrastructure.Persistence.AdhicomDbContext _db;
    private readonly ISupportService _support;

    public SubscriptionController(Adhicom.Infrastructure.Persistence.AdhicomDbContext db, ISupportService support,
        IStoreService storeService, ITenantContext tenant, ICurrentUser currentUser, INotificationService notifications, ISubscriptionLimitService limits)
        : base(storeService, tenant, currentUser, notifications, limits)
        => (_db, _support) = (db, support);

    public async Task<IActionResult> Index()
    {
        var plan = await Limits.GetEffectivePlanAsync(CurrentUserId);
        using (Tenant.BypassScope())
        {
            var subscription = await _db.Subscriptions.AsNoTracking().Include(s => s.Plan)
                .Where(s => s.SubscriberUserId == CurrentUserId)
                .OrderByDescending(s => s.EndsAtUtc)
                .FirstOrDefaultAsync();
            var availablePlans = await _db.Plans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync();
            var productsUsed = CurrentStore is null ? 0 : await Limits.CountProductsAsync(CurrentStore.Id);
            var storesUsed = await Limits.CountStoresAsync(CurrentUserId);
            var daysRemaining = subscription is null ? 0 : Math.Max(0, (int)(subscription.EndsAtUtc - DateTime.UtcNow).TotalDays);

            return View(new SubscriptionViewModel
            {
                Subscription = subscription,
                Plan = plan,
                ProductsUsed = productsUsed,
                ProductsLimit = plan.MaxProducts,
                StoresUsed = storesUsed,
                StoresLimit = plan.MaxStores,
                AdvancedAnalytics = plan.AllowAdvancedAnalytics,
                DaysRemaining = daysRemaining,
                AvailablePlans = availablePlans
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestUpgrade(string planKey)
    {
        var email = CurrentUserCtx.Email ?? "";
        await _support.CreateTicketAsync(CurrentUserId, CurrentStore?.Id,
            $"Demande de changement de plan : {planKey}",
            $"Bonjour, je souhaite passer au plan {planKey}. Merci de me contacter pour finaliser le règlement (Mobile Money / virement).",
            CurrentUserCtx.UserName, email);
        TempData["Toast"] = "Votre demande a été envoyée à l'équipe Adhicom. Nous vous contactons rapidement.";
        return Redirect("/app/subscription");
    }
}
