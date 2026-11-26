using Adhicom.Application.Subscriptions;
using Adhicom.Domain.Common;
using Adhicom.Domain.Subscriptions;
using Adhicom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Infrastructure.Services;

public sealed class SubscriptionLimitService : ISubscriptionLimitService
{
    private readonly AdhicomDbContext _db;
    private readonly Adhicom.Application.Common.ITenantContext _tenant;

    public SubscriptionLimitService(AdhicomDbContext db, Adhicom.Application.Common.ITenantContext tenant)
        => (_db, _tenant) = (db, tenant);

    public async Task<Plan> GetEffectivePlanAsync(Guid userId, CancellationToken ct = default)
    {
        using (_tenant.BypassScope())
        {
            var sub = await _db.Subscriptions.AsNoTracking()
                .Include(s => s.Plan)
                .Where(s => s.SubscriberUserId == userId && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Grace))
                .OrderByDescending(s => s.EndsAtUtc)
                .FirstOrDefaultAsync(ct);
            if (sub?.Plan is { } plan) return plan;
            var free = await _db.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Key == "free", ct);
            return free ?? new Plan { Key = "free", Name = "Gratuit", MonthlyPrice = 0 };
        }
    }

    public async Task<int> CountStoresAsync(Guid userId, CancellationToken ct = default)
    {
        using (_tenant.BypassScope())
            return await _db.Stores.AsNoTracking().CountAsync(s => s.OwnerId == userId && s.Status != StoreStatus.Closed, ct);
    }

    public async Task<SubscriptionLimitCheck> CanCreateStoreAsync(Guid userId, CancellationToken ct = default)
    {
        var plan = await GetEffectivePlanAsync(userId, ct);
        var count = await CountStoresAsync(userId, ct);
        return count < plan.MaxStores
            ? new SubscriptionLimitCheck(true, null, plan.MaxStores)
            : new SubscriptionLimitCheck(false, $"Votre plan « {plan.Name} » autorise {plan.MaxStores} boutique(s). Passez à une formule supérieure pour en créer davantage.", plan.MaxStores);
    }

    public async Task<int> CountProductsAsync(Guid storeId, CancellationToken ct = default)
    {
        using (_tenant.BypassScope())
            return await _db.Products.AsNoTracking().CountAsync(p => p.StoreId == storeId && p.Status != ProductStatus.Archived, ct);
    }

    public async Task<SubscriptionLimitCheck> CanCreateProductAsync(Guid storeId, CancellationToken ct = default)
    {
        Guid? ownerId = null;
        using (_tenant.BypassScope())
            ownerId = await _db.Stores.AsNoTracking().Where(s => s.Id == storeId).Select(s => (Guid?)s.OwnerId).FirstOrDefaultAsync(ct);
        if (ownerId is null) return new SubscriptionLimitCheck(false, "Boutique introuvable.", null);

        var plan = await GetEffectivePlanAsync(ownerId.Value, ct);
        var count = await CountProductsAsync(storeId, ct);
        return count < plan.MaxProducts
            ? new SubscriptionLimitCheck(true, null, plan.MaxProducts)
            : new SubscriptionLimitCheck(false, $"Votre plan « {plan.Name} » autorise {plan.MaxProducts} produit(s). Passez à une formule supérieure pour en ajouter davantage.", plan.MaxProducts);
    }

    public async Task<bool> CanUsePremiumThemesAsync(Guid userId, CancellationToken ct = default) => (await GetEffectivePlanAsync(userId, ct)).AllowPremiumThemes;
    public async Task<bool> CanExportCustomersAsync(Guid userId, CancellationToken ct = default) => (await GetEffectivePlanAsync(userId, ct)).AllowExports;
    public async Task<bool> CanAccessAdvancedAnalyticsAsync(Guid userId, CancellationToken ct = default) => (await GetEffectivePlanAsync(userId, ct)).AllowAdvancedAnalytics;
}

public sealed class SubscriptionMaintenanceService : ISubscriptionMaintenanceService
{
    private readonly AdhicomDbContext _db;
    private readonly Adhicom.Application.Common.ITenantContext _tenant;
    private readonly Adhicom.Application.Common.IClock _clock;
    private readonly Adhicom.Application.Notifications.INotificationService _notifications;
    private readonly Adhicom.Application.Administration.ISystemSettingsService _settings;

    public SubscriptionMaintenanceService(AdhicomDbContext db, Adhicom.Application.Common.ITenantContext tenant,
        Adhicom.Application.Common.IClock clock, Adhicom.Application.Notifications.INotificationService notifications,
        Adhicom.Application.Administration.ISystemSettingsService settings)
        => (_db, _tenant, _clock, _notifications, _settings) = (db, tenant, clock, notifications, settings);

    /// <summary>Échéance -> grâce -> limitation. Les données du commerçant ne sont JAMAIS supprimées.</summary>
    public async Task<int> ProcessExpirationsAsync(CancellationToken ct = default)
    {
        using (_tenant.BypassScope())
        {
            var now = _clock.UtcNow;
            var graceDays = await _settings.GetIntAsync("subscription.grace_days", 7, ct);
            var processed = 0;

            var expired = await _db.Subscriptions.Include(s => s.Plan)
                .Where(s => s.Status == SubscriptionStatus.Active && s.EndsAtUtc < now)
                .ToListAsync(ct);
            foreach (var sub in expired)
            {
                sub.Status = SubscriptionStatus.Grace;
                sub.GraceEndsAtUtc = now.AddDays(graceDays);
                sub.UpdatedAtUtc = now;
                _db.SubscriptionHistories.Add(new SubscriptionHistory { SubscriptionId = sub.Id, Event = "grace_started", Detail = $"Échéance dépassée. Période de grâce de {graceDays} jours." });
                await _notifications.NotifyAsync(sub.SubscriberUserId, null, "subscription", "Abonnement arrivé à échéance",
                    $"Votre abonnement {sub.Plan?.Name} a expiré. Une période de grâce de {graceDays} jours s'applique avant limitation des fonctionnalités. Renouvelez-le depuis /app/subscription.", "/app/subscription", ct);
                processed++;
            }

            if (expired.Count > 0) await _db.SaveChangesAsync(ct);

            var pastDue = await _db.Subscriptions.Include(s => s.Plan)
                .Where(s => s.Status == SubscriptionStatus.Grace && s.GraceEndsAtUtc != null && s.GraceEndsAtUtc < now)
                .ToListAsync(ct);
            foreach (var sub in pastDue)
            {
                sub.Status = SubscriptionStatus.PastDue;
                sub.UpdatedAtUtc = now;
                _db.SubscriptionHistories.Add(new SubscriptionHistory { SubscriptionId = sub.Id, Event = "past_due", Detail = "Fin de période de grâce. Fonctionnalités limitées (données conservées)." });
                await _notifications.NotifyAsync(sub.SubscriberUserId, null, "subscription", "Abonnement limité",
                    $"La période de grâce est écoulée pour votre abonnement {sub.Plan?.Name}. Votre boutique reste visible mais certaines fonctionnalités sont limitées. Vos données sont conservées.", "/app/subscription", ct);
                processed++;
            }

            if (pastDue.Count > 0) await _db.SaveChangesAsync(ct);
            return processed;
        }
    }
}
