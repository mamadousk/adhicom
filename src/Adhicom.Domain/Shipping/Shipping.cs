using Adhicom.Domain.Common;

namespace Adhicom.Domain.Shipping;

public class ShippingZone : TenantEntity
{
    public required string Name { get; set; }          // ex. "Kinshasa centre"
    public string? Cities { get; set; }                // Communes/quartiers, séparés par des virgules
    public bool IsActive { get; set; } = true;
}

public class ShippingMethod : TenantEntity
{
    public Guid? ZoneId { get; set; }
    public ShippingZone? Zone { get; set; }
    public required string Name { get; set; }          // ex. "Livraison standard Kinshasa", "Retrait en boutique"
    public ShippingMethodType Type { get; set; } = ShippingMethodType.Flat;
    public decimal Price { get; set; }                 // 0 pour Free / Pickup
    public decimal? FreeOverAmount { get; set; }       // Gratuit au-delà de ce montant (override boutique)
    public string? EstimatedDelivery { get; set; }     // ex. "24-48h"
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}
