using Adhicom.Application.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adhicom.Web.Controllers.Webhooks;

/// <summary>
/// Point d'entrée des webhooks prestataires (Moko, Nomba, OPTGateway, Malipo...).
/// Sécurité : signature HMAC vérifiée côté serveur + idempotence sur (provider, eventId).
/// Une redirection navigateur ne vaut JAMAIS confirmation de paiement.
/// </summary>
[ApiController]
[Route("webhooks/payments/{provider}")]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class PaymentWebhookController : ControllerBase
{
    private readonly IPaymentWebhookProcessor _processor;
    private readonly ILogger<PaymentWebhookController> _logger;

    public PaymentWebhookController(IPaymentWebhookProcessor processor, ILogger<PaymentWebhookController> logger)
        => (_processor, _logger) = (processor, logger);

    [HttpPost]
    public async Task<IActionResult> Receive(string provider, CancellationToken ct)
    {
        string payload;
        using (var reader = new StreamReader(Request.Body, leaveOpen: false))
            payload = await reader.ReadToEndAsync(ct);

        if (payload.Length > 256 * 1024) return BadRequest(new { error = "payload_too_large" });

        var signature = Request.Headers["X-Signature"].FirstOrDefault()
                     ?? Request.Headers["X-Hub-Signature-256"].FirstOrDefault()
                     ?? Request.Headers["X-Webhook-Signature"].FirstOrDefault();

        var (handled, error) = await _processor.ProcessAsync(provider, payload, signature, ct);
        if (!handled)
        {
            _logger.LogWarning("Webhook {Provider} non traité : {Error}", provider, error);
            return BadRequest(new { error = "rejected" });
        }
        return Ok(new { received = true });
    }
}
