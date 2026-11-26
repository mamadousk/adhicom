using Adhicom.Application.Stats;
using Adhicom.Domain.Common;
using Adhicom.Domain.Orders;
using Adhicom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Infrastructure.Services;

/// <summary>
/// Statistiques. RÈGLE MÉTIER du chiffre d'affaires :
/// CA réalisé = commandes PAYÉES ou LIVRÉES. Une commande non payée n'est jamais comptée comme un encaissement.
/// Les dates sont stockées en UTC ; l'affichage se fait selon le fuseau de la boutique.
/// </summary>
public sealed class StatsService : IStatsService
{
    private readonly AdhicomDbContext _db;

    public StatsService(AdhicomDbContext db) => _db = db;

    // Une commande compte comme vente dès qu'elle est payée ou livrée
    private static bool CountsAsRevenue(Order o) => o.PaymentStatus == PaymentStatus.Paid || o.Status == OrderStatus.Delivered;

    public async Task<MerchantDashboardDto> GetMerchantDashboardAsync(Guid storeId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var since = today.AddDays(-13);

        var orders = await _db.Orders.AsNoTracking()
            .Where(o => o.StoreId == storeId && o.CreatedAtUtc >= DateTime.SpecifyKind(since, DateTimeKind.Utc))
            .ToListAsync(ct);

        var pending = await _db.Orders.CountAsync(o => o.StoreId == storeId && o.Status == OrderStatus.Pending, ct);
        var productsCount = await _db.Products.CountAsync(p => p.StoreId == storeId && p.Status != ProductStatus.Archived, ct);
        var settingsThreshold = await _db.StoreSettings.AsNoTracking().Where(s => s.StoreId == storeId).Select(s => (int?)s.LowStockThreshold).FirstOrDefaultAsync(ct) ?? 5;
        var outOfStock = await _db.Products.CountAsync(p => p.StoreId == storeId && p.Status == ProductStatus.Active && p.StockQuantity <= 0, ct)
                       + await _db.Products.CountAsync(p => p.StoreId == storeId && p.Status == ProductStatus.Active && p.StockQuantity > 0 && p.StockQuantity <= settingsThreshold, ct);
        var customersCount = await _db.Customers.CountAsync(c => c.StoreId == storeId, ct);
        var recent = await _db.Orders.AsNoTracking().Where(o => o.StoreId == storeId).Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAtUtc).Take(6).ToListAsync(ct);

        // CA "aujourd'hui" et "ce mois" recalculés précisément (sous-ensemble des 14 jours pour aujourd'hui, mois complet ci-dessous)
        var revenueToday = await _db.Orders.AsNoTracking()
            .Where(o => o.StoreId == storeId && o.CreatedAtUtc >= DateTime.SpecifyKind(today, DateTimeKind.Utc)
                        && (o.PaymentStatus == PaymentStatus.Paid || o.Status == OrderStatus.Delivered))
            .SumAsync(o => (decimal?)o.Total, ct) ?? 0m;
        var revenueMonth = await _db.Orders.AsNoTracking()
            .Where(o => o.StoreId == storeId && o.CreatedAtUtc >= DateTime.SpecifyKind(monthStart, DateTimeKind.Utc)
                        && (o.PaymentStatus == PaymentStatus.Paid || o.Status == OrderStatus.Delivered))
            .SumAsync(o => (decimal?)o.Total, ct) ?? 0m;

        var sales = Enumerable.Range(0, 14)
            .Select(i => DateOnly.FromDateTime(today.AddDays(-13 + i)))
            .Select(d => new SalesPoint(d,
                orders.Count(o => DateOnly.FromDateTime(o.CreatedAtUtc) == d),
                orders.Where(CountsAsRevenue).Where(o => DateOnly.FromDateTime(o.CreatedAtUtc) == d).Sum(o => o.Total)))
            .ToList();

        var ordersToday = orders.Count(o => o.CreatedAtUtc >= today);
        return new MerchantDashboardDto(ordersToday, pending, revenueToday, revenueMonth, productsCount, outOfStock, customersCount, recent, sales);
    }

    public async Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var today = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var since = today.AddDays(-29);

        var totalStores = await _db.Stores.CountAsync(ct);
        var activeStores = await _db.Stores.CountAsync(s => s.Status == StoreStatus.Active, ct);
        var suspendedStores = await _db.Stores.CountAsync(s => s.Status == StoreStatus.Suspended, ct);
        var totalUsers = await _db.Users.CountAsync(ct);
        var ordersToday = await _db.Orders.CountAsync(o => o.CreatedAtUtc >= today, ct);
        var gmvMonth = await _db.Orders.AsNoTracking()
            .Where(o => o.CreatedAtUtc >= monthStart && (o.PaymentStatus == PaymentStatus.Paid || o.Status == OrderStatus.Delivered))
            .SumAsync(o => (decimal?)o.Total, ct) ?? 0m;
        var grace = await _db.Subscriptions.CountAsync(s => s.Status == Adhicom.Domain.Subscriptions.SubscriptionStatus.Grace, ct);

        var raw = await _db.Orders.AsNoTracking()
            .Where(o => o.CreatedAtUtc >= since)
            .ToListAsync(ct);

        var sales = Enumerable.Range(0, 30)
            .Select(i => DateOnly.FromDateTime(since.AddDays(i)))
            .Select(d => new SalesPoint(d,
                raw.Count(o => DateOnly.FromDateTime(o.CreatedAtUtc) == d),
                raw.Where(CountsAsRevenue).Where(o => DateOnly.FromDateTime(o.CreatedAtUtc) == d).Sum(o => o.Total)))
            .ToList();

        return new AdminDashboardDto(totalStores, activeStores, suspendedStores, totalUsers, ordersToday, gmvMonth, grace, sales);
    }

    public async Task<IReadOnlyList<SalesReportRow>> GetSalesReportAsync(Guid storeId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var fromUtc = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

        var rows = await _db.Orders.AsNoTracking()
            .Where(o => o.StoreId == storeId && o.CreatedAtUtc >= fromUtc && o.CreatedAtUtc <= toUtc)
            .Select(o => new { o.CreatedAtUtc, o.Status, o.PaymentStatus, o.Total })
            .ToListAsync(ct);

        var result = new List<SalesReportRow>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var dayRows = rows.Where(o => DateOnly.FromDateTime(o.CreatedAtUtc) == d).ToList();
            result.Add(new SalesReportRow(d,
                dayRows.Count,
                dayRows.Where(CountsAsRevenue).Sum(o => o.Total),
                dayRows.Count(o => o.Status == OrderStatus.Delivered),
                dayRows.Count(o => o.Status == OrderStatus.Cancelled || o.Status == OrderStatus.Refused)));
        }
        return result;
    }
}
