using Microsoft.AspNetCore.Mvc.Testing;

namespace Adhicom.SecurityTests;

/// <summary>
/// En-têtes de sécurité HTTP. Nécessite une application démarrable : ADHICOM_TEST_DB=1.
/// En l'absence de base, les tests passent sans rien vérifier (voir README).
/// </summary>
public class SecurityHeadersTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SecurityHeadersTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static bool CanRun => Environment.GetEnvironmentVariable("ADHICOM_TEST_DB") == "1";

    [Fact]
    public async Task Public_Pages_Set_Security_Headers()
    {
        if (!CanRun) return;
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/");
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("SAMEORIGIN", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("Referrer-Policy", response.Headers.Select(h => h.Key), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_Endpoint_Rejects_Get()
    {
        if (!CanRun) return;
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/webhooks/payments/moko");
        Assert.True((int)response.StatusCode == 405);
    }

    [Fact]
    public async Task Login_Page_Sets_Antiforgery_Cookie()
    {
        if (!CanRun) return;
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/account/login");
        response.EnsureSuccessStatusCode();
        Assert.Contains(response.Headers, h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase));
    }
}
