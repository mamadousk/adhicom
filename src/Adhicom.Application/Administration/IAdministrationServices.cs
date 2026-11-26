using Adhicom.Domain.Administration;

namespace Adhicom.Application.Administration;

public interface IAuditService
{
    Task LogAsync(string action, string entity, string? entityId, string? details, Guid? storeId = null, CancellationToken ct = default);
}

public interface ISupportService
{
    Task<SupportTicket> CreateTicketAsync(Guid? userId, Guid? storeId, string subject, string message, string? requesterName, string? requesterEmail, CancellationToken ct = default);
}

/// <summary>Paramètres globaux (clé/valeur) avec cache léger.</summary>
public interface ISystemSettingsService
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default);
    Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default);
    Task SetAsync(string key, string value, string? description, CancellationToken ct = default);
}

/// <summary>Contenus éditoriaux du site principal.</summary>
public interface ISiteContentService
{
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default);
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, string? description, CancellationToken ct = default);
}
