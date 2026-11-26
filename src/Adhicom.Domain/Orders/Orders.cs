using Adhicom.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Adhicom.Domain.Orders;

public class Customer : TenantEntity
{
    public required string Name { get; set; }
    public required string Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime? LastOrderAtUtc { get; set; }
}

public class Order : TenantEntity
{
    public required string OrderNumber { get; set; }   // Public, ex. "AHC-20261126-4F8K2"
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string PaymentMethod { get; set; } = PaymentMethods.CashOnDelivery;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    // Instantané des coordonnées au moment de la commande (jamais réécrit par la fiche client)
    public required string CustomerName { get; set; }
    public required string CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerCity { get; set; }
    public string? CustomerDistrict { get; set; }      // Commune / quartier
    public string? CustomerAddress { get; set; }       // Adresse ou point de livraison
    public string? CustomerInstructions { get; set; }

    public decimal SubTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }                 // SubTotal - Discount + Shipping
    public string Currency { get; set; } = Currencies.Usd;
    public string? Notes { get; set; }
    public bool StockDeducted { get; set; }            // Garde contre les doubles décréments
    public DateTime? DeliveredAtUtc { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
}

public class OrderItem : TenantEntity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public Guid? ProductId { get; set; }               // Null si le produit a été supprimé
    public Guid? VariantId { get; set; }
    public required string ProductName { get; set; }   // Instantané
    public string? VariantName { get; set; }           // Instantané ex. "Taille: XL"
    public string? Sku { get; set; }
    public decimal UnitPrice { get; set; }             // Instantané
    public int Quantity { get; set; }
    [NotMapped]
    public decimal LineTotal => UnitPrice * Quantity;
}

public class OrderStatusHistory : TenantEntity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public OrderStatus? OldStatus { get; set; }
    public OrderStatus NewStatus { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string? ChangedByName { get; set; }
    public string? Comment { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}

public class ShoppingCart : TenantEntity
{
    public required string Token { get; set; }         // Cookie anonyme
    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}

public class CartItem : TenantEntity
{
    public Guid CartId { get; set; }
    public ShoppingCart? Cart { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public int Quantity { get; set; } = 1;
}
