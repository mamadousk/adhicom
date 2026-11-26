using Adhicom.Application.Administration;
using Adhicom.Application.Common;
using Adhicom.Domain.Administration;
using Adhicom.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Web.Areas.Admin.Controllers;

public class SupportController : AdminControllerBase
{
    private readonly Adhicom.Infrastructure.Persistence.AdhicomDbContext _db;
    private readonly IAuditService _audit;

    public SupportController(Adhicom.Infrastructure.Persistence.AdhicomDbContext db, IAuditService audit, ITenantContext tenant, ICurrentUser currentUser)
        : base(tenant, currentUser)
        => (_db, _audit) = (db, audit);

    public async Task<IActionResult> Index()
    {
        var open = await _db.SupportTickets.AsNoTracking().Where(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress)
            .OrderByDescending(t => t.CreatedAtUtc).Take(100).ToListAsync();
        var closed = await _db.SupportTickets.AsNoTracking().Where(t => t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Closed)
            .OrderByDescending(t => t.UpdatedAtUtc ?? t.CreatedAtUtc).Take(50).ToListAsync();
        return View(new SupportTicketViewModel { OpenTickets = open, ClosedTickets = closed });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Respond(Guid id, string response)
    {
        var ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is not null && !string.IsNullOrWhiteSpace(response))
        {
            ticket.AdminResponse = response.Trim();
            ticket.Status = TicketStatus.Resolved;
            ticket.ResolvedAtUtc = DateTime.UtcNow;
            ticket.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("support.responded", "SupportTicket", ticket.Id.ToString(), null);
        }
        return Redirect("/admin/support");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(Guid id)
    {
        var ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is not null)
        {
            ticket.Status = TicketStatus.Closed;
            ticket.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return Redirect("/admin/support");
    }
}
