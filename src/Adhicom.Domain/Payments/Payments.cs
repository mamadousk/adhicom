using Adhicom.Domain.Common;
using Adhicom.Domain.Orders;

namespace Adhicom.Domain.Payments;

/// <summary>Catalogue des prestataires (référentiel global, alimenté par Seed).</summary>
public class PaymentProvider : Entity
{
    public required string Key { get; set; }           // "cod", "manual", "moko", "nomba", "optgateway", "malipo"
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public bool SupportsWebhook { get; set; }
    public bool SupportsRefund { get; set; }
    public string? DocsUrl { get; set; }
}

/// <summary>Configuration par boutique des moyens de paiement acceptés (clés publiques uniquement ; les secrets restent dans la configuration serveur).</summary>
public class StorePaymentConfiguration : TenantEntity
{
    public required string Method { get; set; }        // PaymentMethods.*
    public bool IsEnabled { get; set; } = true;
    public string? PublicInstructions { get; set; }    // ex. numéro de réception Mobile Money affiché au client
    public string? PublicMerchantCode { get; set; }    // ex. identifiant marchand affiché
    public string? Notes { get; set; }
}

public class Payment : TenantEntity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public required string Method { get; set; }
    public required string Provider { get; set; }      // Clé du prestataire
    public decimal Amount { get; set; }
    public string Currency { get; set; } = Currencies.Usd;
    public PaymentStatus Status { get; set; } = PaymentStatus.Unpaid;
    public string? TransactionReference { get; set; }
    public string? ProviderPayloadJson { get; set; }
    public DateTime? PaidAtUtc { get; set; }
}

public class PaymentTransaction : TenantEntity
{
    public Guid PaymentId { get; set; }
    public Payment? Payment { get; set; }
    public required string Type { get; set; }          // "init", "verify", "webhook", "refund", "manual_confirm"
    public PaymentStatus Status { get; set; }
    public string? Reference { get; set; }
    public string? Detail { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Idempotence + audit des webhooks prestataires.</summary>
public class PaymentWebhookEvent : Entity
{
    public required string Provider { get; set; }
    public required string EventId { get; set; }       // Unique par prestataire
    public string? OrderReference { get; set; }
    public bool SignatureValid { get; set; }
    public bool Processed { get; set; }
    public string? Payload { get; set; }
    public string? ProcessingError { get; set; }
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }
}
