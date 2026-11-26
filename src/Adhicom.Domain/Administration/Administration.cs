using Adhicom.Domain.Common;

namespace Adhicom.Domain.Administration;

/// <summary>Journal d'audit des opérations sensibles (admin + changements importants).</summary>
public class AuditLog : Entity
{
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public Guid? StoreId { get; set; }
    public required string Action { get; set; }        // ex. "store.suspended", "plan.updated"
    public required string Entity { get; set; }        // ex. "Store"
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Paramètres globaux de la plateforme (clé/valeur, éditables par l'admin).</summary>
public class SystemSetting : AuditableEntity
{
    public required string Key { get; set; }           // ex. "subscription.grace_days"
    public required string Value { get; set; }
    public string? Description { get; set; }
}

/// <summary>Contenu éditorial du site principal (accueil, bannières...).</summary>
public class SiteContent : AuditableEntity
{
    public required string Key { get; set; }           // ex. "home.hero_title"
    public required string Value { get; set; }
    public string? Description { get; set; }
}

/// <summary>Ticket de support ouvert par un commerçant.</summary>
public class SupportTicket : AuditableEntity
{
    public Guid? UserId { get; set; }
    public Guid? StoreId { get; set; }
    public required string Subject { get; set; }
    public required string Message { get; set; }
    public string? RequesterName { get; set; }
    public string? RequesterEmail { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public string? AdminResponse { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}

/// <summary>Signalement d'une boutique ou d'un contenu abusif.</summary>
public class Report : AuditableEntity
{
    public Guid? StoreId { get; set; }
    public required string Reason { get; set; }
    public string? Details { get; set; }
    public string? ReporterEmail { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.New;
    public string? AdminNote { get; set; }
}
