using Adhicom.Application.Common;
using Adhicom.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Adhicom.Web.Areas.Admin.Controllers;

/// <summary>
/// Base de l'administration générale (équipe Adhil &amp; Co).
/// Réservée au rôle SuperAdmin. Le filtre tenant est contourné (portée globale) — chaque action
/// sensible est journalisée dans AuditLogs.
/// </summary>
[Area("Admin")]
[Authorize(Roles = AppRoles.SuperAdmin)]
public abstract class AdminControllerBase : Controller
{
    protected ITenantContext Tenant { get; }
    protected ICurrentUser CurrentUserCtx { get; }

    protected AdminControllerBase(ITenantContext tenant, ICurrentUser currentUser)
        => (Tenant, CurrentUserCtx) = (tenant, currentUser);

    public override Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        Tenant.SetStore(null, bypass: true);
        return next();
    }
}
