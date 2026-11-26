using Adhicom.Domain.Notifications;

namespace Adhicom.Application.Notifications;

public interface INotificationService
{
    Task NotifyAsync(Guid? userId, Guid? storeId, string type, string title, string body, string? link, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> ListAsync(Guid userId, int take, CancellationToken ct = default);
    Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}
