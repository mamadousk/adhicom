using Adhicom.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Web.Areas.Admin.Controllers;

public class AuditController : AdminControllerBase
{
    private readonly Adhicom.Infrastructure.Persistence.AdhicomDbContext _db;

    public AuditController(Adhicom.Infrastructure.Persistence.AdhicomDbContext db, ITenantContext tenant, ICurrentUser currentUser)
        : base(tenant, currentUser)
        => _db = db;

    public async Task<IActionResult> Index(int page = 1)
    {
        page = Math.Max(1, page);
        const int pageSize = 50;
        var total = await _db.AuditLogs.CountAsync();
        var logs = await _db.AuditLogs.AsNoTracking().OrderByDescending(l => l.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        ViewData["Page"] = page;
        ViewData["TotalPages"] = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        ViewData["Total"] = total;
        return View(logs);
    }
}
