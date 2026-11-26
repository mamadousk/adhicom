using Adhicom.Application.Common;
using Adhicom.Domain.Stores;

namespace Adhicom.Application.Stores;

public interface IStoreService
{
    /// <summary>Crée une boutique, vérifie la disponibilité du sous-domaine, initialise paramètres + membre owner + plan gratuit.</summary>
    Task<OperationResult<Store>> CreateStoreAsync(CreateStoreRequest request, CancellationToken ct = default);

    /// <summary>Vérifie la disponibilité d'un slug (sous-domaine).</summary>
    Task<bool> IsSlugAvailableAsync(string slug, CancellationToken ct = default);

    /// <summary>Liste les boutiques accessibles par l'utilisateur (via StoreMembers).</summary>
    Task<IReadOnlyList<Store>> ListMyStoresAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Vérifie que l'utilisateur est membre de la boutique (contrôle d'autorisation serveur).</summary>
    Task<bool> IsMemberAsync(Guid userId, Guid storeId, CancellationToken ct = default);

    /// <summary>Met à jour le profil public et l'apparence de la boutique.</summary>
    Task<OperationResult> UpdateProfileAsync(Guid storeId, Store profile, CancellationToken ct = default);

    /// <summary>Met à jour les paramètres étendus (devise, SEO, réseaux sociaux, livraison).</summary>
    Task<OperationResult> UpdateSettingsAsync(Guid storeId, StoreSettings settings, CancellationToken ct = default);

    /// <summary>Pourcentage de complétion de l'assistant de configuration (bar de progression).</summary>
    Task<int> GetSetupCompletionAsync(Guid storeId, CancellationToken ct = default);
}
