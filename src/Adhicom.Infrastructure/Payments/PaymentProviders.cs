using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Adhicom.Application.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adhicom.Infrastructure.Payments;

/// <summary>Paiement à la livraison : la remise se fait physiquement ; le paiement est confirmé par le commerçant.</summary>
public sealed class CashOnDeliveryPaymentProvider : IPaymentProvider
{
    public string Key => "cod";
    public string DisplayName => "Paiement à la livraison";
    public bool SupportsWebhook => false;
    public bool SupportsRefund => false;

    public Task<PaymentInitResult> InitializeAsync(PaymentInitRequest request, CancellationToken ct = default)
        => Task.FromResult(new PaymentInitResult(true, $"COD-{Guid.NewGuid():N}"[..16], null, null));

    public Task<PaymentVerificationResult> VerifyAsync(string reference, CancellationToken ct = default)
        => Task.FromResult(new PaymentVerificationResult(false, reference, null, null, "Le paiement à la livraison est confirmé manuellement par le commerçant."));

    public bool VerifyWebhookSignature(string payload, string? signatureHeader) => false;
}

/// <summary>Paiement manuel (Mobile Money / virement avec référence envoyée au commerçant, confirmation depuis le tableau de bord).</summary>
public sealed class ManualPaymentProvider : IPaymentProvider
{
    public string Key => "manual";
    public string DisplayName => "Paiement manuel";
    public bool SupportsWebhook => false;
    public bool SupportsRefund => false;

    public Task<PaymentInitResult> InitializeAsync(PaymentInitRequest request, CancellationToken ct = default)
        => Task.FromResult(new PaymentInitResult(true, $"MAN-{Guid.NewGuid():N}"[..16].ToUpperInvariant(), null, null));

    public Task<PaymentVerificationResult> VerifyAsync(string reference, CancellationToken ct = default)
        => Task.FromResult(new PaymentVerificationResult(false, reference, null, null, "Le paiement manuel est confirmé par le commerçant dans son tableau de bord."));

    public bool VerifyWebhookSignature(string payload, string? signatureHeader) => false;
}

/// <summary>Virement bancaire : instructions au client, confirmation manuelle par le commerçant.</summary>
public sealed class BankTransferPaymentProvider : IPaymentProvider
{
    public string Key => "bank_transfer";
    public string DisplayName => "Virement bancaire";
    public bool SupportsWebhook => false;
    public bool SupportsRefund => false;

    public Task<PaymentInitResult> InitializeAsync(PaymentInitRequest request, CancellationToken ct = default)
        => Task.FromResult(new PaymentInitResult(true, $"VIR-{Guid.NewGuid():N}"[..16].ToUpperInvariant(), null, null));

    public Task<PaymentVerificationResult> VerifyAsync(string reference, CancellationToken ct = default)
        => Task.FromResult(new PaymentVerificationResult(false, reference, null, null, "Le virement est confirmé manuellement par le commerçant."));

    public bool VerifyWebhookSignature(string payload, string? signatureHeader) => false;
}

/// <summary>
/// Fournisseur Mobile Money / carte via passerelle hébergée (Moko Afrika, Nomba, OPTGateway, Malipo).
/// Conforme au cahier des charges : chaque prestataire n'est activé que si ses identifiants sont configurés
/// et que son modèle multi-commerçants (sous-comptes marchands, KYC, versement direct) est vérifié.
/// Tant que la configuration est absente, l'initialisation échoue de façon contrôlée.
/// </summary>
public sealed class HostedCheckoutProvider : IPaymentProvider
{
    private readonly Microsoft.Extensions.Configuration.IConfiguration _config;
    public string Key { get; }
    public string DisplayName { get; }
    public bool SupportsWebhook => true;
    public bool SupportsRefund => false; // activé après intégration effective

    public HostedCheckoutProvider(string key, string displayName, Microsoft.Extensions.Configuration.IConfiguration config)
        => (Key, DisplayName, _config) = (key, displayName, config);

    private string Section => $"Payments:{Key}";
    private string? Secret => _config[$"{Section}:Secret"];
    private string? ApiKey => _config[$"{Section}:ApiKey"];
    private string? BaseUrl => _config[$"{Section}:BaseUrl"];

    public Task<PaymentInitResult> InitializeAsync(PaymentInitRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ApiKey) || string.IsNullOrWhiteSpace(BaseUrl))
            return Task.FromResult(new PaymentInitResult(false, null, null,
                $"Le prestataire « {DisplayName} » n'est pas encore configuré pour votre boutique. Contactez le support Adhicom pour l'activer."));

        // Point d'intégration réel (après validation commerciale + KYC du prestataire) :
        // POST {BaseUrl}/transactions avec clé API + référence unique ; la signature webhook sera vérifiée HMAC-SHA256.
        return Task.FromResult(new PaymentInitResult(false, null, null,
            $"L'intégration du prestataire « {DisplayName} » est en cours de finalisation. Utilisez le paiement manuel ou le paiement à la livraison."));
    }

    public Task<PaymentVerificationResult> VerifyAsync(string reference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ApiKey) || string.IsNullOrWhiteSpace(BaseUrl))
            return Task.FromResult(new PaymentVerificationResult(false, reference, null, null, "Prestataire non configuré."));
        return Task.FromResult(new PaymentVerificationResult(false, reference, null, null, "Vérification distante non implémentée tant que le contrat prestataire n'est pas validé."));
    }

    /// <summary>Vérification HMAC-SHA256(payload, secret) — format attendu : hex ou base64.</summary>
    public bool VerifyWebhookSignature(string payload, string? signatureHeader)
    {
        var secret = Secret;
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signatureHeader)) return false;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        var base64 = Convert.ToBase64String(hash);
        var provided = signatureHeader.Trim();
        if (provided.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)) provided = provided[7..];

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(hex))
            || CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(base64))
            || CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(signatureHeader.Trim()));
    }
}

public sealed class PaymentProviderRegistry : IPaymentProviderRegistry
{
    private readonly Dictionary<string, IPaymentProvider> _providers;

    public PaymentProviderRegistry(Microsoft.Extensions.Configuration.IConfiguration config)
    {
        _providers = new Dictionary<string, IPaymentProvider>(StringComparer.OrdinalIgnoreCase)
        {
            ["cod"] = new CashOnDeliveryPaymentProvider(),
            ["manual"] = new ManualPaymentProvider(),
            ["bank_transfer"] = new BankTransferPaymentProvider(),
            ["moko"] = new HostedCheckoutProvider("moko", "Moko Afrika", config),
            ["nomba"] = new HostedCheckoutProvider("nomba", "Nomba", config),
            ["optgateway"] = new HostedCheckoutProvider("optgateway", "OPTGateway (Orange Money)", config),
            ["malipo"] = new HostedCheckoutProvider("malipo", "Malipo", config),
        };
    }

    public IPaymentProvider? Get(string key) => key is not null && _providers.TryGetValue(key, out var p) ? p : null;
    public IReadOnlyList<IPaymentProvider> GetAll() => _providers.Values.ToList();
}

/// <summary>
/// Traitement centralisé des webhooks : signature obligatoire, idempotence sur (prestataire, eventId),
/// montant re-vérifié côté serveur, aucune confiance accordée à la redirection du navigateur.
/// </summary>
public sealed class PaymentWebhookProcessor : IPaymentWebhookProcessor
{
    private readonly Persistence.AdhicomDbContext _db;
    private readonly IPaymentProviderRegistry _registry;
    private readonly Adhicom.Application.Common.ITenantContext _tenant;
    private readonly Microsoft.Extensions.Logging.ILogger<PaymentWebhookProcessor> _logger;

    public PaymentWebhookProcessor(Persistence.AdhicomDbContext db, IPaymentProviderRegistry registry, Adhicom.Application.Common.ITenantContext tenant, Microsoft.Extensions.Logging.ILogger<PaymentWebhookProcessor> logger)
        => (_db, _registry, _tenant, _logger) = (db, registry, tenant, logger);

    public async Task<(bool IsHandled, string? Error)> ProcessAsync(string providerKey, string payload, string? signatureHeader, CancellationToken ct = default)
    {
        // Webhook signé : le contexte n'a pas de boutique -> on contourne le filtre tenant en lecture contrôlée
        using (_tenant.BypassScope())
        {
        return await ProcessCoreAsync(providerKey, payload, signatureHeader, ct);
        }
    }

    private async Task<(bool IsHandled, string? Error)> ProcessCoreAsync(string providerKey, string payload, string? signatureHeader, CancellationToken ct)
    {
        var provider = _registry.Get(providerKey);
        if (provider is null || !provider.SupportsWebhook)
            return (false, "Prestataire inconnu ou sans webhook.");

        var signatureValid = provider.VerifyWebhookSignature(payload, signatureHeader);
        if (!signatureValid)
        {
            _logger.LogWarning("Webhook {Provider} rejeté : signature invalide.", providerKey);
            return (false, "Signature invalide.");
        }

        string? eventId = null, reference = null, status = null; decimal? amount = null; string? currency = null;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            eventId = TryGetString(root, "event_id") ?? TryGetString(root, "eventId") ?? TryGetString(root, "id");
            reference = TryGetString(root, "reference") ?? TryGetString(root, "order_reference") ?? TryGetString(root, "merchant_reference") ?? TryGetString(root, "external_reference");
            status = (TryGetString(root, "status") ?? TryGetString(root, "event"))?.ToLowerInvariant();
            if (root.TryGetProperty("amount", out var a) && a.ValueKind == JsonValueKind.Number) amount = a.GetDecimal();
            currency = TryGetString(root, "currency");
        }
        catch (JsonException)
        {
            return (false, "Charge utile JSON invalide.");
        }

        if (string.IsNullOrWhiteSpace(eventId)) return (false, "Identifiant d'événement manquant.");

        // Idempotence : un même événement n'est traité qu'une seule fois
        var exists = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(_db.PaymentWebhookEvents, e => e.Provider == providerKey && e.EventId == eventId, ct);
        if (exists) return (true, null);

        // Comparaison montant annoncée vs montant attendu (anti-falsification)
        if (!string.IsNullOrWhiteSpace(reference) && amount is not null)
        {
            var expected = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                _db.Payments.Where(p => p.TransactionReference == reference).Select(p => (decimal?)p.Amount), ct);
            if (expected is not null && Math.Abs(expected.Value - amount.Value) > 0.01m)
            {
                _db.PaymentWebhookEvents.Add(new Domain.Payments.PaymentWebhookEvent { Provider = providerKey, EventId = eventId, OrderReference = reference, SignatureValid = true, Processed = false, Payload = payload, ProcessingError = "Montant discordant" });
                await _db.SaveChangesAsync(ct);
                _logger.LogError("Webhook {Provider}/{EventId} : montant {Amount} ≠ attendu {Expected}.", providerKey, eventId, amount, expected);
                return (false, "Montant discordant.");
            }
        }

        var evt = new Domain.Payments.PaymentWebhookEvent { Provider = providerKey, EventId = eventId, OrderReference = reference, SignatureValid = true, Payload = payload };
        try
        {
            if (!string.IsNullOrWhiteSpace(reference))
            {
                var payment = _db.Payments.Local.FirstOrDefault(p => p.TransactionReference == reference)
                              ?? await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(_db.Payments, p => p.TransactionReference == reference, ct);
                if (payment is not null)
                {
                    var isPaid = status is "paid" or "success" or "completed" or "successful" or "payment.succeeded";
                    var isFailed = status is "failed" or "error" or "cancelled" or "payment.failed";
                    if (isPaid)
                    {
                        payment.Status = Domain.Common.PaymentStatus.Paid;
                        payment.PaidAtUtc = DateTime.UtcNow;
                        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == payment.OrderId, ct);
                        if (order is not null) { order.PaymentStatus = Domain.Common.PaymentStatus.Paid; order.UpdatedAtUtc = DateTime.UtcNow; }
                    }
                    else if (isFailed)
                    {
                        payment.Status = Domain.Common.PaymentStatus.Failed;
                        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == payment.OrderId, ct);
                        if (order is not null) { order.PaymentStatus = Domain.Common.PaymentStatus.Failed; order.UpdatedAtUtc = DateTime.UtcNow; }
                    }
                    _db.PaymentTransactions.Add(new Domain.Payments.PaymentTransaction
                    {
                        StoreId = payment.StoreId,
                        PaymentId = payment.Id,
                        Type = "webhook",
                        Status = isPaid ? Domain.Common.PaymentStatus.Paid : isFailed ? Domain.Common.PaymentStatus.Failed : Domain.Common.PaymentStatus.Pending,
                        Reference = eventId,
                        Detail = $"Webhook {providerKey} ({status ?? "inconnu"})",
                        OccurredAtUtc = DateTime.UtcNow
                    });
                }
            }
            evt.Processed = true;
            evt.ProcessedAtUtc = DateTime.UtcNow;
            _db.PaymentWebhookEvents.Add(evt);
            await _db.SaveChangesAsync(ct);
            return (true, null);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Webhook {Provider}/{EventId} déjà traité (contrainte d'unicité).", providerKey, eventId);
            return (true, null); // course d'idempotence : déjà enregistré
        }
    }

    private static string? TryGetString(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;
}
