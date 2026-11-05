using System.Net;
using System.Text;
using Sabarna.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sabarna.Web.Services;

namespace Sabarna.Web.Controllers;

/// <summary>
/// The two things search engines ask for and a hand-written file cannot keep up to date.
///
/// /sitemap.xml is generated from the database on every request, so a product the owner adds in
/// the admin panel is in the sitemap before the next crawl — no file to remember to regenerate.
/// The URLs use the host the request arrived on, so the same build produces the right sitemap on
/// localhost, on a staging domain and in production.
/// </summary>
public class SeoController : Controller
{
    private readonly AppDbContext _db;
    private readonly PreviewOptions _preview;

    public SeoController(AppDbContext db, IOptions<PreviewOptions> preview)
    {
        _db = db;
        _preview = preview.Value;
    }

    [HttpGet]
    [Route("sitemap.xml")]
    [ResponseCache(Duration = 1800, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Sitemap()
    {
        var origin = $"{Request.Scheme}://{Request.Host}";
        var now = DateTime.UtcNow;

        // Only what is visible in the shop: hidden products are not dead links, they are absent.
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.Active)
            .Select(p => new { p.Id, p.Category, p.UpdatedAt, p.CreatedAt })
            .ToListAsync();

        var categories = products
            .Select(p => p.Category)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category)
            .ToList();

        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        // ---- the pages that always exist -------------------------------------------------------
        var pages = new (string Path, string ChangeFrequency, string Priority)[]
        {
            ("/", "weekly", "1.0"),
            ("/Shop", "daily", "0.9"),
            ("/Shop/Combos", "weekly", "0.8"),
            ("/Home/About", "monthly", "0.7"),
            ("/Contact", "monthly", "0.6"),
            ("/Home/Shipping", "monthly", "0.5"),
            ("/Home/Policies", "yearly", "0.4"),
            ("/Home/Privacy", "yearly", "0.3"),
            ("/Home/Terms", "yearly", "0.3")
        };

        foreach (var page in pages)
        {
            AppendUrl(xml, origin + page.Path, now, page.ChangeFrequency, page.Priority);
        }

        // ---- one page per category, because /Shop?category=… is a real landing page -------------
        foreach (var category in categories)
        {
            AppendUrl(xml, $"{origin}/Shop?category={Uri.EscapeDataString(category)}", now, "weekly", "0.7");
        }

        // ---- one page per visible jar -----------------------------------------------------------
        foreach (var product in products)
        {
            var lastTouched = product.UpdatedAt == default ? product.CreatedAt : product.UpdatedAt;
            AppendUrl(xml, $"{origin}/Shop/Details/{product.Id}", lastTouched, "weekly", "0.8");
        }

        xml.AppendLine("</urlset>");

        return Content(xml.ToString(), "application/xml", Encoding.UTF8);
    }

    /// <summary>
    /// /robots.txt, generated so the Sitemap line always names the host the file was served from.
    /// The alternative — a static file with a hard-coded domain — is the thing everybody forgets
    /// to edit on launch day, and a wrong Sitemap line is silently ignored by every crawler.
    /// </summary>
    [HttpGet]
    [Route("robots.txt")]
    [ResponseCache(Duration = 1800, Location = ResponseCacheLocation.Any)]
    public IActionResult Robots()
    {
        var origin = $"{Request.Scheme}://{Request.Host}";

        var text = new StringBuilder();

        // A preview shop hands out a link to a handful of people. Until it opens, crawlers are
        // told to stay away entirely — otherwise the temporary address ends up in a search index
        // and competes with the real domain later.
        if (_preview.Enabled)
        {
            text.AppendLine("# Sabarna — robots.txt");
            text.AppendLine("# Private preview: this shop is not launched yet and must not be indexed.");
            text.AppendLine();
            text.AppendLine("User-agent: *");
            text.AppendLine("Disallow: /");
            return Content(text.ToString(), "text/plain", Encoding.UTF8);
        }

        text.AppendLine("# Sabarna — robots.txt");
        text.AppendLine("# Everything public may be crawled. The admin panel, the basket and the error page may not.");
        text.AppendLine();
        text.AppendLine("User-agent: *");
        text.AppendLine("Allow: /");
        text.AppendLine();
        text.AppendLine("Disallow: /Admin");
        text.AppendLine("Disallow: /Cart");
        text.AppendLine("Disallow: /Home/Error");
        text.AppendLine();
        text.AppendLine($"Sitemap: {origin}/sitemap.xml");

        return Content(text.ToString(), "text/plain", Encoding.UTF8);
    }

    /// <summary>One &lt;url&gt; block, with the values escaped.</summary>
    private static void AppendUrl(StringBuilder xml, string location, DateTime lastModified,
                                  string changeFrequency, string priority)
    {
        var stamp = lastModified == default ? DateTime.UtcNow : lastModified;

        xml.AppendLine("  <url>");
        xml.AppendLine($"    <loc>{WebUtility.HtmlEncode(location)}</loc>");
        xml.AppendLine($"    <lastmod>{stamp:yyyy-MM-dd}</lastmod>");
        xml.AppendLine($"    <changefreq>{changeFrequency}</changefreq>");
        xml.AppendLine($"    <priority>{priority}</priority>");
        xml.AppendLine("  </url>");
    }
}
