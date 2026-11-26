using Adhicom.Application.Common;
using Adhicom.Domain.Catalog;

namespace Adhicom.Application.Catalog;

public interface IProductService
{
    /// <summary>Crée un produit en appliquant la limite du plan côté serveur.</summary>
    Task<OperationResult<Product>> CreateAsync(Product product, CancellationToken ct = default);
    Task<OperationResult> UpdateAsync(Product product, CancellationToken ct = default);
    Task<OperationResult> SetStatusAsync(Guid productId, Domain.Common.ProductStatus status, CancellationToken ct = default);
    Task<OperationResult> ArchiveAsync(Guid productId, CancellationToken ct = default);
}

public interface ICategoryService
{
    Task<OperationResult<Category>> CreateAsync(Category category, CancellationToken ct = default);
    Task<OperationResult> UpdateAsync(Category category, CancellationToken ct = default);
    Task<OperationResult> DeleteAsync(Guid categoryId, CancellationToken ct = default);
}

public interface IInventoryService
{
    /// <summary>Décrément atomique (SQL) avec garde-fou anti stock négatif. Retourne false si stock insuffisant ou produit introuvable.</summary>
    Task<bool> TryDeductAsync(Guid storeId, Guid productId, Guid? variantId, int quantity, string? reference, Guid? userId, CancellationToken ct = default, Domain.Common.InventoryMovementReason reason = Domain.Common.InventoryMovementReason.OrderPlaced);
    /// <summary>Ré-incrément (annulation de commande).</summary>
    Task RestockAsync(Guid storeId, Guid productId, Guid? variantId, int quantity, string? reference, Guid? userId, CancellationToken ct = default);
}
