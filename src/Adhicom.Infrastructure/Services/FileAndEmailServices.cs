using System.Text;
using Adhicom.Application.Common;
using Microsoft.Extensions.Logging;

namespace Adhicom.Infrastructure.Services;

/// <summary>
/// Stockage local des fichiers sous wwwroot/uploads (défaut développement/VPS unique).
/// En production multi-serveurs, remplacer par un stockage compatible S3 (IFileStorage) — interface inchangée.
/// Contrôles : type MIME autorisé + extension déduite du MIME (jamais du nom client) + taille max 4 Mo.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    private const long MaxBytes = 4 * 1024 * 1024;

    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

    public LocalFileStorage(Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        => _env = env;

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, string folder, CancellationToken cancellationToken = default)
    {
        if (!Allowed.TryGetValue(contentType, out var ext))
            throw new InvalidOperationException("Format de fichier non autorisé. Formats acceptés : JPEG, PNG, WebP.");

        var root = string.IsNullOrWhiteSpace(_env.WebRootPath) ? Path.Combine(AppContext.BaseDirectory, "wwwroot") : _env.WebRootPath;
        var safeFolder = string.Join('/', (folder ?? "misc").Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => new string(p.Where(char.IsLetterOrDigit).ToArray()))
            .Where(p => p.Length > 0));
        var directory = Path.Combine(root, "uploads", safeFolder);
        Directory.CreateDirectory(directory);

        var name = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(directory, name);

        await using var file = File.Create(path);
        var buffer = new byte[81920];
        long total = 0; int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > MaxBytes)
            {
                await file.DisposeAsync();
                File.Delete(path);
                throw new InvalidOperationException("Image trop volumineuse (4 Mo maximum).");
            }
            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return $"/uploads/{safeFolder}/{name}";
    }

    public Task DeleteAsync(string publicUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicUrl) || !publicUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        var root = string.IsNullOrWhiteSpace(_env.WebRootPath) ? Path.Combine(AppContext.BaseDirectory, "wwwroot") : _env.WebRootPath;
        var relative = publicUrl["/uploads/".Length..].Replace('/', Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(root, "uploads", relative));
        var uploadsRoot = Path.GetFullPath(Path.Combine(root, "uploads"));
        if (path.StartsWith(uploadsRoot, StringComparison.Ordinal) && File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}

/// <summary>E-mail journalisé (défaut) — les e-mails apparaissent dans les logs applicatifs.</summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly Microsoft.Extensions.Logging.ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(Microsoft.Extensions.Logging.ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("E-MAIL (journalisé) pour {To} | Sujet : {Subject}\n{Body}", to, subject, htmlBody);
        return Task.CompletedTask;
    }
}

/// <summary>Envoi SMTP réel (activé lorsque Smtp:Host est renseigné dans la configuration).</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly Microsoft.Extensions.Configuration.IConfiguration _config;
    private readonly Microsoft.Extensions.Logging.ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(Microsoft.Extensions.Configuration.IConfiguration config, Microsoft.Extensions.Logging.ILogger<SmtpEmailSender> logger)
        => (_config, _logger) = (config, logger);

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var host = _config["Smtp:Host"];
        var port = int.TryParse(_config["Smtp:Port"], out var p) ? p : 587;
        var user = _config["Smtp:User"];
        var password = _config["Smtp:Password"];
        var from = _config["Smtp:From"] ?? "Adhicom <no-reply@adhicom.com>";
        var enableSsl = bool.TryParse(_config["Smtp:EnableSsl"], out var ssl) ? ssl : true;

        using var message = new System.Net.Mail.MailMessage
        {
            From = new System.Net.Mail.MailAddress(from.Contains('@') ? from.Split('<')[1].TrimEnd('>') : from, "Adhicom"),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(to);

        using var client = new System.Net.Mail.SmtpClient(host, port) { EnableSsl = enableSsl };
        if (!string.IsNullOrWhiteSpace(user)) client.Credentials = new System.Net.NetworkCredential(user, password);

        try
        {
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            // Un e-mail non délivré ne doit jamais bloquer l'opération principale
            _logger.LogError(ex, "Échec d'envoi d'e-mail à {To} (sujet: {Subject})", to, subject);
        }
    }
}
