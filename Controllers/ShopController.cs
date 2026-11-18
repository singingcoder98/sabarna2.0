using Sabarna.Web.Data;
using Sabarna.Web.Models;
using Sabarna.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Sabarna.Web.Controllers;

/// <summary>
/// The catalogue: everything, one product, or the combo boxes.
/// </summary>
public class ShopController : Controller
{
    private readonly AppDbContext _db;

    public ShopController(AppDbContext db)
    {
        _db = db;
    }

    // ---------------------------------------------------------------------------------------------
    // GET /Shop  and  GET /Shop?category=Pickles
    // ---------------------------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Index(string category)
    {
        var visible = _db.Products.AsNoTracking().Where(p => p.Active);

        var categories = await visible
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync();

        var filtered = visible;
        if (!string.IsNullOrWhiteSpace(category))
        {
            filtered = filtered.Where(p => p.Category == category);
        }

        var model = new ShopViewModel
        {
            Category = category ?? string.Empty,
            Categories = categories,
            TotalCount = await visible.CountAsync(),
            Products = await filtered
                .OrderByDescending(p => p.Featured)
                .ThenBy(p => p.Id)
                .ToListAsync()
        };

        // saptarshi: 16-Nov-2026 — each shelf gets the words people actually type: "daler bori",
        // "Bengali achar online", "Sundarban honey". One shelf, one keyword set, no stuffing.
        var (shelfTitle, shelfDescription) = SeoForShelf(category);
        ViewData["Title"] = shelfTitle;
        ViewData["Description"] = shelfDescription;

        return View(model);
    }

    // ---------------------------------------------------------------------------------------------
    // GET /Shop/Details/5
    // ---------------------------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var product = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.Active);

        if (product == null)
        {
            // Unknown or hidden product: show the friendly 404 page.
            return NotFound();
        }

        // saptarshi: 16-Nov-2026 — the numeric address still works, but it is not the page a search
        // engine should keep. Send the visitor (and the crawler) to the clean URL, permanently, so
        // the two addresses never compete with each other.
        if (!string.IsNullOrWhiteSpace(product.Slug))
        {
            return RedirectToActionPermanent(nameof(Product), new { slug = product.Slug });
        }

        return await RenderProductAsync(product);
    }

    // ---------------------------------------------------------------------------------------------
    // GET /product/kuler-achar-500g — saptarshi: 16-Nov-2026
    //
    // The address the shop publishes from now on: the keyword is in the URL, no numeric id, and it
    // survives a database rebuild. "p7" style links (a product saved a second ago, before its slug
    // was written) are corrected to the real slug with a 301 rather than shown twice.
    // ---------------------------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Product(string slug)
    {
        var product = await ResolveProductAsync(slug);

        if (product == null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(product.Slug)
            && !string.Equals(slug, product.Slug, StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToActionPermanent(nameof(Product), new { slug = product.Slug });
        }

        return await RenderProductAsync(product);
    }

    /// <summary>Finds a product by slug, or by the "p12" fallback used before a slug is written.</summary>
    private async Task<Product?> ResolveProductAsync(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        slug = slug.Trim();

        var bySlug = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Slug == slug && p.Active);

        if (bySlug != null)
        {
            return bySlug;
        }

        if (slug.Length > 1 && (slug[0] == 'p' || slug[0] == 'P') && int.TryParse(slug[1..], out var id))
        {
            return await _db.Products.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.Active);
        }

        return null;
    }

    /// <summary>Everything the product page needs, once the product is known.</summary>
    private async Task<IActionResult> RenderProductAsync(Product product)
    {
        var related = await _db.Products.AsNoTracking()
            .Where(p => p.Active && p.Id != product.Id && p.Category == product.Category)
            .OrderByDescending(p => p.Featured)
            .Take(3)
            .ToListAsync();

        // Not enough in the same category? Top up with anything else that is on sale.
        if (related.Count < 3)
        {
            var fillers = await _db.Products.AsNoTracking()
                .Where(p => p.Active && p.Id != product.Id && p.Category != product.Category)
                .OrderByDescending(p => p.Sold)
                .Take(3 - related.Count)
                .ToListAsync();

            related.AddRange(fillers);
        }

        var model = new ProductDetailsViewModel
        {
            Product = product,
            ComboItems = product.IsCombo
                ? CartService.GetComboItems(product)
                : new List<ComboItemDto>(),
            Related = related,
            Settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync() ?? new SiteSetting()
        };

        ViewData["Title"] = product.Name;
        ViewData["Description"] = BuildDescription(product);

        return View(model);
    }


    // ---------------------------------------------------------------------------------------------
    // GET /Shop/Combos
    // ---------------------------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Combos()
    {
        // "Featured first, then cheapest" — sorted in memory because SQLite cannot ORDER BY a
        // decimal column (it keeps decimals as TEXT).
        var combos = (await _db.Products.AsNoTracking()
                .Where(p => p.Active && p.IsCombo)
                .ToListAsync())
            .OrderByDescending(p => p.Featured)
            .ThenBy(p => p.Price)
            .ToList();

        ViewData["Title"] = "Special combos";
        ViewData["Description"] = "Two or three of our jars wrapped together in a cotton gift box, at a "
                                + "price lower than buying them separately.";
        return View(combos);
    }

    /// <summary>Meta description: the first ~155 characters of the product story.</summary>
    /// <summary>
    /// saptarshi: 16-Nov-2026 — title + description per shelf. Short enough that Google shows all of
    /// it, and written from the customer's side of the counter ("buy", "online", "Bengali").
    /// </summary>
    private static (string Title, string Description) SeoForShelf(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return ("Shop Bengali achar, bori & honey online",
                    "Every jar on the shelf: kuler achar, amsi, aam achar, daler bori and Sundarban honey. "
                    + "Prices, pack sizes and stock — made in Madhyamgram, Kolkata and shipped across India.");
        }

        return category.Trim().ToLowerInvariant() switch
        {
            "bori" => ("Daler bori — Bengali sun-dried bori",
                       "Handmade daler bori from a Madhyamgram kitchen: biuli, matar, moshla and musur dal bori, "
                       + "sun-dried, never fried, no preservatives. Buy Bengali bori online, 250 g packs."),
            "pickles" => ("Bengali achar — handmade pickles online",
                          "Kuler achar, amsi achar, spicy aam achar and mixed pickle — sun-cured in mustard oil, "
                          + "handmade without preservatives in Kolkata. 250 g and 500 g jars."),
            "honey" => ("Sundarban honey — raw forest honey",
                        "Sundarban honey from the mangrove forests of Bengal: raw, unheated, never blended. "
                        + "Pure forest honey bottled by hand in Kolkata, 100 g starter jar."),
            _ => ($"{category.Trim()} — buy online",
                  $"Our {category.Trim().ToLowerInvariant()} shelf: everything in stock today, with the price, the "
                  + "pack size and how much is left. Made in small batches in Madhyamgram, Kolkata.")
        };
    }

    private static string BuildDescription(Product product)
    {
        // saptarshi: 16-Nov-2026 — the meta description is written for the search result, not copied
        // from the long description (which is 80-120 words and would be cut in the middle).
        var copy = ProductContent.For(product);
        if (copy != null && !string.IsNullOrWhiteSpace(copy.MetaDescription))
        {
            return copy.MetaDescription;
        }

        var text = product.Description;
        if (string.IsNullOrWhiteSpace(text))
        {
            return $"{product.Name} — {product.Unit}, ₹{product.Price:0} from Sabarna.";
        }

        text = text.Trim();
        return text.Length <= 155 ? text : text.Substring(0, 152) + "…";
    }
}
