using Adhicom.Domain.Subscriptions;

namespace Adhicom.Application.Subscriptions;

public record SubscriptionLimitCheck(bool Allowed, string? Reason, int? Limit);

/// <summary>Application des limites de plan — toujours contrôlées côté serveur.</summary>
public interface ISubscriptionLimitService
{
    /// <summary>Abonnement effectif d'un utilisateur (ou plan Gratuit par défaut).</summary>
    Task<Plan> GetEffectivePlanAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Nombre de boutiques actives de l'utilisateur vs limite du plan.</summary>
    Task<int> CountStoresAsync(Guid userId, CancellationToken ct = default);
    Task<SubscriptionLimitCheck> CanCreateStoreAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Nombre de produits actifs de la boutique vs limite du plan.</summary>
    Task<int> CountProductsAsync(Guid storeId, CancellationToken ct = default);
    Task<SubscriptionLimitCheck> CanCreateProductAsync(Guid storeId, CancellationToken ct = default);

    Task<bool> CanUsePremiumThemesAsync(Guid userId, CancellationToken ct = default);
    Task<bool> CanExportCustomersAsync(Guid userId, CancellationToken ct = default);
    Task<bool> CanAccessAdvancedAnalyticsAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>Cycle de vie des abonnements : échéance, période de grâce, réactivation. Aucune donnée supprimée.</summary>
public interface ISubscriptionMaintenanceService
{
    Task<int> ProcessExpirationsAsync(CancellationToken ct = default);
}
