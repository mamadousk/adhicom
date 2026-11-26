using Adhicom.Application.Common;

namespace Adhicom.Application.Payments;

public record PaymentInitRequest(Guid StoreId, Guid OrderId, decimal Amount, string Currency, string? CustomerPhone, string? CustomerEmail, string Description, string? CallbackUrl);
public record PaymentInitResult(bool Success, string? Reference, string? RedirectUrl, string? Error);
public record PaymentVerificationResult(bool Success, string? Reference, decimal? Amount, string? Currency, string? Error);

/// <summary>
/// Contrat commun de tous les prestataires (Moko Afrika, Nomba, OPTGateway, Malipo, COD, manuel).
/// Aucune logique propre à un prestataire n'est codée hors de son implémentation.
/// </summary>
public interface IPaymentProvider
{
    string Key { get; }
    string DisplayName { get; }
    bool SupportsWebhook { get; }
    bool SupportsRefund { get; }
    Task<PaymentInitResult> InitializeAsync(PaymentInitRequest request, CancellationToken ct = default);
    Task<PaymentVerificationResult> VerifyAsync(string reference, CancellationToken ct = default);
    /// <summary>Vérifie la signature HMAC du webhook. DOIT être appelée avant toute action métier.</summary>
    bool VerifyWebhookSignature(string payload, string? signatureHeader);
}

public interface IPaymentProviderRegistry
{
    IPaymentProvider? Get(string key);
    IReadOnlyList<IPaymentProvider> GetAll();
}

/// <summary>Point d'entrée unique des webhooks : idempotence, signature, rapprochement.</summary>
public interface IPaymentWebhookProcessor
{
    Task<(bool IsHandled, string? Error)> ProcessAsync(string providerKey, string payload, string? signatureHeader, CancellationToken ct = default);
}
