using Sabarna.Web.Data;
using Sabarna.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Sabarna.Web.Controllers;

/// <summary>
/// The public "shop front" pages: home, story, the four policy pages and the error page.
/// </summary>
public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db)
    {
        _db = db;
    }

    // ---------------------------------------------------------------------------------------------
    // GET /
    // ---------------------------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Description"] = "Small-batch A2 bilona ghee, raw forest honey, sun-cured pickles and "
                                + "cold-pressed oils from a family farm in Midnapore, West Bengal. Shipped across India.";

        // The hero backdrop is a CSS background image, so nothing tells the browser about it until
        // site.css has been parsed. Preloading it here is what keeps the first paint quick.
        ViewData["PreloadImage"] = "/images/hero-farm.jpg";

        var visible = _db.Products.AsNoTracking().Where(p => p.Active);

        // SQLite stores decimal as TEXT and refuses to ORDER BY it, so the combos are read first
        // and sorted in memory. There are only a handful of them, so this costs nothing.
        var comboRows = await visible
            .Where(p => p.IsCombo)
            .ToListAsync();

        // ---- the slow-motion band: one jar per shelf, then best sellers if a shelf is empty ----
        var singleJars = await visible
            .Where(p => !p.IsCombo)
            .ToListAsync();

        var motion = new List<Product>();
        foreach (var shelf in new[] { "jaggery", "ghee", "honey" })
        {
            var pick = singleJars.FirstOrDefault(p =>
                p.Category.Contains(shelf, StringComparison.OrdinalIgnoreCase) &&
                motion.All(taken => taken.Id != p.Id));

            if (pick != null)
            {
                motion.Add(pick);
            }
        }

        foreach (var product in singleJars.OrderByDescending(p => p.Sold))
        {
            if (motion.Count == 3)
            {
                break;
            }

            if (motion.All(taken => taken.Id != product.Id))
            {
                motion.Add(product);
            }
        }

        var model = new HomeViewModel
        {
            Settings = await GetSettingsAsync(),
            Products = await visible
                .Where(p => !p.IsCombo)
                .OrderByDescending(p => p.Featured)
                .ThenBy(p => p.Id)
                .Take(8)
                .ToListAsync(),
            Combos = comboRows
                .OrderBy(p => p.Price)
                .ToList(),
            Featured = await visible
                .Where(p => p.Featured)
                .OrderBy(p => p.Id)
                .Take(3)
                .ToListAsync(),
            Motion = motion,
            OrdersDelivered = await _db.Orders.AsNoTracking()
                .CountAsync(o => o.Status == OrderStatuses.Delivered),
            ProductsOnline = await visible.CountAsync()
        };

        return View(model);
    }

    // ---------------------------------------------------------------------------------------------
    // saptarshi: 14-Nov-2026 — the newsletter POST is switched off together with its form.
    //
    // It only ever echoed "Thank you, we will write to you" and saved nothing: no table, no mail, no
    // list. The home page now offers a WhatsApp button instead, which the owner actually sees.
    // Kept here (commented, not deleted) so the wording and the validation can be reused the day a
    // real subscriber list is built.
    // ---------------------------------------------------------------------------------------------
    /*
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Index([FromForm] string email)
    {
        if (string.IsNullOrWhiteSpace(email)
            || !email.Contains('@')
            || !email.Contains('.')
            || email.Length > 160)
        {
            TempData["Error"] = "That email address does not look right. Please check and try again.";
            return Redirect(Url.Content("~/") + "#newsletter");
        }

        // TODO (deferred): hand the address to the mailing-list provider / send a welcome mail.
        TempData["Toast"] = $"Thank you — we will write to {email.Trim()} when the next batch is ready.";
        return Redirect(Url.Content("~/") + "#newsletter");
    }
    */

    // ---------------------------------------------------------------------------------------------
    // GET /Home/About
    // ---------------------------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> About()
    {
        ViewData["Title"] = "Our story";
        ViewData["Description"] = "Eleven people, four cows, one wood fire and a rooftop full of drying "
                                + "mangoes — how Sabarna makes its ghee, honey and pickles.";
        return View(await GetSettingsAsync());
    }

    [HttpGet]
    public IActionResult Policies()
    {
        ViewData["Title"] = "Our policies";
        ViewData["Description"] = "Shipping, refunds, privacy and terms — written in plain language.";
        return View();
    }

    [HttpGet]
    public IActionResult Privacy()
    {
        ViewData["Title"] = "Privacy policy";
        ViewData["Description"] = "Exactly what we collect when you order, why we collect it, and what we never do with it.";
        return View();
    }

    [HttpGet]
    public IActionResult Terms()
    {
        ViewData["Title"] = "Terms & conditions";
        ViewData["Description"] = "The plain-language terms that apply when you order from Sabarna.";
        return View();
    }

    [HttpGet]
    public IActionResult Shipping()
    {
        ViewData["Title"] = "Shipping, refunds & returns";
        ViewData["Description"] = "Delivery timelines, courier partners, damaged-jar policy and how refunds are paid back.";
        return View();
    }

    // ---------------------------------------------------------------------------------------------
    // GET /Home/Error — also used by UseStatusCodePagesWithReExecute for 404 / 403 / 500.
    // ---------------------------------------------------------------------------------------------
    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode)
    {
        // Error pages carry a 404/500 status, but say so explicitly as well.
        ViewData["Robots"] = "noindex, follow";
        ViewData["Title"] = "Something went wrong";
        ViewData["Description"] = "A friendly explanation of what happened and where to go next.";
        ViewData["StatusCode"] = statusCode ?? 500;
        return View();
    }

    /// <summary>Reads the single settings row (with a safe fallback so a page never renders blank).</summary>
    private async Task<SiteSetting> GetSettingsAsync()
    {
        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync();
        return settings ?? new SiteSetting
        {
            BrandName = "Sabarna Pure",
            Tagline = "Small-batch ghee, honey & pickles",
            Email = "orders.sabarna@gmail.com",   // saptarshi: 13-Nov-2026
            Phone = "+91 89101 11809",
            Whatsapp = "918910111809",
            Address = "Sabarna Farm, Village Sabarna, P.O. Kharagpur, West Bengal 721301",
            HeroKicker = "Farm to home",
            HeroTitle1 = "Ghee that still",
            HeroTitle2 = "smells of the farm",
            HeroText = "Slow-churned ghee, raw forest honey and sun-cured pickles from a family farm in Bengal."
        };
    }
}
