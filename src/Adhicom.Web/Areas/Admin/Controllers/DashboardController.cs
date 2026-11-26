using Adhicom.Application.Common;
using Adhicom.Application.Stats;
using Adhicom.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Adhicom.Web.Areas.Admin.Controllers;

public class DashboardController : AdminControllerBase
{
    private readonly IStatsService _stats;

    public DashboardController(IStatsService stats, ITenantContext tenant, ICurrentUser currentUser)
        : base(tenant, currentUser)
        => _stats = stats;

    public async Task<IActionResult> Index() => View(new AdminDashboardViewModel { Stats = await _stats.GetAdminDashboardAsync() });
}
