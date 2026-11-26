using Adhicom.Application.Administration;
using Adhicom.Application.Common;
using Adhicom.Domain.Subscriptions;
using Adhicom.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Web.Controllers;

public class HomeController : Controller
{
    private readonly ISiteContentService _content;
    private readonly ISystemSettingsService _settings;
    private readonly Adhicom.Infrastructure.Persistence.AdhicomDbContext _db;

    public HomeController(ISiteContentService content, ISystemSettingsService settings, Adhicom.Infrastructure.Persistence.AdhicomDbContext db)
        => (_content, _settings, _db) = (content, settings, db);

    private async Task<Dictionary<string, string>> ContentAsync() => new(await _content.GetAllAsync());

    public async Task<IActionResult> Index()
    {
        ViewData["Contents"] = await ContentAsync();
        ViewData["Plans"] = await _db.Plans.AsNoTracking().Include(p => p.Features).Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync();
        return View();
    }

    public IActionResult Fonctionnalites() => View();

    public async Task<IActionResult> Tarifs()
    {
        ViewData["Plans"] = await _db.Plans.AsNoTracking().Include(p => p.Features).Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync();
        return View();
    }

    public IActionResult Faq() => View();
    public IActionResult Cgu() => View();
    public IActionResult Confidentialite() => View();

    public async Task<IActionResult> Health() => Ok(new { status = "ok" });
}

public class ContactController : Controller
{
    private readonly ISupportService _support;
    private readonly ICurrentUser _currentUser;

    public ContactController(ISupportService support, ICurrentUser currentUser)
        => (_support, _currentUser) = (support, currentUser);

    [HttpGet]
    public IActionResult Index() => View(new ContactViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ContactViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        await _support.CreateTicketAsync(_currentUser.UserId, null, model.Subject, model.Message, model.Name, model.Email, ct);
        TempData["Toast"] = "Votre message a bien été envoyé. Notre équipe vous répond dans les meilleurs délais.";
        return RedirectToAction(nameof(Index));
    }
}

public class ErrorController : Controller
{
    [Route("/erreur")]
    public IActionResult Erreur() => View("Error");

    public IActionResult Error() => View();
}

public class SitemapController : Controller
{
    private readonly Adhicom.Infrastructure.Persistence.AdhicomDbContext _db;

    public SitemapController(Adhicom.Infrastructure.Persistence.AdhicomDbContext db) => _db = db;

    [HttpGet("/sitemap.xml")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Index()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var storeHost = Request.Host.Host; // ex. app.adhicom.com ou adhicom.com → on pointe la boutique publique via /storefront/{slug}
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        void Url(string loc, string changefreq, string priority, DateTime? lastmod = null)
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{System.Net.WebUtility.HtmlEncode(loc)}</loc>");
            if (lastmod is { } lm) sb.AppendLine($"    <lastmod>{lm.ToString("yyyy-MM-dd")}</lastmod>");
            sb.AppendLine($"    <changefreq>{changefreq}</changefreq>");
            sb.AppendLine($"    <priority>{priority}</priority>");
            sb.AppendLine("  </url>");
        }
        Url($"{baseUrl}/", "daily", "1.0");
        Url($"{baseUrl}/fonctionnalites", "monthly", "0.8");
        Url($"{baseUrl}/tarifs", "weekly", "0.9");
        Url($"{baseUrl}/faq", "monthly", "0.6");
        Url($"{baseUrl}/cgu", "yearly", "0.3");
        Url($"{baseUrl}/confidentialite", "yearly", "0.3");
        Url($"{baseUrl}/contact", "monthly", "0.5");

        var activeStores = await _db.Stores.AsNoTracking()
            .Where(s => s.Status == Adhicom.Domain.Common.StoreStatus.Active)
            .Select(s => new { s.Slug, s.UpdatedAtUtc })
            .Take(10000)
            .ToListAsync();
        foreach (var s in activeStores)
            Url($"{baseUrl}/storefront/{s.Slug}", "daily", "0.7", s.UpdatedAtUtc);

        sb.AppendLine("</urlset>");
        return Content(sb.ToString(), "application/xml");
    }
}
