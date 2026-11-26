using Adhicom.Application.Common;
using Adhicom.Application.Notifications;
using Adhicom.Application.Orders;
using Adhicom.Application.Stores;
using Adhicom.Application.Subscriptions;
using Adhicom.Domain.Common;
using Adhicom.Domain.Orders;
using Adhicom.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Web.Areas.App.Controllers;

public class OrdersController : MerchantControllerBase
{
    private const int PageSize = 20;

    private readonly Adhicom.Infrastructure.Persistence.AdhicomDbContext _db;
    private readonly IOrderService _orders;

    public OrdersController(Adhicom.Infrastructure.Persistence.AdhicomDbContext db, IOrderService orders,
        IStoreService storeService, ITenantContext tenant, ICurrentUser currentUser, INotificationService notifications, ISubscriptionLimitService limits)
        : base(storeService, tenant, currentUser, notifications, limits)
        => (_db, _orders) = (db, orders);

    public async Task<IActionResult> Index(string? status, int page = 1)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        var storeId = CurrentStore.Id;
        page = Math.Max(1, page);

        var query = _db.Orders.Where(o => o.StoreId == storeId).AsQueryable();
        if (Enum.TryParse<OrderStatus>(status, true, out var parsed)) query = query.Where(o => o.Status == parsed);

        var total = await query.CountAsync();
        var orders = await query.OrderByDescending(o => o.CreatedAtUtc).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        var itemCounts = await _db.OrderItems.Where(i => orders.Select(o => o.Id).Contains(i.OrderId))
            .GroupBy(i => i.OrderId).Select(g => new { g.Key, Count = g.Sum(x => x.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Count);

        return View(new OrderListItemViewModel
        {
            Orders = orders,
            ItemCounts = itemCounts,
            StatusFilter = status,
            Page = page,
            TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize)),
            TotalCount = total
        });
    }

    public async Task<IActionResult> Details(Guid id)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id && o.StoreId == CurrentStore.Id);
        if (order is null) return NotFound();

        var history = await _db.OrderStatusHistories.Where(h => h.OrderId == order.Id && h.StoreId == CurrentStore.Id)
            .OrderByDescending(h => h.ChangedAtUtc).ToListAsync();

        var allowed = OrderStatusMachine.Allowed(order.Status);

        string? whatsApp = null;
        if (!string.IsNullOrWhiteSpace(order.CustomerPhone))
        {
            var message = $"Bonjour {order.CustomerName}, ici {CurrentStore.Name} au sujet de votre commande {order.OrderNumber}.";
            whatsApp = WhatsAppLink.Build(order.CustomerPhone, message);
        }

        return View(new OrderDetailsViewModel { Order = order, History = history, AllowedNextStatuses = allowed, WhatsAppUrl = whatsApp });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(Guid id, OrderStatus status, string? comment)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        var result = await _orders.UpdateStatusAsync(id, status, comment, CurrentUserId, CurrentUserCtx.UserName);
        TempData["Toast"] = result.Success ? "Statut de la commande mis à jour." : result.Error;
        TempData["ToastType"] = result.Success ? "success" : "error";
        return Redirect($"/app/orders/details/{id}");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayment(Guid id, string? reference)
    {
        if (CurrentStore is null) return Redirect("/app/stores/create");
        var result = await _orders.ConfirmManualPaymentAsync(id, CurrentUserId, reference);
        TempData["Toast"] = result.Success ? "Paiement confirmé. La commande est marquée payée." : result.Error;
        TempData["ToastType"] = result.Success ? "success" : "error";
        return Redirect($"/app/orders/details/{id}");
    }
}
