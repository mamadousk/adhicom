using System.Globalization;
using System.Text;

namespace Adhicom.Domain.Common;

/// <summary>Génération de slugs ASCII à partir de texte libre (nom de boutique, catégorie...).</summary>
public static class SlugHelper
{
    public static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "boutique";

        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue; // accents
            sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-');
        }

        var slug = sb.ToString().Normalize(NormalizationForm.FormC);
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-", StringComparison.Ordinal);
        slug = slug.Trim('-');
        if (slug.Length > 48) slug = slug[..48].Trim('-');
        return slug.Length == 0 ? "boutique" : slug;
    }

    /// <summary>Vérifie qu'un slug est valide comme sous-domaine (RFC 1123 simplifié).</summary>
    public static bool IsValidSubdomain(string slug) =>
        !string.IsNullOrEmpty(slug)
        && slug.Length is >= 3 and <= 48
        && slug.All(c => char.IsLower(c) || char.IsDigit(c) || c == '-')
        && !slug.StartsWith('-') && !slug.EndsWith('-');

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    { "www","app","admin","api","mail","smtp","imap","ftp","cdn","status","support","help","blog","dev","staging","test","demo","adhicom","static","assets","images","webmail","cpanel","ns1","ns2" };

    public static bool IsReservedSubdomain(string slug) => Reserved.Contains(slug);
}
