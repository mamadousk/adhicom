using System.Security.Claims;
using Adhicom.Application.Common;

namespace Adhicom.Web.Infrastructure;

/// <summary>Tenant courant résolu par requête. Source unique du filtre multi-tenant EF Core.</summary>
public sealed class HttpTenantContext : ITenantContext
{
    private Guid? _storeId;
    private bool _bypass;

    public Guid? StoreId => _storeId;
    public bool Bypass => _bypass;

    public void SetStore(Guid? storeId, bool bypass = false)
    {
        _storeId = storeId;
        _bypass = bypass;
    }

    public IDisposable BypassScope()
    {
        var previousStore = _storeId;
        var previousBypass = _bypass;
        _bypass = true;
        return new BypassRestore(this, previousStore, previousBypass);
    }

    private sealed class BypassRestore : IDisposable
    {
        private readonly HttpTenantContext _context;
        private readonly Guid? _store;
        private readonly bool _bypass;
        public BypassRestore(HttpTenantContext context, Guid? store, bool bypass) => (_context, _store, _bypass) = (context, store, bypass);
        public void Dispose() => _context.SetStore(_store, _bypass);
    }
}

/// <summary>Utilisateur courant : identité lue EXCLUSIVEMENT depuis le principal authentifié (cookie).</summary>
public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public HttpCurrentUser(IHttpContextAccessor http) => _http = http;

    public Guid? UserId
    {
        get
        {
            var id = _http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(id, out var guid) ? guid : null;
        }
    }

    public string? UserName => _http.HttpContext?.User?.Identity?.Name;
    public string? Email => _http.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);
    public bool IsInRole(string role) => _http.HttpContext?.User?.IsInRole(role) ?? false;

    public string? IpAddress => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
