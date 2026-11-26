using Adhicom.Application.Administration;
using Adhicom.Application.Common;
using Adhicom.Infrastructure.Identity;
using Adhicom.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Web.Areas.Admin.Controllers;

public class UsersController : AdminControllerBase
{
    private readonly Adhicom.Infrastructure.Persistence.AdhicomDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _audit;

    public UsersController(Adhicom.Infrastructure.Persistence.AdhicomDbContext db, UserManager<ApplicationUser> userManager, IAuditService audit, ITenantContext tenant, ICurrentUser currentUser)
        : base(tenant, currentUser)
        => (_db, _userManager, _audit) = (db, userManager, audit);

    public async Task<IActionResult> Index(string? search)
    {
        var users = await _db.Users.AsNoTracking().OrderByDescending(u => u.CreatedAtUtc).Take(500).ToListAsync();
        if (!string.IsNullOrWhiteSpace(search))
        {
            users = users.Where(u => (u.Email ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) || ((u.FirstName + " " + u.LastName).Contains(search, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        var memberships = await _db.StoreMembers.AsNoTracking().GroupBy(m => m.UserId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);

        var rows = new List<AdminUserRowViewModel>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            rows.Add(new AdminUserRowViewModel
            {
                Id = Guid.TryParse(user.Id, out var gid) ? gid : Guid.Empty,
                Email = user.Email ?? "",
                FullName = user.FullName,
                IsActive = user.IsActive,
                Roles = roles,
                StoreCount = Guid.TryParse(user.Id, out var uid) && memberships.TryGetValue(uid, out var c) ? c : 0,
                CreatedAtUtc = user.CreatedAtUtc
            });
        }

        return View(new AdminUsersViewModel { Users = rows, Search = search });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(Guid id)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id.ToString());
        if (user is not null)
        {
            // Un SuperAdmin ne peut pas se désactiver lui-même
            if (CurrentUserCtx.UserId == id) { TempData["Toast"] = "Vous ne pouvez pas désactiver votre propre compte."; TempData["ToastType"] = "error"; return Redirect("/admin/users"); }
            user.IsActive = !user.IsActive;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            await _audit.LogAsync(user.IsActive ? "user.activated" : "user.deactivated", "User", user.Id, user.Email);
            TempData["Toast"] = user.IsActive ? "Compte réactivé." : "Compte désactivé.";
        }
        return Redirect("/admin/users");
    }
}
