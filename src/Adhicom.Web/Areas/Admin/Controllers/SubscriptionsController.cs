using Adhicom.Application.Administration;
using Adhicom.Application.Common;
using Adhicom.Domain.Subscriptions;
using Adhicom.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Web.Areas.Admin.Controllers;

public class SubscriptionsController : AdminControllerBase
{
    private readonly Adhicom.Infrastructure.Persistence.AdhicomDbContext _db;
    private readonly IAuditService _audit;

    public SubscriptionsController(Adhicom.Infrastructure.Persistence.AdhicomDbContext db, IAuditService audit, ITenantContext tenant, ICurrentUser currentUser)
        : base(tenant, currentUser)
        => (_db, _audit) = (db, audit);

    public async Task<IActionResult> Index(string? status)
    {
        var query = _db.Subscriptions.AsNoTracking().Include(s => s.Plan).AsQueryable();
        if (Enum.TryParse<SubscriptionStatus>(status, true, out var parsed)) query = query.Where(s => s.Status == parsed);

        var subscriptions = await query.OrderByDescending(s => s.EndsAtUtc).Take(300).ToListAsync();
        var userIds = subscriptions.Select(s => s.SubscriberUserId.ToString()).Distinct().ToList();
        var emails = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? "");

        var vm = new AdminSubscriptionsViewModel
        {
            StatusFilter = status,
            Plans = await _db.Plans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync(),
            Subscriptions = subscriptions.Select(s => new AdminSubscriptionRowViewModel
            {
                Id = s.Id,
                SubscriberEmail = emails.TryGetValue(s.SubscriberUserId.ToString(), out var e) ? e : "—",
                PlanName = s.Plan?.Name ?? "—",
                Status = s.Status,
                StartsAtUtc = s.StartsAtUtc,
                EndsAtUtc = s.EndsAtUtc,
                MonthlyPrice = s.Plan?.MonthlyPrice ?? 0,
                Currency = s.Plan?.Currency ?? "USD"
            }).ToList()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Extend(Guid id, int days = 30)
    {
        days = Math.Clamp(days, 1, 730);
        var subscription = await _db.Subscriptions.FirstOrDefaultAsync(s => s.Id == id);
        if (subscription is not null)
        {
            var baseDate = subscription.EndsAtUtc > DateTime.UtcNow ? subscription.EndsAtUtc : DateTime.UtcNow;
            subscription.EndsAtUtc = baseDate.AddDays(days);
            if (subscription.Status is SubscriptionStatus.PastDue or SubscriptionStatus.Grace or SubscriptionStatus.Expired)
                subscription.Status = SubscriptionStatus.Active;
            subscription.UpdatedAtUtc = DateTime.UtcNow;
            _db.SubscriptionHistories.Add(new SubscriptionHistory { SubscriptionId = subscription.Id, Event = "admin_extended", Detail = $"Prolongé de {days} jours par l'administration.", ChangedByUserId = CurrentUserCtx.UserId });
            await _db.SaveChangesAsync();
            await _audit.LogAsync("subscription.extended", "Subscription", subscription.Id.ToString(), $"+{days} jours");
        }
        TempData["Toast"] = "Abonnement prolongé.";
        return Redirect("/admin/subscriptions");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePlan(Guid id, Guid planId)
    {
        var subscription = await _db.Subscriptions.FirstOrDefaultAsync(s => s.Id == id);
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == planId);
        if (subscription is not null && plan is not null)
        {
            var previous = subscription.PlanId;
            subscription.PlanId = plan.Id;
            subscription.Status = SubscriptionStatus.Active;
            subscription.UpdatedAtUtc = DateTime.UtcNow;
            _db.SubscriptionHistories.Add(new SubscriptionHistory { SubscriptionId = subscription.Id, Event = "plan_changed", Detail = $"Plan modifié par l'administration ({previous} -> {plan.Id}).", ChangedByUserId = CurrentUserCtx.UserId });
            await _db.SaveChangesAsync();
            await _audit.LogAsync("subscription.plan_changed", "Subscription", subscription.Id.ToString(), plan.Key);
        }
        TempData["Toast"] = "Plan mis à jour.";
        return Redirect("/admin/subscriptions");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, string? reason)
    {
        var subscription = await _db.Subscriptions.FirstOrDefaultAsync(s => s.Id == id);
        if (subscription is not null)
        {
            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.CancelledAtUtc = DateTime.UtcNow;
            subscription.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Annulé par l'administration." : reason.Trim();
            subscription.UpdatedAtUtc = DateTime.UtcNow;
            _db.SubscriptionHistories.Add(new SubscriptionHistory { SubscriptionId = subscription.Id, Event = "cancelled", Detail = subscription.CancellationReason, ChangedByUserId = CurrentUserCtx.UserId });
            await _db.SaveChangesAsync();
            // Aucune donnée supprimée : la boutique reste consultable, fonctionnalités limitées selon la politique.
            await _audit.LogAsync("subscription.cancelled", "Subscription", subscription.Id.ToString(), subscription.CancellationReason);
        }
        TempData["Toast"] = "Abonnement annulé. Les données du commerçant sont conservées.";
        return Redirect("/admin/subscriptions");
    }
}
