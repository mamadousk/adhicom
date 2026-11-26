using Adhicom.Application.Administration;
using Adhicom.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Adhicom.Web.Areas.Admin.Controllers;

public class ContentController : AdminControllerBase
{
    private readonly ISiteContentService _content;
    private readonly IAuditService _audit;

    public ContentController(ISiteContentService content, IAuditService audit, ITenantContext tenant, ICurrentUser currentUser)
        : base(tenant, currentUser)
        => (_content, _audit) = (content, audit);

    public async Task<IActionResult> Index()
    {
        var all = await _content.GetAllAsync();
        ViewData["Contents"] = all.OrderBy(k => k.Key).ToList();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key)) return BadRequest();
        await _content.SetAsync(key, value ?? string.Empty, null);
        await _audit.LogAsync("content.updated", "SiteContent", key, null);
        TempData["Toast"] = $"Contenu « {key} » enregistré.";
        return Redirect("/admin/content");
    }
}
