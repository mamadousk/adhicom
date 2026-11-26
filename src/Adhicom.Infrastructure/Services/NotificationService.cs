using Adhicom.Application.Notifications;
using Adhicom.Domain.Notifications;
using Adhicom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Infrastructure.Services;

public sealed class NotificationService : INotificationService
{
    private readonly AdhicomDbContext _db;
    private readonly Adhicom.Application.Common.IClock _clock;

    public NotificationService(AdhicomDbContext db, Adhicom.Application.Common.IClock clock) => (_db, _clock) = (db, clock);

    public async Task NotifyAsync(Guid? userId, Guid? storeId, string type, string title, string body, string? link, CancellationToken ct = default)
    {
        if (body.Length > 1000) body = body[..1000];
        if (title.Length > 200) title = title[..200];
        _db.Notifications.Add(new Notification { UserId = userId, StoreId = storeId, Type = type, Title = title, Body = body, Link = link, CreatedAtUtc = _clock.UtcNow });
        await _db.SaveChangesAsync(ct);
    }

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
        => _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public async Task<IReadOnlyList<Notification>> ListAsync(Guid userId, int take, CancellationToken ct = default)
        => await _db.Notifications.Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAtUtc).Take(Math.Clamp(take, 1, 100)).ToListAsync(ct);

    public async Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);
        if (n is null) return;
        n.IsRead = true;
        n.ReadAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
        => _db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
              .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAtUtc, _clock.UtcNow), ct);
}
