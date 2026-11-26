using Adhicom.Domain.Common;

namespace Adhicom.Domain.Notifications;

public class Notification : AuditableEntity
{
    public Guid? UserId { get; set; }                  // Destinataire (notif in-app)
    public Guid? StoreId { get; set; }                 // Boutique concernée (contexte)
    public required string Title { get; set; }
    public required string Body { get; set; }
    public string Type { get; set; } = "info";         // info, order, payment, subscription, stock, system
    public string? Link { get; set; }                  // Lien relatif dans l'application
    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}

/// <summary>Modèles de notification (email / in-app) — personnalisables par l'administration.</summary>
public class NotificationTemplate : AuditableEntity
{
    public required string Key { get; set; }           // ex. "order.created", "subscription.expiring"
    public required string Channel { get; set; }       // "email" | "inapp"
    public required string Subject { get; set; }       // Peut contenir des variables {{orderNumber}}
    public required string Body { get; set; }
    public bool IsActive { get; set; } = true;
}
