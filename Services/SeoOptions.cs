using Microsoft.AspNetCore.Http;

namespace Sabarna.Web.Services;

/// <summary>
/// saptarshi: 16-Nov-2026 — one place that knows what the shop's public address is.
///
/// Until now every absolute URL (canonical, og:url, sitemap, JSON-LD) was built from the request's
/// own Host header. That works until the domain changes — and the domain is about to change twice
/// (runasp.net → own domain, http → https), and every canonical Google has already stored would
/// have pointed at the old one.
///
/// Now the address lives in appsettings.json:
///
///     "Seo": { "BaseUrl": "https://sabarna.in" }
///
/// Leave it empty and the shop falls back to the host the request arrived on, which is what makes
/// localhost and the staging address keep working with the same build.
/// </summary>
public class SeoOptions
{
    /// <summary>The public address of the shop, with no trailing slash. Empty = use the request host.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Shown after a page title: "Amsi Pickle — 500 g · Sabarna Pure".</summary>
    public string TitleSuffix { get; set; } = "Sabarna Pure";

    /// <summary>Google shows roughly 60 characters of a title and 155 of a description.</summary>
    public int MaxTitleLength { get; set; } = 60;

    public int MaxDescriptionLength { get; set; } = 155;

    /// <summary>"https://sabarna.in" if configured, otherwise whatever host the request came in on.</summary>
    public string Origin(HttpRequest request)
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl))
        {
            return BaseUrl.Trim().TrimEnd('/');
        }

        return $"{request.Scheme}://{request.Host}";
    }

    /// <summary>An absolute URL for a site-relative path ("/product/kuler-achar-500g").</summary>
    public string Absolute(HttpRequest request, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Origin(request);
        }

        return path.StartsWith('/') ? Origin(request) + path : $"{Origin(request)}/{path}";
    }
}
