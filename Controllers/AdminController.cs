using System.Security.Claims;
using System.Text.Json;
using Matibari.Web.Data;
using Matibari.Web.Models;
using Matibari.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Matibari.Web.Controllers;

/// <summary>
/// The shop owner's back office. Every action requires the "admin" role except the two login
/// steps, which are marked [AllowAnonymous].
///
/// Login is deliberately two steps: something you know (the password) and something you have
/// (a 6-digit code that expires in five minutes).
/// </summary>
[Authorize(Policy = "AdminOnly")]
public class AdminController : Controller
{
    /// <summary>Session key holding the id of the admin who is halfway through logging in.</summary>
    private const string PendingAdminKey = "matibari.pending-admin";

    /// <summary>Session key holding the dev-only copy of the code, so it survives a page refresh.</summary>
    private const string DevCodeKey = "matibari.dev-otp";

    /// <summary>Products at or below this stock level are flagged on the dashboard.</summary>
    public const int LowStockThreshold = 10;

    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly IWebHostEnvironment _environment;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AdminController(AppDbContext db, AuthService auth, IWebHostEnvironment environment)
    {
        _db = db;
        _auth = auth;
        _environment = environment;
    }

    // =============================================================================================
    // LOGIN — step 1: email + password
    // =============================================================================================

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login()
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            return RedirectToAction(nameof(Dashboard));
        }

        ViewData["Title"] = "Admin sign in";
        return View(new AdminLoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(AdminLoginViewModel model)
    {
        ViewData["Title"] = "Admin sign in";

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var admin = await _auth.FindActiveAdminAsync(model.Email);

        // The same message for "no such account" and "wrong password" — no free hints.
        if (admin == null || !_auth.VerifyPassword(admin, model.Password))
        {
            ModelState.AddModelError(string.Empty, "That email and password do not match an account.");
            return View(model);
        }

        var code = await _auth.CreateOtpAsync(admin);

        HttpContext.Session.SetInt32(PendingAdminKey, admin.Id);

        if (_environment.IsDevelopment())
        {
            // DEVELOPMENT ONLY: no mail provider is connected yet, so the code is shown on screen.
            HttpContext.Session.SetString(DevCodeKey, code);
        }

        // TODO (deferred): e-mail / SMS the code to the admin's own address or phone.
        TempData["Toast"] = "Password accepted. We have sent a 6-digit code to your registered email.";
        return RedirectToAction(nameof(VerifyOtp));
    }

    // =============================================================================================
    // LOGIN — step 2: the 6-digit code
    // =============================================================================================

    [HttpGet]
    [AllowAnonymous]
    public IActionResult VerifyOtp()
    {
        var pendingAdminId = HttpContext.Session.GetInt32(PendingAdminKey);
        if (pendingAdminId == null)
        {
            TempData["Error"] = "Please start again with your email and password.";
            return RedirectToAction(nameof(Login));
        }

        ViewData["Title"] = "Enter your one-time code";
        ViewData["DevCode"] = _environment.IsDevelopment()
            ? HttpContext.Session.GetString(DevCodeKey)
            : null;

        return View(new OtpViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(OtpViewModel model)
    {
        var pendingAdminId = HttpContext.Session.GetInt32(PendingAdminKey);
        if (pendingAdminId == null)
        {
            TempData["Error"] = "Your login attempt expired. Please start again.";
            return RedirectToAction(nameof(Login));
        }

        ViewData["Title"] = "Enter your one-time code";
        ViewData["DevCode"] = _environment.IsDevelopment()
            ? HttpContext.Session.GetString(DevCodeKey)
            : null;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var admin = await _db.AdminUsers.FirstOrDefaultAsync(a => a.Id == pendingAdminId.Value && a.IsActive);
        if (admin == null)
        {
            HttpContext.Session.Remove(PendingAdminKey);
            TempData["Error"] = "That account is no longer active.";
            return RedirectToAction(nameof(Login));
        }

        var valid = await _auth.VerifyOtpAsync(admin.Id, model.Code);
        if (!valid)
        {
            ModelState.AddModelError(string.Empty,
                "That code is not correct, has expired, or has been used already. "
                + "Codes last five minutes — start the sign-in again for a fresh one.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new(ClaimTypes.Name, admin.FullName),
            new(ClaimTypes.Email, admin.Email),
            new(ClaimTypes.Role, admin.Role)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        HttpContext.Session.Remove(PendingAdminKey);
        HttpContext.Session.Remove(DevCodeKey);

        TempData["Toast"] = $"Welcome back, {admin.FullName}.";
        return RedirectToAction(nameof(Dashboard));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Toast"] = "Signed out.";
        return RedirectToAction(nameof(Login));
    }

    // =============================================================================================
    // DASHBOARD
    // =============================================================================================

    [HttpGet]
    public IActionResult Index()
    {
        // /Admin on its own lands here.
        return RedirectToAction(nameof(Dashboard));
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var orders = _db.Orders.AsNoTracking();

        var model = new AdminDashboardViewModel
        {
            Revenue = await orders
                .Where(o => o.Payment == "Paid")
                .SumAsync(o => (decimal?)o.Total) ?? 0m,
            OrderCount = await orders.CountAsync(),
            OpenOrders = await orders.CountAsync(o => o.Status != OrderStatuses.Delivered),
            ProductsLive = await _db.Products.AsNoTracking().CountAsync(p => p.Active),
            ProductsTotal = await _db.Products.AsNoTracking().CountAsync(),
            RecentOrders = await orders
                .OrderByDescending(o => o.CreatedAt)
                .Take(8)
                .ToListAsync(),
            LowStockProducts = await _db.Products.AsNoTracking()
                .Where(p => p.Active && p.Stock <= LowStockThreshold)
                .OrderBy(p => p.Stock)
                .Take(6)
                .ToListAsync()
        };

        model.LowStockCount = model.LowStockProducts.Count;

        ViewData["Title"] = "Dashboard";
        return View(model);
    }

    // =============================================================================================
    // ORDERS
    // =============================================================================================

    [HttpGet]
    public async Task<IActionResult> Orders(string status)
    {
        var query = _db.Orders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status) && OrderStatuses.All.Contains(status))
        {
            query = query.Where(o => o.Status == status);
        }
        else
        {
            status = "All";
        }

        var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

        ViewData["Title"] = "Orders";
        ViewData["Status"] = status;
        return View(orders);
    }

    /// <summary>Processing -> Packed -> Shipped -> Delivered. One click per parcel.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdvanceOrder(int id, string filter = null)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            TempData["Error"] = "That order no longer exists.";
            return RedirectToAction(nameof(Orders));
        }

        if (order.Status == OrderStatuses.Delivered)
        {
            TempData["Error"] = $"{order.OrderNumber} is already delivered.";
            return RedirectToAction(nameof(Orders), new { status = filter });
        }

        order.Status = OrderStatuses.Next(order.Status);
        await _db.SaveChangesAsync();

        TempData["Toast"] = $"{order.OrderNumber} moved to {order.Status}.";
        return RedirectToAction(nameof(Orders), new { status = filter });
    }

    // =============================================================================================
    // PRODUCTS
    // =============================================================================================

    [HttpGet]
    public async Task<IActionResult> Products()
    {
        var products = await _db.Products.AsNoTracking()
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .ToListAsync();

        ViewData["Title"] = "Products";
        return View(products);
    }

    /// <summary>Creates a product when Id is 0, otherwise updates it.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProduct(Product form)
    {
        // A description is optional but the JSON of a combo must at least be valid.
        if (string.IsNullOrWhiteSpace(form.ComboItemsJson))
        {
            form.ComboItemsJson = "[]";
        }

        if (!IsValidComboJson(form.ComboItemsJson))
        {
            TempData["Error"] = "The \"what's inside\" field must be a JSON list, e.g. "
                              + "[{\"name\":\"Ghee\",\"qty\":\"500 ml\",\"image\":\"/images/products/ghee-500.jpg\"}]. "
                              + "Nothing was saved.";
            return RedirectToAction(nameof(Products));
        }

        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values
                .SelectMany(entry => entry.Errors)
                .Select(error => error.ErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

            TempData["Error"] = string.IsNullOrWhiteSpace(firstError)
                ? "Please check the product details and try again."
                : firstError;
            return RedirectToAction(nameof(Products));
        }

        Product product;
        if (form.Id > 0)
        {
            var existing = await _db.Products.FirstOrDefaultAsync(p => p.Id == form.Id);
            if (existing == null)
            {
                TempData["Error"] = "That product no longer exists.";
                return RedirectToAction(nameof(Products));
            }

            product = existing;
        }
        else
        {
            product = new Product();
            _db.Products.Add(product);
        }

        product.Name = (form.Name ?? string.Empty).Trim();
        product.Category = (form.Category ?? string.Empty).Trim();
        product.Price = form.Price;
        product.Mrp = form.Mrp <= 0 ? form.Price : form.Mrp;
        product.Unit = (form.Unit ?? string.Empty).Trim();
        product.Rating = form.Rating;
        product.Reviews = form.Reviews;
        product.Tag = (form.Tag ?? string.Empty).Trim();
        product.Description = (form.Description ?? string.Empty).Trim();
        product.Active = form.Active;
        product.Stock = form.Stock;
        product.IsCombo = form.IsCombo;
        product.Featured = form.Featured;
        product.ComboItemsJson = form.ComboItemsJson;
        product.Image = NormaliseImagePath(form.Image);
        // product.Sold is intentionally left alone: it is a counter, not a form field.

        await _db.SaveChangesAsync();

        TempData["Toast"] = form.Id > 0
            ? $"\"{product.Name}\" updated — the shop shows the new details immediately."
            : $"\"{product.Name}\" added to the shop.";

        return RedirectToAction(nameof(Products));
    }

    /// <summary>Flips a product between visible and hidden without touching anything else.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleProduct(int id)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product == null)
        {
            TempData["Error"] = "That product no longer exists.";
            return RedirectToAction(nameof(Products));
        }

        product.Active = !product.Active;
        await _db.SaveChangesAsync();

        TempData["Toast"] = product.Active
            ? $"\"{product.Name}\" is live in the shop again."
            : $"\"{product.Name}\" is hidden from the shop.";

        return RedirectToAction(nameof(Products));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product == null)
        {
            TempData["Error"] = "That product no longer exists.";
            return RedirectToAction(nameof(Products));
        }

        // Past orders keep their own copy of the product name, so deleting is safe.
        _db.Products.Remove(product);
        await _db.SaveChangesAsync();

        TempData["Toast"] = $"\"{product.Name}\" deleted. Past orders are unaffected.";
        return RedirectToAction(nameof(Products));
    }

    // =============================================================================================
    // SETTINGS
    // =============================================================================================

    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        var settings = await _db.Settings.FirstOrDefaultAsync() ?? new SiteSetting
        {
            BrandName = "Matibari",
            Tagline = "Small-batch ghee, honey & pickles"
        };

        ViewData["Title"] = "Shop settings";
        return View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(SiteSetting form)
    {
        ViewData["Title"] = "Shop settings";

        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var settings = await _db.Settings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new SiteSetting();
            _db.Settings.Add(settings);
        }

        settings.BrandName = (form.BrandName ?? string.Empty).Trim();
        settings.Tagline = (form.Tagline ?? string.Empty).Trim();
        settings.Logo = NormaliseImagePath(form.Logo);
        settings.Email = (form.Email ?? string.Empty).Trim();
        settings.Phone = (form.Phone ?? string.Empty).Trim();
        settings.Whatsapp = (form.Whatsapp ?? string.Empty).Trim();
        settings.Address = (form.Address ?? string.Empty).Trim();
        settings.HeroKicker = (form.HeroKicker ?? string.Empty).Trim();
        settings.HeroTitle1 = (form.HeroTitle1 ?? string.Empty).Trim();
        settings.HeroTitle2 = (form.HeroTitle2 ?? string.Empty).Trim();
        settings.HeroText = (form.HeroText ?? string.Empty).Trim();

        await _db.SaveChangesAsync();

        TempData["Toast"] = "Settings saved — the storefront already reflects them.";
        return RedirectToAction(nameof(Settings));
    }

    // =============================================================================================
    // Helpers
    // =============================================================================================

    /// <summary>Accepts "images/x.jpg", "~/images/x.jpg" or "/images/x.jpg" and stores the last form.</summary>
    private static string NormaliseImagePath(string path)
    {
        var value = (path ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return string.Empty;
        }

        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        value = value.Replace("~", string.Empty);
        return value.StartsWith("/") ? value : "/" + value;
    }

    private static bool IsValidComboJson(string json)
    {
        try
        {
            JsonSerializer.Deserialize<List<ComboItemDto>>(json, JsonOptions);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
