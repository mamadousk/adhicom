namespace Adhicom.Domain.Common;

/// <summary>Rôles applicatifs. Les valeurs sont utilisées telles quelles dans ASP.NET Core Identity.</summary>
public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Merchant = "Merchant";
    public const string All = SuperAdmin + "," + Merchant;
}

/// <summary>Entité de base : identifiant unique non séquentiel.</summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

/// <summary>Entité auditée horodatée (UTC).</summary>
public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}

/// <summary>
/// Entité appartenant strictement à une boutique (multi-tenant).
/// Toutes les requêtes sur ces entités passent par le filtre global de tenant.
/// </summary>
public abstract class TenantEntity : AuditableEntity
{
    public Guid StoreId { get; set; }
}

/// <summary>Statuts possibles d'une boutique.</summary>
public enum StoreStatus
{
    Draft = 0,
    PendingValidation = 1,
    Active = 2,
    Suspended = 3,
    Disabled = 4,
    Closed = 5
}

/// <summary>Statuts d'un produit.</summary>
public enum ProductStatus
{
    Draft = 0,
    Active = 1,
    Archived = 2
}

/// <summary>Cycle de vie d'une commande.</summary>
public enum OrderStatus
{
    Pending = 0,       // En attente
    Confirmed = 1,     // Confirmée
    Preparing = 2,     // En préparation
    ReadyToShip = 3,   // Prête à être expédiée
    OutForDelivery = 4,// En livraison
    Delivered = 5,     // Livrée
    Cancelled = 6,     // Annulée
    Refused = 7,       // Refusée
    Returned = 8       // Retournée
}

/// <summary>Statut de paiement d'une commande ou d'un paiement.</summary>
public enum PaymentStatus
{
    Unpaid = 0,        // Non encaissé
    Pending = 1,       // En attente de confirmation
    Paid = 2,          // Encaissé
    Failed = 3,        // Échoué
    Refunded = 4,      // Remboursé
    ToBeConfirmed = 5  // Paiement manuel en attente de validation par le commerçant
}

/// <summary>Moyens de paiement d'une boutique.</summary>
public static class PaymentMethods
{
    public const string CashOnDelivery = "cod";          // Paiement à la livraison
    public const string Manual = "manual";               // Paiement manuel (Mobile Money / virement avec référence)
    public const string MobileMoney = "mobile_money";    // Mobile Money automatisé (prestataire)
    public const string Card = "card";                   // Carte bancaire (prestataire)
    public const string BankTransfer = "bank_transfer";  // Virement bancaire
    public const string All = "cod,manual,mobile_money,card,bank_transfer";
}

/// <summary>Statut d'un abonnement.</summary>
public enum SubscriptionStatus
{
    Active = 0,
    Grace = 1,       // Période de grâce après échéance / échec de paiement
    PastDue = 2,     // Grâce écoulée : fonctionnalités limitées
    Cancelled = 3,   // Annulé par le commerçant ou l'admin
    Expired = 4      // Expiré définitivement
}

/// <summary>Quand décrémenter le stock.</summary>
public enum StockDecrementMode
{
    OnOrder = 0,      // À la prise de commande (défaut)
    OnConfirmation = 1// À la confirmation de la commande par le commerçant
}

/// <summary>Mode de traitement des commandes WhatsApp.</summary>
public enum WhatsAppOrderMode
{
    RecordAndContact = 0, // Commande enregistrée dans Adhicom + bouton WhatsApp
    Redirect = 1          // Redirection vers wa.me avec récapitulatif prérempli
}

/// <summary>Type de méthode de livraison.</summary>
public enum ShippingMethodType
{
    Flat = 0,       // Forfait fixe
    Free = 1,       // Gratuite
    Pickup = 2,     // Retrait sur place
    Negotiated = 3  // À négocier avec le client (frais fixés manuellement)
}

/// <summary>Type d'écriture de stock.</summary>
public enum InventoryMovementReason
{
    OrderPlaced = 0,
    OrderConfirmed = 1,
    OrderCancelled = 2,
    ManualAdjustment = 3,
    Restock = 4,
    InitialStock = 5
}

/// <summary>Statut d'un ticket support.</summary>
public enum TicketStatus
{
    Open = 0,
    InProgress = 1,
    Resolved = 2,
    Closed = 3
}

/// <summary>Statut d'un signalement.</summary>
public enum ReportStatus
{
    New = 0,
    Reviewing = 1,
    Actioned = 2,
    Dismissed = 3
}

/// <summary>Devises supportées à ce stade.</summary>
public static class Currencies
{
    public const string Usd = "USD";
    public const string Cdf = "CDF";
    public const string Xaf = "XAF";
    public const string Xof = "XOF";
    public static readonly string[] All = { Usd, Cdf, Xaf, Xof };
}
