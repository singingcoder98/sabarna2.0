using Matibari.Web.Data;
using Matibari.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Matibari.Web.Controllers;

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

        var visible = _db.Products.AsNoTracking().Where(p => p.Active);

        var model = new HomeViewModel
        {
            Settings = await GetSettingsAsync(),
            Products = await visible
                .Where(p => !p.IsCombo)
                .OrderByDescending(p => p.Featured)
                .ThenBy(p => p.Id)
                .Take(8)
                .ToListAsync(),
            Combos = await visible
                .Where(p => p.IsCombo)
                .OrderBy(p => p.Price)
                .ToListAsync(),
            Featured = await visible
                .Where(p => p.Featured)
                .OrderBy(p => p.Id)
                .Take(3)
                .ToListAsync(),
            OrdersDelivered = await _db.Orders.AsNoTracking()
                .CountAsync(o => o.Status == OrderStatuses.Delivered),
            ProductsOnline = await visible.CountAsync()
        };

        return View(model);
    }

    // ---------------------------------------------------------------------------------------------
    // POST / — the newsletter form at the bottom of the home page.
    // The address is echoed back in a toast today; connecting a mailing list is a TODO (see README).
    // ---------------------------------------------------------------------------------------------
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

    // ---------------------------------------------------------------------------------------------
    // GET /Home/About
    // ---------------------------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> About()
    {
        ViewData["Title"] = "Our story";
        ViewData["Description"] = "Eleven people, four cows, one wood fire and a rooftop full of drying "
                                + "mangoes — how Matibari makes its ghee, honey and pickles.";
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
        ViewData["Description"] = "The plain-language terms that apply when you order from Matibari.";
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
            BrandName = "Matibari",
            Tagline = "Small-batch ghee, honey & pickles",
            Email = "hello@matibari.in",
            Phone = "+91 98000 12345",
            Whatsapp = "919800012345",
            Address = "Matibari Farm, Village Matibari, P.O. Kharagpur, West Bengal 721301",
            HeroKicker = "Farm to home",
            HeroTitle1 = "Ghee that still",
            HeroTitle2 = "smells of the farm",
            HeroText = "Slow-churned ghee, raw forest honey and sun-cured pickles from a family farm in Bengal."
        };
    }
}
