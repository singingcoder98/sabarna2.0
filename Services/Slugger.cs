using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Sabarna.Web.Data;

namespace Sabarna.Web.Services;

/// <summary>
/// saptarshi: 16-Nov-2026 — clean product URLs.
///
/// "/Shop/Details/7" tells a customer nothing and tells Google nothing. "/product/kuler-achar-500g"
/// carries the keyword the customer actually typed. This turns a product name into that slug:
///
///   "Kul Pickle (Sweet &amp; Sour) — 500 g"  →  "kul-pickle-sweet-sour-500-g"
///
/// Latin letters and digits only — no transliteration of Bengali, because a Bengali slug gets
/// percent-encoded into an address nobody can read or share.
/// </summary>
public static class Slugger
{
    /// <summary>The slug a name would get, before we check it is unique.</summary>
    public static string From(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "product";
        }

        // Decompose accented letters, then drop the accents: "é" becomes "e".
        var normalised = name.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalised.Length);

        foreach (var character in normalised)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                // Anything that is not a letter or a digit becomes a single dash.
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');

        // A slug that is nothing but dashes (a Bengali-only name, say) still needs to exist.
        if (slug.Length == 0)
        {
            slug = "product";
        }

        return slug.Length <= 80 ? slug : slug[..80].Trim('-');
    }

    /// <summary>
    /// A slug no other product is using. Duplicate names are real — two sizes of the same jar is
    /// exactly how the owner writes them — so a clash gets a number, not an error.
    /// </summary>
    public static async Task<string> MakeUniqueAsync(AppDbContext db, string? name, int currentProductId)
    {
        var baseSlug = From(name);
        var candidate = baseSlug;

        for (var attempt = 2; attempt <= 20; attempt++)
        {
            var taken = await db.Products.AsNoTracking()
                .AnyAsync(p => p.Slug == candidate && p.Id != currentProductId);

            if (!taken)
            {
                return candidate;
            }

            candidate = $"{baseSlug}-{attempt}";
        }

        return $"{baseSlug}-{DateTime.UtcNow.Ticks % 10000}";
    }
}
