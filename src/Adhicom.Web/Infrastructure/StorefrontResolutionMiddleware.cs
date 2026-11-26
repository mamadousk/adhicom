using System.Text.RegularExpressions;

namespace Adhicom.Web.Infrastructure;

/// <summary>
/// Résolution des hôtes de la plateforme :
///   adhicom.com            -> site public
///   app.adhicom.com        -> back-office commerçant (réécrit en /app/...)
///   admin.adhicom.com      -> administration générale (réécrit en /admin/...)
///   {slug}.adhicom.com     -> boutique publique (réécrit en /storefront/{slug}/...)
/// Les chemins statiques (/css, /js, /img, /uploads, /favicon.ico) ne sont jamais réécrits.
/// En développement local (localhost), les routes directes /app, /admin et /storefront/{slug} servent le même contenu.
/// </summary>
public sealed class HostResolutionMiddleware
{
    private static readonly HashSet<string> StaticPrefixes = new(StringComparer.OrdinalIgnoreCase) { "/css", "/js", "/img", "/uploads", "/favicon" };

    private readonly RequestDelegate _next;
    private readonly ILogger<HostResolutionMiddleware> _logger;
    private readonly string _baseDomain;

    public HostResolutionMiddleware(RequestDelegate next, ILogger<HostResolutionMiddleware> logger, IConfiguration config)
    {
        _next = next;
        _logger = logger;
        _baseDomain = (config["Adhicom:BaseDomain"] ?? "adhicom.com").ToLowerInvariant();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var host = (context.Request.Host.Host ?? string.Empty).ToLowerInvariant();
        var path = context.Request.Path.Value ?? "/";

        var isStatic = StaticPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        if (!isStatic && TryGetSubdomain(host, out var sub))
        {
            if (string.Equals(sub, "app", StringComparison.Ordinal) && !path.StartsWith("/app", StringComparison.OrdinalIgnoreCase))
            {
                context.Request.Path = Rewrite(path, "/app");
            }
            else if (string.Equals(sub, "admin", StringComparison.Ordinal) && !path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase))
            {
                context.Request.Path = Rewrite(path, "/admin");
            }
            else if (!IsReserved(sub) && !path.StartsWith("/storefront", StringComparison.OrdinalIgnoreCase))
            {
                // Sous-domaine boutique : réécriture interne vers la zone Storefront
                context.Request.Path = RewriteStorefront(path, sub);
            }
        }

        await _next(context);
    }

    private static string Rewrite(string path, string prefix)
        => path == "/" ? prefix + "/" : prefix + path;

    private static string RewriteStorefront(string path, string slug)
    {
        if (path == "/") return $"/storefront/{slug}/";
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return $"/storefront/{slug}/";

        return segments[0].ToLowerInvariant() switch
        {
            "produit" when segments.Length >= 2 => $"/storefront/{slug}/Product/{Uri.EscapeDataString(segments[1])}",
            "panier" => $"/storefront/{slug}/Panier",
            "commander" => $"/storefront/{slug}/Commander",
            "commande" when segments.Length >= 2 => $"/storefront/{slug}/Confirmation/{Uri.EscapeDataString(segments[1])}",
            _ => $"/storefront/{slug}{path}"
        };
    }

    private bool TryGetSubdomain(string host, out string subdomain)
    {
        subdomain = string.Empty;
        if (string.IsNullOrEmpty(host) || host == "localhost" || Regex.IsMatch(host, @"^(\d{1,3}\.){3}\d{1,3}$")) return false;

        if (host == _baseDomain || host == $"www.{_baseDomain}") return false;
        if (host.EndsWith("." + _baseDomain, StringComparison.Ordinal))
        {
            var sub = host[..^(_baseDomain.Length + 1)];
            if (!sub.Contains('.', StringComparison.Ordinal) && sub.Length > 0) { subdomain = sub; return true; }
        }
        // Wildcards dev : {slug}.localhost
        if (host.EndsWith(".localhost", StringComparison.Ordinal))
        {
            var sub = host[..^".localhost".Length];
            if (!sub.Contains('.', StringComparison.Ordinal) && sub.Length > 0) { subdomain = sub; return true; }
        }
        return false;
    }

    private static bool IsReserved(string sub) =>
        new[] { "www", "app", "admin", "api", "mail", "smtp", "cdn", "status", "support", "blog", "static", "assets", "ns1", "ns2" }
            .Contains(sub, StringComparer.OrdinalIgnoreCase);
}

/// <summary>En-têtes de sécurité HTTP pour toutes les réponses.</summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _allowFraming;

    public SecurityHeadersMiddleware(RequestDelegate next, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
    {
        _next = next;
        // En développement (prévisualisation embarquée), on n'émet pas d'en-têtes anti-iframe.
        _allowFraming = env.IsDevelopment() || env.IsEnvironment("Testing");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            if (!_allowFraming) headers["X-Frame-Options"] = "SAMEORIGIN";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            if (!headers.ContainsKey("Content-Security-Policy"))
            {
                var frameAncestors = _allowFraming ? "" : "; frame-ancestors 'self'";
                headers["Content-Security-Policy"] =
                    $"default-src 'self'; img-src 'self' data: https:; style-src 'self' 'unsafe-inline'; script-src 'self'; font-src 'self' data:; form-action 'self'; base-uri 'self'{frameAncestors}";
            }
            return Task.CompletedTask;
        });
        await _next(context);
    }
}
