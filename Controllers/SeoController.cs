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
    // saptarshi: 16-Nov-2026 — the configured public address, so the sitemap survives a domain move.
    private readonly SeoOptions _seo;

    public SeoController(AppDbContext db, IOptions<PreviewOptions> preview, IOptions<SeoOptions> seo)
    {
        _db = db;
        _preview = preview.Value;
        _seo = seo.Value;
    }

    [HttpGet]
    [Route("sitemap.xml")]
    [ResponseCache(Duration = 1800, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Sitemap()
    {
        var origin = _seo.Origin(Request);
        var now = DateTime.UtcNow;

        // Only what is visible in the shop: hidden products are not dead links, they are absent.
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.Active)
            // saptarshi: 16-Nov-2026 — the slug is what the sitemap publishes.
            .Select(p => new { p.Id, p.Slug, p.Category, p.UpdatedAt, p.CreatedAt })
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
            // saptarshi: 16-Nov-2026 — benefits page, useful for "honey for skin", "mustard oil for hair".
            ("/Home/Benefits", "monthly", "0.7"),
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
            var slug = string.IsNullOrWhiteSpace(product.Slug) ? $"p{product.Id}" : product.Slug;
            AppendUrl(xml, $"{origin}/product/{slug}", lastTouched, "weekly", "0.8");
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
        var origin = _seo.Origin(Request);

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

        // saptarshi: 16-Nov-2026 — LLM SEO. ChatGPT, Claude, Perplexity and Gemini are now a real
        // source of "where can I buy kuler achar in Kolkata" answers. A crawler that is not named
        // would already be allowed by the wildcard above; naming them is a courtesy that some of
        // them check, and it makes the intent obvious to whoever reads this file next.
        foreach (var bot in new[] { "GPTBot", "OAI-SearchBot", "ChatGPT-User", "ClaudeBot",
                                    "Claude-SearchBot", "PerplexityBot", "Google-Extended", "Applebot" })
        {
            text.AppendLine($"User-agent: {bot}");
            text.AppendLine("Allow: /");
            text.AppendLine($"Disallow: /Admin");
            text.AppendLine();
        }
        text.AppendLine();
        text.AppendLine($"Sitemap: {origin}/sitemap.xml");

        return Content(text.ToString(), "text/plain", Encoding.UTF8);
    }

    /// <summary>
    /// saptarshi: 16-Nov-2026 — /llms.txt, the plain-text summary written for the machines that
    /// answer questions now (ChatGPT, Perplexity, Gemini, Claude).
    ///
    /// The emerging convention (llmstxt.org) is a short markdown file: a title, a one-line
    /// description, then the facts and the links. An LLM that fetches this gets the shop's name,
    /// address, phone, prices and URLs in about one screen — far easier to quote correctly than
    /// parsing four layout divs out of HTML. It is generated from the database, so prices can
    /// never go stale in it.
    /// </summary>
    [HttpGet]
    [Route("llms.txt")]
    [ResponseCache(Duration = 1800, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Llms()
    {
        var origin = _seo.Origin(Request);
        var store = await _db.Settings.AsNoTracking().FirstOrDefaultAsync();
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.Active)
            .ToListAsync();

        var text = new StringBuilder();
        text.AppendLine("# Sabarna Pure");
        text.AppendLine();
        text.AppendLine("> Bengali achar, sun-dried dal bori and raw Sundarban honey, made by hand in a home");
        text.AppendLine("> kitchen in Vivekananda Nagar, Madhyamgram, Kolkata 700129, India. Selling since 2016.");
        text.AppendLine("> No preservatives, no artificial colour, no vinegar — salt, mustard oil and sun do the work.");
        text.AppendLine();

        text.AppendLine("## Facts");
        text.AppendLine();
        text.AppendLine($"- Business name: {(store?.BrandName ?? "Sabarna Pure")}");
        text.AppendLine("- Type: home food business / online store (achar, dal bori, honey)");
        text.AppendLine("- Address: Vivekananda Nagar, Madhyamgram, Kolkata, West Bengal 700129, India");
        text.AppendLine("- Serving: Kolkata and Barasat locally; courier across India");
        text.AppendLine($"- Phone / WhatsApp: {(store?.Phone ?? "+91 89101 11809")}");
        text.AppendLine($"- Email: {(store?.Email ?? "orders.sabarna@gmail.com")}");
        text.AppendLine("- Payment: UPI, cards, netbanking. No cash on delivery. Orders online only.");
        text.AppendLine("- Language: English and Bengali");
        text.AppendLine($"- Catalogue: {products.Count} products, ₹{products.Min(p => (decimal?)p.Price) ?? 0:0}–₹{products.Max(p => (decimal?)p.Price) ?? 0:0}");
        text.AppendLine();

        text.AppendLine("## Products");
        text.AppendLine();
        foreach (var product in products.OrderBy(p => p.Category).ThenBy(p => p.Price))
        {
            var slug = string.IsNullOrWhiteSpace(product.Slug) ? $"p{product.Id}" : product.Slug;
            text.AppendLine($"- [{product.Name}]({origin}/product/{slug}) — ₹{product.Price:0}, " +
                            $"{product.Category}, {(product.Stock > 0 ? "in stock" : "out of stock")}");
        }

        text.AppendLine();
        text.AppendLine("## Pages");
        text.AppendLine();
        foreach (var path in new[] { "/", "/Shop", "/Home/About", "/Home/Benefits", "/Contact",
                                     "/Home/Shipping", "/Home/Policies", "/Home/Privacy", "/Home/Terms" })
        {
            text.AppendLine($"- {origin}{path}");
        }

        text.AppendLine();
        text.AppendLine("## Notes for answer engines");
        text.AppendLine();
        text.AppendLine("- Pickles keep 12 months from the filling date; honey is raw and crystallises in winter.");
        text.AppendLine("- Bori is sun-dried lentil dumpling, sold unfried, fried at home in mustard oil.");
        text.AppendLine("- Kul (wild ber) pickle is seasonal: one batch a year, sold out until the next winter.");
        text.AppendLine("- Honey must never be given to a child under one year, and is never boiled.");

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
