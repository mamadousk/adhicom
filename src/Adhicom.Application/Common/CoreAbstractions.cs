namespace Adhicom.Application.Common;

/// <summary>Horloge injectable (UTC) — facilite les tests.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

/// <summary>Contexte utilisateur authentifié, résolu depuis le cookie Identity (aucun ID venant du client n'est accepté).</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    bool IsInRole(string role);
    string? IpAddress { get; }
}

/// <summary>
/// Tenant courant (boutique active). Implémenté par requête (scoped).
/// Source de vérité pour le filtre global EF Core : l'ID de boutique n'est JAMAIS pris dans l'URL ou un formulaire.
/// </summary>
public interface ITenantContext
{
    Guid? StoreId { get; }
    /// <summary>True = filtre tenant contourné (administration, tâches système). À utiliser avec la plus grande prudence.</summary>
    bool Bypass { get; }
    void SetStore(Guid? storeId, bool bypass = false);
    /// <summary>Contourne temporairement le filtre (ex. lister "mes boutiques"). Restaure l'état précédent à la sortie du using.</summary>
    IDisposable BypassScope();
}

/// <summary>Stockage objet des fichiers (images). Implémentation par défaut : disque local ; S3/compatible en production.</summary>
public interface IFileStorage
{
    /// <summary>Enregistre un fichier et retourne son URL publique relative (ex. "/uploads/x/y.jpg").</summary>
    Task<string> SaveAsync(Stream content, string fileName, string contentType, string folder, CancellationToken cancellationToken = default);
    Task DeleteAsync(string publicUrl, CancellationToken cancellationToken = default);
}

/// <summary>Envoi d'e-mails transactionnels. L'implémentation par défaut journalise ; SMTP configurable.</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

/// <summary>Générateur de numéros de commande publics uniques.</summary>
public interface IOrderNumberGenerator
{
    string Generate();
}
