using Adhicom.Domain.Common;

namespace Adhicom.Domain.Catalog;

public class Category : TenantEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Product : TenantEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }       // Prix barré
    public string? Sku { get; set; }
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    public ProductStatus Status { get; set; } = ProductStatus.Draft;
    public int StockQuantity { get; set; }
    public decimal? WeightKg { get; set; }
    public string? DetailsJson { get; set; }           // Informations complémentaires libres (JSON)
    public int DisplayOrder { get; set; }
    public int SalesCount { get; set; }                // Quantité cumulée vendue (dénormalisé)

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
}

public class ProductImage : TenantEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public required string Url { get; set; }
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
}

public class ProductVariant : TenantEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public required string Name { get; set; }          // ex. "Taille", "Couleur"
    public required string Value { get; set; }         // ex. "XL", "Noir"
    public decimal? PriceAdjustment { get; set; }      // Prix spécifique de la variante (sinon prix produit)
    public string? Sku { get; set; }
    public int StockQuantity { get; set; }
}

public class InventoryMovement : TenantEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid? VariantId { get; set; }
    public int QuantityDelta { get; set; }             // Négatif = sortie, positif = entrée
    public InventoryMovementReason Reason { get; set; }
    public string? Reference { get; set; }             // Numéro de commande, etc.
    public Guid? CreatedByUserId { get; set; }
}
