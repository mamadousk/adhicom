using Adhicom.Application.Notifications;
using Adhicom.Application.Stats;
using Adhicom.Application.Stores;
using Adhicom.Application.Subscriptions;
using Adhicom.Application.Common;
using Adhicom.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Web.Areas.App.Controllers;

public class DashboardController : MerchantControllerBase
{
    private readonly IStatsService _stats;
    private readonly Adhicom.Infrastructure.Persistence.AdhicomDbContext _db;

    public DashboardController(IStatsService stats, Adhicom.Infrastructure.Persistence.AdhicomDbContext db,
        IStoreService storeService, ITenantContext tenant, ICurrentUser currentUser, INotificationService notifications, ISubscriptionLimitService limits)
        : base(storeService, tenant, currentUser, notifications, limits)
        => (_stats, _db) = (stats, db);

    public async Task<IActionResult> Index()
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");

        var currency = await _db.StoreSettings.AsNoTracking().Where(s => s.StoreId == CurrentStore.Id).Select(s => s.Currency).FirstOrDefaultAsync() ?? "USD";
        var vm = new DashboardViewModel
        {
            Store = CurrentStore,
            Stats = await _stats.GetMerchantDashboardAsync(CurrentStore.Id),
            SetupCompletion = await StoreService.GetSetupCompletionAsync(CurrentStore.Id),
            Currency = currency
        };
        return View(vm);
    }
}
