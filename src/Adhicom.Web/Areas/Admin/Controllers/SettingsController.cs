using Adhicom.Application.Administration;
using Adhicom.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Web.Areas.Admin.Controllers;

public class SettingsController : AdminControllerBase
{
    private readonly Adhicom.Infrastructure.Persistence.AdhicomDbContext _db;
    private readonly ISystemSettingsService _settings;
    private readonly IAuditService _audit;

    public SettingsController(Adhicom.Infrastructure.Persistence.AdhicomDbContext db, ISystemSettingsService settings, IAuditService audit, ITenantContext tenant, ICurrentUser currentUser)
        : base(tenant, currentUser)
        => (_db, _settings, _audit) = (db, settings, audit);

    public async Task<IActionResult> Index()
        => View(await _db.SystemSettings.AsNoTracking().OrderBy(s => s.Key).ToListAsync());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key)) return BadRequest();
        var existing = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key);
        await _settings.SetAsync(key, value ?? string.Empty, existing?.Description);
        await _audit.LogAsync("settings.updated", "SystemSetting", key, value);
        TempData["Toast"] = $"Paramètre « {key} » enregistré.";
        return Redirect("/admin/settings");
    }
}
