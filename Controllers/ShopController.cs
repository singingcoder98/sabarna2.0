using Matibari.Web.Data;
using Matibari.Web.Models;
using Matibari.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Matibari.Web.Controllers;

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

        ViewData["Title"] = string.IsNullOrWhiteSpace(category)
            ? "Shop everything"
            : $"{category}";
        ViewData["Description"] = "Every jar we currently have on the shelf — ghee, honey, pickles, oils and "
                                + "spices, with the price, the jar size and how much stock is left.";

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
    private static string BuildDescription(Product product)
    {
        var text = product.Description;
        if (string.IsNullOrWhiteSpace(text))
        {
            return $"{product.Name} — {product.Unit}, ₹{product.Price:0} from Matibari.";
        }

        text = text.Trim();
        return text.Length <= 155 ? text : text.Substring(0, 152) + "…";
    }
}
