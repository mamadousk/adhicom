using Adhicom.Application.Administration;
using Adhicom.Application.Common;
using Adhicom.Domain.Administration;
using Adhicom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Adhicom.Infrastructure.Services;

public sealed class AuditService : IAuditService
{
    private readonly AdhicomDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public AuditService(AdhicomDbContext db, ICurrentUser currentUser, IClock clock) => (_db, _currentUser, _clock) = (db, currentUser, clock);

    public async Task LogAsync(string action, string entity, string? entityId, string? details, Guid? storeId = null, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUser.UserId,
            UserName = _currentUser.UserName,
            StoreId = storeId,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Details = details,
            IpAddress = _currentUser.IpAddress,
            CreatedAtUtc = _clock.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class SupportService : ISupportService
{
    private readonly AdhicomDbContext _db;

    public SupportService(AdhicomDbContext db) => _db = db;

    public async Task<SupportTicket> CreateTicketAsync(Guid? userId, Guid? storeId, string subject, string message, string? requesterName, string? requesterEmail, CancellationToken ct = default)
    {
        var ticket = new SupportTicket
        {
            UserId = userId,
            StoreId = storeId,
            Subject = subject.Trim(),
            Message = message.Trim(),
            RequesterName = requesterName?.Trim(),
            RequesterEmail = requesterEmail?.Trim()
        };
        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync(ct);
        return ticket;
    }
}

public sealed class SystemSettingsService : ISystemSettingsService
{
    private readonly AdhicomDbContext _db;

    public SystemSettingsService(AdhicomDbContext db) => _db = db;

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
        => await _db.SystemSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(ct);

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default)
        => decimal.TryParse(await GetAsync(key, ct), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
        => int.TryParse(await GetAsync(key, ct), out var v) ? v : fallback;

    public async Task SetAsync(string key, string value, string? description, CancellationToken ct = default)
    {
        var entity = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (entity is null) { entity = new SystemSetting { Key = key, Value = value, Description = description }; _db.SystemSettings.Add(entity); }
        else { entity.Value = value; if (description is not null) entity.Description = description; entity.UpdatedAtUtc = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class SiteContentService : ISiteContentService
{
    private readonly AdhicomDbContext _db;

    public SiteContentService(AdhicomDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default)
        => await _db.SiteContents.AsNoTracking().ToDictionaryAsync(c => c.Key, c => c.Value, ct);

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
        => _db.SiteContents.AsNoTracking().Where(c => c.Key == key).Select(c => c.Value).FirstOrDefaultAsync(ct);

    public async Task SetAsync(string key, string value, string? description, CancellationToken ct = default)
    {
        var entity = await _db.SiteContents.FirstOrDefaultAsync(c => c.Key == key, ct);
        if (entity is null) { entity = new SiteContent { Key = key, Value = value, Description = description }; _db.SiteContents.Add(entity); }
        else { entity.Value = value; if (description is not null) entity.Description = description; entity.UpdatedAtUtc = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
    }
}
