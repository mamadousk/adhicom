using System.Security.Cryptography;
using System.Text;
using Adhicom.Infrastructure.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Adhicom.SecurityTests;

/// <summary>Vérification HMAC-SHA256 des webhooks de paiement — défense en profondeur.</summary>
public class WebhookSignatureTests
{
    private static IConfiguration ConfigWithSecret(string secret) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Payments:moko:Secret"] = secret,
            ["Payments:moko:ApiKey"] = "test-api-key",
            ["Payments:moko:BaseUrl"] = "https://payments.example.com/api"
        })
        .Build();

    private static string SignHex(string payload, string secret)
        => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

    [Fact]
    public void Valid_Hex_Signature_Is_Accepted()
    {
        var provider = new HostedCheckoutProvider("moko", "Moko Afrika", ConfigWithSecret("topsecret"));
        const string payload = """{"reference":"PAY-123","status":"paid"}""";
        Assert.True(provider.VerifyWebhookSignature(payload, SignHex(payload, "topsecret")));
    }

    [Fact]
    public void Valid_Signature_With_Sha256_Prefix_Is_Accepted()
    {
        var provider = new HostedCheckoutProvider("moko", "Moko Afrika", ConfigWithSecret("topsecret"));
        const string payload = """{"reference":"PAY-123"}""";
        Assert.True(provider.VerifyWebhookSignature(payload, "sha256=" + SignHex(payload, "topsecret")));
    }

    [Fact]
    public void Tampered_Payload_Is_Rejected()
    {
        var provider = new HostedCheckoutProvider("moko", "Moko Afrika", ConfigWithSecret("topsecret"));
        var signature = SignHex("""{"amount":10}""", "topsecret");
        Assert.False(provider.VerifyWebhookSignature("""{"amount":1000000}""", signature));
    }

    [Fact]
    public void Wrong_Secret_Is_Rejected()
    {
        var provider = new HostedCheckoutProvider("moko", "Moko Afrika", ConfigWithSecret("topsecret"));
        var signature = SignHex("""{"a":1}""", "autresecret");
        Assert.False(provider.VerifyWebhookSignature("""{"a":1}""", signature));
    }

    [Fact]
    public void Missing_Signature_Is_Rejected()
    {
        var provider = new HostedCheckoutProvider("moko", "Moko Afrika", ConfigWithSecret("topsecret"));
        Assert.False(provider.VerifyWebhookSignature("{}", null));
        Assert.False(provider.VerifyWebhookSignature("{}", ""));
    }

    [Fact]
    public void Missing_Secret_Fails_Closed()
    {
        var provider = new HostedCheckoutProvider("moko", "Moko Afrika", new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build());
        Assert.False(provider.VerifyWebhookSignature("{}", "abc123"));
    }
}
