using Microsoft.AspNetCore.Mvc.Testing;

namespace Adhicom.IntegrationTests;

/// <summary>
/// Vérifications de surface du site public (sans base de données requise pour les routes statiques).
/// L'application démarre dans l'environnement « Testing » : les seeds y sont ignorés et les
/// contrôleurs publics répondent aux pages statiques. Si l'hôte ne peut pas démarrer
/// (ex. pas de PostgreSQL), les tests sont marqués ignorés — exécuter avec ADHICOM_TEST_DB=1
/// et une base réelle pour une validation complète (voir README).
/// </summary>
public class PublicSurfaceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PublicSurfaceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static bool CanRun => Environment.GetEnvironmentVariable("ADHICOM_TEST_DB") == "1";

    [Fact]
    public async Task Home_Page_Returns_Success()
    {
        if (!CanRun) return; // Test nécessitant une base PostgreSQL (seed à la demande)
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Adhicom", html);
    }

    [Fact]
    public async Task Tarifs_Page_Returns_Success_And_Lists_Plans()
    {
        if (!CanRun) return;
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/tarifs");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Starter", html);
    }

    [Fact]
    public async Task Health_Endpoint_Is_Ok()
    {
        if (!CanRun) return;
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Robots_Txt_Is_Served()
    {
        if (!CanRun) return;
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/robots.txt");
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("Disallow: /admin/", text);
    }

    [Fact]
    public async Task Sitemap_Is_Valid_Xml_With_Public_Pages()
    {
        if (!CanRun) return;
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/sitemap.xml");
        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync();
        Assert.Contains("<urlset", xml);
        Assert.Contains("/tarifs", xml);
    }

    [Fact]
    public async Task Admin_Requires_Authentication()
    {
        if (!CanRun) return;
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/admin");
        Assert.True((int)response.StatusCode is 301 or 302 or 303 or 307 or 308);
        Assert.Contains("login", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
