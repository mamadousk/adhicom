using Microsoft.AspNetCore.Identity;

namespace Adhicom.Infrastructure.Identity;

/// <summary>Utilisateur applicatif (ASP.NET Core Identity).</summary>
public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public string FullName => $"{FirstName} {LastName}".Trim() is { Length: > 0 } n ? n : (UserName ?? Email ?? "Utilisateur");
}
