using Adhicom.Application.Administration;
using Adhicom.Application.Common;
using Adhicom.Domain.Administration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Web.Areas.Admin.Controllers;

public class ReportsController : AdminControllerBase
{
    private readonly Adhicom.Infrastructure.Persistence.AdhicomDbContext _db;
    private readonly IAuditService _audit;

    public ReportsController(Adhicom.Infrastructure.Persistence.AdhicomDbContext db, IAuditService audit, ITenantContext tenant, ICurrentUser currentUser)
        : base(tenant, currentUser)
        => (_db, _audit) = (db, audit);

    public async Task<IActionResult> Index()
        => View(await _db.Reports.AsNoTracking().OrderByDescending(r => r.CreatedAtUtc).Take(200).ToListAsync());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(Guid id, ReportStatus status, string? adminNote)
    {
        var report = await _db.Reports.FirstOrDefaultAsync(r => r.Id == id);
        if (report is not null)
        {
            report.Status = status;
            if (!string.IsNullOrWhiteSpace(adminNote)) report.AdminNote = adminNote.Trim();
            report.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("report.status_changed", "Report", report.Id.ToString(), status.ToString());
        }
        return Redirect("/admin/reports");
    }
}
