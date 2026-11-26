using Adhicom.Application.Catalog;
using Adhicom.Application.Common;
using Adhicom.Application.Subscriptions;
using Adhicom.Domain.Catalog;
using Adhicom.Domain.Common;
using Adhicom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Infrastructure.Services;

public sealed class EfProductService : IProductService
{
    private readonly AdhicomDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ISubscriptionLimitService _limits;
    private readonly IClock _clock;

    public EfProductService(AdhicomDbContext db, ITenantContext tenant, ISubscriptionLimitService limits, IClock clock)
        => (_db, _tenant, _limits, _clock) = (db, tenant, limits, clock);

    private async Task<string> UniqueSlugAsync(Guid storeId, string name, CancellationToken ct)
    {
        var baseSlug = SlugHelper.Slugify(name);
        var slug = baseSlug; var i = 2;
        while (await _db.Products.AsNoTracking().AnyAsync(p => p.StoreId == storeId && p.Slug == slug, ct))
            slug = $"{baseSlug}-{i++}";
        return slug;
    }

    public async Task<OperationResult<Product>> CreateAsync(Product product, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(product.Name)) return OperationResult<Product>.Fail("Le nom du produit est requis.");
        if (product.Price < 0) return OperationResult<Product>.Fail("Le prix ne peut pas être négatif.");
        if (product.CompareAtPrice is { } compare && compare < product.Price)
            return OperationResult<Product>.Fail("Le prix promotionnel doit être inférieur ou égal au prix de vente.");

        var check = await _limits.CanCreateProductAsync(product.StoreId, ct);
        if (!check.Allowed) return OperationResult<Product>.Fail(check.Reason ?? "Limite de produits atteinte pour votre plan.");

        product.Name = product.Name.Trim();
        product.Slug = await UniqueSlugAsync(product.StoreId, product.Name, ct);
        product.CreatedAtUtc = _clock.UtcNow;
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
        return OperationResult<Product>.Ok(product);
    }

    public async Task<OperationResult> UpdateAsync(Product product, CancellationToken ct = default)
    {
        var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == product.Id && p.StoreId == product.StoreId, ct);
        if (entity is null) return OperationResult.Fail("Produit introuvable.");
        if (string.IsNullOrWhiteSpace(product.Name)) return OperationResult.Fail("Le nom du produit est requis.");
        if (product.Price < 0) return OperationResult.Fail("Le prix ne peut pas être négatif.");

        entity.Name = product.Name.Trim();
        entity.Description = product.Description?.Trim();
        entity.Price = product.Price;
        entity.CompareAtPrice = product.CompareAtPrice;
        entity.Sku = product.Sku?.Trim();
        entity.CategoryId = product.CategoryId;
        entity.Status = product.Status;
        entity.StockQuantity = Math.Max(0, product.StockQuantity);
        entity.WeightKg = product.WeightKg;
        entity.DetailsJson = product.DetailsJson;
        entity.UpdatedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> SetStatusAsync(Guid productId, ProductStatus status, CancellationToken ct = default)
    {
        var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct); // filtre tenant actif
        if (entity is null) return OperationResult.Fail("Produit introuvable.");
        entity.Status = status;
        entity.UpdatedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> ArchiveAsync(Guid productId, CancellationToken ct = default)
    {
        // Suppression logique : on conserve le produit pour l'historique des commandes
        var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (entity is null) return OperationResult.Fail("Produit introuvable.");
        entity.Status = ProductStatus.Archived;
        entity.UpdatedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }
}

public sealed class EfCategoryService : ICategoryService
{
    private readonly AdhicomDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;

    public EfCategoryService(AdhicomDbContext db, ITenantContext tenant, IClock clock)
        => (_db, _tenant, _clock) = (db, tenant, clock);

    public async Task<OperationResult<Category>> CreateAsync(Category category, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(category.Name)) return OperationResult<Category>.Fail("Le nom de la catégorie est requis.");
        var baseSlug = SlugHelper.Slugify(category.Name);
        var slug = baseSlug; var i = 2;
        while (await _db.Categories.AsNoTracking().AnyAsync(c => c.StoreId == category.StoreId && c.Slug == slug, ct))
            slug = $"{baseSlug}-{i++}";
        category.Slug = slug;
        category.Name = category.Name.Trim();
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);
        return OperationResult<Category>.Ok(category);
    }

    public async Task<OperationResult> UpdateAsync(Category category, CancellationToken ct = default)
    {
        var entity = await _db.Categories.FirstOrDefaultAsync(c => c.Id == category.Id && c.StoreId == category.StoreId, ct);
        if (entity is null) return OperationResult.Fail("Catégorie introuvable.");
        if (string.IsNullOrWhiteSpace(category.Name)) return OperationResult.Fail("Le nom de la catégorie est requis.");
        entity.Name = category.Name.Trim();
        entity.Description = category.Description?.Trim();
        entity.DisplayOrder = category.DisplayOrder;
        entity.IsActive = category.IsActive;
        entity.ImageUrl = category.ImageUrl ?? entity.ImageUrl;
        entity.UpdatedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> DeleteAsync(Guid categoryId, CancellationToken ct = default)
    {
        var entity = await _db.Categories.FirstOrDefaultAsync(c => c.Id == categoryId, ct);
        if (entity is null) return OperationResult.Fail("Catégorie introuvable.");
        var inUse = await _db.Products.AnyAsync(p => p.CategoryId == categoryId, ct);
        if (inUse) return OperationResult.Fail("Cette catégorie contient des produits. Réaffectez-les ou archivez-la plutôt que de la supprimer.");
        _db.Categories.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }
}

public sealed class EfInventoryService : IInventoryService
{
    private readonly AdhicomDbContext _db;
    private readonly IClock _clock;

    public EfInventoryService(AdhicomDbContext db, IClock clock) => (_db, _clock) = (db, clock);

    public async Task<bool> TryDeductAsync(Guid storeId, Guid productId, Guid? variantId, int quantity, string? reference, Guid? userId, CancellationToken ct = default, InventoryMovementReason reason = InventoryMovementReason.OrderPlaced)
    {
        if (quantity <= 0) return false;

        if (_db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            // Chemin portable pour les tests (provider en mémoire)
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId && p.StoreId == storeId, ct);
            if (product is null || product.StockQuantity < quantity) return false;
            product.StockQuantity -= quantity;
            product.SalesCount += quantity;
            product.UpdatedAtUtc = _clock.UtcNow;
            await DeductVariantAsync(storeId, variantId, quantity, ct);
            _db.InventoryMovements.Add(BuildMovement(storeId, productId, variantId, -quantity, reference, userId, reason));
            await _db.SaveChangesAsync(ct);
            return true;
        }

        // Décrément atomique côté base : aucune double vente même sous charge
        var rows = await _db.Database.ExecuteSqlRawAsync(
            "UPDATE \"Products\" SET \"StockQuantity\" = \"StockQuantity\" - {0}, \"SalesCount\" = \"SalesCount\" + {0}, \"UpdatedAtUtc\" = {1} WHERE \"Id\" = {2} AND \"StoreId\" = {3} AND \"StockQuantity\" >= {0}",
            quantity, _clock.UtcNow, productId, storeId);
        if (rows != 1) return false;

        await DeductVariantAsync(storeId, variantId, quantity, ct);
        _db.InventoryMovements.Add(BuildMovement(storeId, productId, variantId, -quantity, reference, userId, reason));
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task RestockAsync(Guid storeId, Guid productId, Guid? variantId, int quantity, string? reference, Guid? userId, CancellationToken ct = default)
    {
        if (quantity <= 0) return;
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId && p.StoreId == storeId, ct);
        if (product is not null)
        {
            product.StockQuantity += quantity;
            product.SalesCount = Math.Max(0, product.SalesCount - quantity);
            product.UpdatedAtUtc = _clock.UtcNow;
        }
        if (variantId is { } vid)
        {
            var variant = await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == vid && v.StoreId == storeId, ct);
            if (variant is not null) variant.StockQuantity += quantity;
        }
        _db.InventoryMovements.Add(BuildMovement(storeId, productId, variantId, quantity, reference, userId, InventoryMovementReason.OrderCancelled));
        await _db.SaveChangesAsync(ct);
    }

    private async Task DeductVariantAsync(Guid storeId, Guid? variantId, int quantity, CancellationToken ct)
    {
        if (variantId is not { } vid) return;
        var variant = await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == vid && v.StoreId == storeId, ct);
        if (variant is not null) variant.StockQuantity = Math.Max(0, variant.StockQuantity - quantity);
    }

    private InventoryMovement BuildMovement(Guid storeId, Guid productId, Guid? variantId, int delta, string? reference, Guid? userId, InventoryMovementReason reason)
        => new()
        {
            StoreId = storeId,
            ProductId = productId,
            VariantId = variantId,
            QuantityDelta = delta,
            Reason = reason,
            Reference = reference,
            CreatedByUserId = userId,
            CreatedAtUtc = _clock.UtcNow
        };
}
