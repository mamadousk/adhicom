namespace Adhicom.Application.Stats;

public record MerchantDashboardDto(
    int OrdersToday,
    int PendingOrders,
    decimal RevenueTodayPaid,
    decimal RevenueMonthPaid,
    int ProductsCount,
    int OutOfStockCount,
    int CustomersCount,
    IReadOnlyList<Domain.Orders.Order> RecentOrders,
    IReadOnlyList<SalesPoint> SalesLast14Days);

public record SalesPoint(DateOnly Date, int Orders, decimal Revenue);

public record AdminDashboardDto(
    int TotalStores,
    int ActiveStores,
    int SuspendedStores,
    int TotalUsers,
    int OrdersToday,
    decimal GmvMonthPaid,
    int SubscriptionsInGrace,
    IReadOnlyList<SalesPoint> SalesLast30Days);

public record SalesReportRow(DateOnly Date, int Orders, decimal Revenue, int Delivered, int Cancelled);

public interface IStatsService
{
    Task<MerchantDashboardDto> GetMerchantDashboardAsync(Guid storeId, CancellationToken ct = default);
    Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SalesReportRow>> GetSalesReportAsync(Guid storeId, DateOnly from, DateOnly to, CancellationToken ct = default);
}
