using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;   // saptarshi: login attempt counter
using System.Text;
using System.Text.Json;
using Sabarna.Web.Data;
using Sabarna.Web.Models;
using Sabarna.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Sabarna.Web.Controllers;

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
    private const string PendingAdminKey = "sabarna.pending-admin";

    /// <summary>Session key holding the dev-only copy of the code, so it survives a page refresh.</summary>
    private const string DevCodeKey = "sabarna.dev-otp";

    /// <summary>Products at or below this stock level are flagged on the dashboard.</summary>
    public const int LowStockThreshold = 10;

    /// <summary>Session key holding when the last code was sent, so "Resend" cannot be hammered.</summary>
    private const string LastOtpSentKey = "sabarna.otp-sent-at";   // saptarshi

    /// <summary>
    /// saptarshi: Phase 0 (12-Nov-2026) — carried inside the auth cookie. "1" means this account is
    /// still on the seeded password, and the panel will not open anything except the password form.
    /// </summary>
    private const string MustChangePasswordClaim = "sabarna.must-change-password";

    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly EmailService _email;
    private readonly BackupService _backups;
    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminController> _logger;   // saptarshi

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AdminController(
        AppDbContext db,
        AuthService auth,
        EmailService email,          // saptarshi
        BackupService backups,       // saptarshi
        IMemoryCache cache,          // saptarshi
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<AdminController> logger)
    {
        _db = db;
        _auth = auth;
        _email = email;
        _backups = backups;
        _cache = cache;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// True when the one-time code may be shown on screen. No mail provider is connected yet, so
    /// on a fresh host this is the only way in — it is Development by default, and can be switched
    /// on for a hosted preview with Otp:ShowOnScreen = true. Turn it off the day mail is wired up.
    /// </summary>
    private bool ShowOtpOnScreen =>
        _configuration.GetValue<bool?>("Otp:ShowOnScreen") ?? _environment.IsDevelopment();

    // =============================================================================================
    // saptarshi: added 12-Nov-2026 — the code is now e-mailed (Task 2).
    //
    // Three rules decide where a code goes, in this order:
    //
    //   1. Otp:ShowOnScreen is on (or the site is in Development)  -> straight on the screen.
    //      That is how this PC works, and how the hosted preview worked until today.
    //   2. Otherwise it goes out by e-mail: owner address first, then the backup address, then the
    //      admin's own address (Services/EmailService.cs).
    //   3. If no mail is configured, or the mail server refuses, the code falls back to the screen.
    //
    // Rule 3 exists on purpose. A shop whose owner cannot get in because a password expired is a
    // worse day than a code being visible for five minutes. The moment Email__Enabled is turned on
    // and the first e-mail lands, the screen copy disappears by itself — no setting to remember.
    // =============================================================================================
    private async Task<bool> DeliverOtpAsync(AdminUser admin, string code)
    {
        HttpContext.Session.SetString(LastOtpSentKey, DateTime.UtcNow.ToString("O"));

        if (ShowOtpOnScreen)
        {
            HttpContext.Session.SetString(DevCodeKey, code);
            return true;
        }

        var emailed = await _email.SendOtpAsync(code, admin.Email);

        if (emailed)
        {
            HttpContext.Session.Remove(DevCodeKey);
            return true;
        }

        // saptarshi: Phase 0 (12-Nov-2026) — the live site never draws a sign-in code on the screen.
        // A code on screen turns two-factor sign-in into one-factor for anybody who has the password,
        // so a mail failure is reported as a failure. The escape hatch is the explicit setting
        // Otp:ShowOnScreen=true, which is a deliberate decision by the owner, not an accident.
        if (!_environment.IsProduction())
        {
            HttpContext.Session.SetString(DevCodeKey, code);
        }
        else
        {
            HttpContext.Session.Remove(DevCodeKey);
        }

        return false;
    }

    // =============================================================================================
    // saptarshi: added 12-Nov-2026 — a ceiling on wrong passwords, counted per address + device.
    // Eight wrong tries in a quarter of an hour and that pair waits; the real owner on another
    // phone, or fifteen minutes later, is unaffected. Nothing is locked permanently, so there is
    // no "recovery" step to remember.
    // =============================================================================================
    private const int MaxLoginFailures = 8;
    private static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(15);

    private string LoginAttemptKey(string email)
    {
        var device = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return "sabarna.login-fail|" + device + "|" + (email ?? string.Empty).Trim().ToLowerInvariant();
    }

    private bool IsLoginBlocked(string email)
    {
        return _cache.TryGetValue(LoginAttemptKey(email), out int failures) && failures >= MaxLoginFailures;
    }

    private void RecordLoginFailure(string email)
    {
        var key = LoginAttemptKey(email);
        var failures = _cache.TryGetValue(key, out int current) ? current : 0;
        _cache.Set(key, failures + 1, LoginWindow);
    }

    private void ClearLoginFailures(string email)
    {
        _cache.Remove(LoginAttemptKey(email));
    }

    /// <summary>a***@gmail.com — enough for the owner to recognise the inbox, useless to a snooper.</summary>
    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "your registered email";
        }

        var at = email.IndexOf('@');
        return at <= 1 ? email : email[0] + "***" + email[(at - 1)..];
    }

    // =============================================================================================
    // saptarshi: Phase 0 (12-Nov-2026) — "you are still using the password from the README"
    //
    // Nothing in the panel opens until a new password is set. Everything the owner needs to get out
    // of that state (the Settings page with its password form, the change action itself, and signing
    // out) is allowed through, so this can never become a lock-out.
    // =============================================================================================
    /// <summary>
    /// saptarshi: Phase 0 — an admin who was already signed in when this update was deployed carries
    /// an older cookie that does not know about the flag. Rather than guess, they are signed out once
    /// and asked to sign in again, which stamps the answer into the new cookie.
    /// </summary>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated == true
            && context.HttpContext.User.FindFirst(MustChangePasswordClaim) == null)
        {
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Result = new RedirectToActionResult(nameof(Login), "Admin", null);
            return;
        }

        await base.OnActionExecutionAsync(context, next);
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        base.OnActionExecuting(context);

        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (context.HttpContext.User.FindFirst(MustChangePasswordClaim)?.Value != "1")
        {
            return;
        }

        var action = context.RouteData.Values["action"] as string ?? string.Empty;

        if (action is nameof(Settings) or nameof(ChangePassword) or nameof(Logout))
        {
            return;
        }

        TempData["Error"] = "The shop is still using the password it was set up with. "
                          + "Please choose a new one below, then the whole panel opens.";

        context.Result = new RedirectToActionResult(nameof(Settings), "Admin", null);
    }

    /// <summary>
    /// saptarshi: Phase 0 — one place that builds the cookie, so the "change your password" mark can
    /// never be left out of a fresh sign-in.
    /// </summary>
    private async Task SignInAdminAsync(AdminUser admin)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new(ClaimTypes.Name, admin.FullName),
            new(ClaimTypes.Email, admin.Email),
            new(ClaimTypes.Role, admin.Role),
            new(MustChangePasswordClaim, admin.MustChangePassword ? "1" : "0")
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

        // saptarshi: added 12-Nov-2026 — too many wrong tries from this device: stop guessing here.
        if (IsLoginBlocked(model.Email))
        {
            ModelState.AddModelError(string.Empty,
                "Too many wrong attempts from this device. Please wait fifteen minutes and try again.");
            return View(model);
        }

        var admin = await _auth.FindActiveAdminAsync(model.Email);

        // The same message for "no such account" and "wrong password" — no free hints.
        if (admin == null || !_auth.VerifyPassword(admin, model.Password))
        {
            RecordLoginFailure(model.Email);   // saptarshi
            ModelState.AddModelError(string.Empty, "That email and password do not match an account.");
            return View(model);
        }

        ClearLoginFailures(model.Email);       // saptarshi

        var code = await _auth.CreateOtpAsync(admin);

        HttpContext.Session.SetInt32(PendingAdminKey, admin.Id);

        // saptarshi: e-mail it, or fall back to the screen if no mail is connected (see DeliverOtpAsync).
        var delivered = await DeliverOtpAsync(admin, code);

        // saptarshi: Phase 0 — "could not send" is now an error, not a friendly note, because on the
        // live site there is nothing to fall back to by design.
        if (delivered)
        {
            TempData["Toast"] = $"Password accepted. A 6-digit code is on its way to {MaskEmail(admin.Email)} — it expires in 5 minutes.";
        }
        else if (ShowOtpOnScreen)
        {
            TempData["Toast"] = "Password accepted. Your code is on the next screen.";
        }
        else
        {
            TempData["Error"] = "Password accepted, but we could not e-mail your code. "
                              + "Check the Email__ settings on the server (or the backup inbox), then start again.";
        }

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

        // saptarshi: the session only holds a code when the screen is the delivery method, so this
        // one line covers both the "screen" setting and the "email failed, fell back" case.
        ViewData["DevCode"] = HttpContext.Session.GetString(DevCodeKey);

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
        ViewData["DevCode"] = ShowOtpOnScreen
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

        // saptarshi: Phase 0 — the helper also records whether the seeded password is still in use.
        await SignInAdminAsync(admin);

        HttpContext.Session.Remove(PendingAdminKey);
        HttpContext.Session.Remove(DevCodeKey);

        TempData["Toast"] = $"Welcome back, {admin.FullName}.";
        return RedirectToAction(nameof(Dashboard));
    }

    // ---------------------------------------------------------------------------------------------
    // saptarshi: added 12-Nov-2026 — POST /Admin/ResendOtp
    //
    // "The code never came." One new code, one old code burnt, and a 60-second wait between asks so
    // the button cannot be used to flood a mailbox or to fish for a code that happens to match.
    // ---------------------------------------------------------------------------------------------
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp()
    {
        var pendingAdminId = HttpContext.Session.GetInt32(PendingAdminKey);
        if (pendingAdminId == null)
        {
            TempData["Error"] = "Please start again with your email and password.";
            return RedirectToAction(nameof(Login));
        }

        var lastSent = HttpContext.Session.GetString(LastOtpSentKey);
        if (!string.IsNullOrWhiteSpace(lastSent)
            && DateTime.TryParse(lastSent, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var sentAt)
            && DateTime.UtcNow - sentAt < TimeSpan.FromSeconds(60))
        {
            TempData["Error"] = "A code was sent less than a minute ago. Check your inbox (and the spam folder), then try again.";
            return RedirectToAction(nameof(VerifyOtp));
        }

        var admin = await _db.AdminUsers.FirstOrDefaultAsync(a => a.Id == pendingAdminId.Value && a.IsActive);
        if (admin == null)
        {
            HttpContext.Session.Remove(PendingAdminKey);
            TempData["Error"] = "That account is no longer active.";
            return RedirectToAction(nameof(Login));
        }

        var code = await _auth.CreateOtpAsync(admin);

        var delivered = await DeliverOtpAsync(admin, code);

        if (delivered)
        {
            TempData["Toast"] = $"A fresh code is on its way to {MaskEmail(admin.Email)}. The earlier one no longer works.";
        }
        else if (ShowOtpOnScreen)
        {
            TempData["Toast"] = "A fresh code is shown below. The earlier one no longer works.";
        }
        else
        {
            TempData["Error"] = "We could not e-mail the new code. Please check the Email__ settings on the server.";
        }

        return RedirectToAction(nameof(VerifyOtp));
    }

    // =============================================================================================
    // saptarshi: Phase 0 (12-Nov-2026) — database backups (task 4 of the audit).
    //
    // The whole shop is one SQLite file. A copy is taken automatically (BackupHostedService) and the
    // owner can take one now and download any of them here. Only the admin sees this page, and only
    // file names this application produced are ever opened.
    // =============================================================================================
    [HttpGet]
    public IActionResult Backups()
    {
        ViewData["Title"] = "Backups";
        return View(_backups.ListBackups());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BackupNow()
    {
        try
        {
            var name = await _backups.CreateBackupAsync();
            TempData["Toast"] = $"Backup written: {name}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual database backup failed.");
            TempData["Error"] = "The backup could not be written. The site is unaffected — try again, "
                              + "and tell your host if it keeps failing.";
        }

        return RedirectToAction(nameof(Backups));
    }

    [HttpGet]
    public IActionResult DownloadBackup(string name)
    {
        var path = _backups.ResolvePath(name);

        if (path == null)
        {
            TempData["Error"] = "That backup is not on the server.";
            return RedirectToAction(nameof(Backups));
        }

        return PhysicalFile(path, "application/octet-stream", name);
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
        // Six small columns of the order book, aggregated in memory. SQLite cannot SUM or ORDER BY
        // a decimal column, and a farm this size has nothing to gain from pushing the maths into SQL.
        var facts = await _db.Orders.AsNoTracking()
            .Select(o => new { o.CreatedAt, o.Total, o.Payment, o.Status, o.ProductName, o.ItemCount })
            .ToListAsync();

        var paid = facts.Where(f => f.Payment == "Paid").ToList();
        var revenue = paid.Sum(f => f.Total);
        var today = DateTime.UtcNow.Date;

        // ---- seven days of takings, oldest first, with bar heights scaled to the best day
        var week = new List<AdminDaySales>();
        for (var offset = 6; offset >= 0; offset--)
        {
            var day = today.AddDays(-offset);
            week.Add(new AdminDaySales
            {
                Day = day,
                Revenue = paid.Where(f => f.CreatedAt.Date == day).Sum(f => f.Total),
                Orders = facts.Count(f => f.CreatedAt.Date == day),
                IsToday = offset == 0
            });
        }

        var bestDay = week.Max(d => d.Revenue);
        foreach (var day in week)
        {
            // A floor of 3% keeps quiet days visible as a stub rather than an empty gap.
            day.Height = bestDay <= 0 ? 3 : Math.Max(3, (int)Math.Round((double)(day.Revenue / bestDay) * 100));
        }

        // ---- what actually sells, by revenue
        var top = paid
            .Where(f => !string.IsNullOrWhiteSpace(f.ProductName))
            .GroupBy(f => f.ProductName)
            .Select(group => new AdminTopProduct
            {
                Name = group.Key,
                Jars = group.Sum(f => f.ItemCount),
                Revenue = group.Sum(f => f.Total)
            })
            .OrderByDescending(row => row.Revenue)
            .Take(5)
            .ToList();

        var bestSeller = top.Count == 0 ? 0m : top[0].Revenue;
        foreach (var row in top)
        {
            row.Share = bestSeller <= 0 ? 0 : Math.Max(6, (int)Math.Round((double)(row.Revenue / bestSeller) * 100));
        }

        var lowStock = await _db.Products.AsNoTracking()
            .Where(p => p.Active && p.Stock <= LowStockThreshold)
            .OrderBy(p => p.Stock)
            .Take(6)
            .ToListAsync();

        var model = new AdminDashboardViewModel
        {
            Revenue = revenue,
            PaidOrderCount = paid.Count,
            AverageOrderValue = paid.Count == 0 ? 0m : revenue / paid.Count,
            OrderCount = facts.Count,
            OpenOrders = facts.Count(f => f.Status != OrderStatuses.Delivered),
            OpenValue = facts.Where(f => f.Status != OrderStatuses.Delivered).Sum(f => f.Total),
            TodayOrderCount = facts.Count(f => f.CreatedAt.Date == today),
            TodayRevenue = paid.Where(f => f.CreatedAt.Date == today).Sum(f => f.Total),
            ProductsLive = await _db.Products.AsNoTracking().CountAsync(p => p.Active),
            ProductsTotal = await _db.Products.AsNoTracking().CountAsync(),
            // Counted separately from the list above, which is capped at six rows.
            LowStockCount = await _db.Products.AsNoTracking()
                .CountAsync(p => p.Active && p.Stock <= LowStockThreshold),
            LowStockProducts = lowStock,
            SalesLast7Days = week,
            TopProducts = top,
            RecentOrders = await _db.Orders.AsNoTracking()
                .OrderByDescending(o => o.CreatedAt)
                .Take(8)
                .ToListAsync()
        };

        ViewData["Title"] = "Dashboard";
        return View(model);
    }

    // =============================================================================================
    // ORDERS
    // =============================================================================================

    [HttpGet]
    public async Task<IActionResult> Orders(string status, string search)
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

        // Search runs in SQL (LIKE), so the filter pills and the search box can be combined.
        var term = (search ?? string.Empty).Trim();
        if (term.Length > 0)
        {
            query = query.Where(o =>
                o.OrderNumber.Contains(term) || o.Customer.Contains(term) || o.Email.Contains(term));
        }

        var rows = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

        // "How many in each state" is one grouped query, not four counts.
        var grouped = await _db.Orders.AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var counts = new Dictionary<string, int> { ["All"] = grouped.Sum(g => g.Count) };
        foreach (var name in OrderStatuses.All)
        {
            counts[name] = grouped.FirstOrDefault(g => g.Status == name)?.Count ?? 0;
        }

        var model = new AdminOrdersViewModel
        {
            Orders = rows,
            Status = status,
            Search = term,
            FilteredCount = rows.Count,
            FilteredValue = rows.Sum(o => o.Total),
            TotalCount = counts["All"],
            CountsByStatus = counts
        };

        ViewData["Title"] = "Orders";
        return View(model);
    }

    // =============================================================================================
    // saptarshi: added 06-Nov-2026 — the packing sheet for one order.
    //
    // WHY: until now an order carried only a name and an email. The address and phone the customer
    // typed were thrown away, and the jars were a single summary line, so a parcel could not be
    // packed or queried. This page shows everything, plus how the payment was confirmed.
    // =============================================================================================
    [HttpGet]
    public async Task<IActionResult> OrderDetails(string orderNumber)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

        if (order == null)
        {
            TempData["Error"] = "No order with that number.";
            return RedirectToAction(nameof(Orders));
        }

        ViewData["Title"] = "Order " + order.OrderNumber;
        return View(order);
    }

    /// <summary>Processing -> Packed -> Shipped -> Delivered. One click per parcel.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdvanceOrder(int id, string filter = null, string search = null)
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
            return RedirectToAction(nameof(Orders), new { status = filter, search });
        }

        order.Status = OrderStatuses.Next(order.Status);
        await _db.SaveChangesAsync();

        TempData["Toast"] = $"{order.OrderNumber} moved to {order.Status}.";
        return RedirectToAction(nameof(Orders), new { status = filter, search });
    }

    /// <summary>
    /// Sets any status directly — for the times a parcel is handed back or a click was wrong.
    /// The one-click "next step" button above stays the fast path for the daily routine.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetOrderStatus(int id, string status, string filter = null, string search = null)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            TempData["Error"] = "That order no longer exists.";
            return RedirectToAction(nameof(Orders));
        }

        if (string.IsNullOrWhiteSpace(status) || !OrderStatuses.All.Contains(status))
        {
            TempData["Error"] = "That is not a status this shop uses.";
            return RedirectToAction(nameof(Orders), new { status = filter, search });
        }

        if (order.Status == status)
        {
            TempData["Toast"] = $"{order.OrderNumber} is already {status}.";
            return RedirectToAction(nameof(Orders), new { status = filter, search });
        }

        var previous = order.Status;
        order.Status = status;
        await _db.SaveChangesAsync();

        TempData["Toast"] = $"{order.OrderNumber} moved from {previous} to {status}.";
        return RedirectToAction(nameof(Orders), new { status = filter, search });
    }

    /// <summary>Downloads the order book (all of it, or one status) as a spreadsheet-friendly CSV.</summary>
    [HttpGet]
    public async Task<IActionResult> ExportOrders(string status = null)
    {
        var query = _db.Orders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status) && OrderStatuses.All.Contains(status))
        {
            query = query.Where(o => o.Status == status);
        }

        var rows = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("Order number,Date (UTC),Customer,Email,Items,Total INR,Payment,Method,Status,Products");

        foreach (var order in rows)
        {
            csv.AppendLine(string.Join(',',
                CsvField(order.OrderNumber),
                order.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                CsvField(order.Customer),
                CsvField(order.Email),
                order.ItemCount.ToString(CultureInfo.InvariantCulture),
                order.Total.ToString("0.00", CultureInfo.InvariantCulture),
                CsvField(order.Payment),
                CsvField(order.PaymentMethod),
                CsvField(order.Status),
                CsvField(order.ProductName)));
        }

        // The BOM is what makes Excel open the file as UTF-8 and keep the ₹-priced names readable.
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(csv.ToString());
        var fileName = $"sabarna-orders-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv";

        return File(bytes, "text/csv", fileName);
    }

    /// <summary>Quotes a CSV field when it contains a comma, a quote or a line break.</summary>
    private static string CsvField(string value)
    {
        value ??= string.Empty;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    // =============================================================================================
    // PRODUCTS
    // =============================================================================================

    [HttpGet]
    public async Task<IActionResult> Products(string search)
    {
        var all = await _db.Products.AsNoTracking()
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .ToListAsync();

        var term = (search ?? string.Empty).Trim();
        var rows = term.Length == 0
            ? all
            : all.Where(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                          || p.Category.Contains(term, StringComparison.OrdinalIgnoreCase)
                          || p.Unit.Contains(term, StringComparison.OrdinalIgnoreCase))
                 .ToList();

        var model = new AdminProductsViewModel
        {
            Products = rows,
            Search = term,
            TotalCount = all.Count,
            ActiveCount = all.Count(p => p.Active),
            HiddenCount = all.Count(p => !p.Active),
            OutOfStockCount = all.Count(p => p.Stock == 0),
            TotalUnits = all.Sum(p => p.Stock)
        };

        ViewData["Title"] = "Products";
        return View(model);
    }

    /// <summary>Quick stock correction straight from the products table.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStock(int id, int stock, string search = null)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product == null)
        {
            TempData["Error"] = "That product no longer exists.";
            return RedirectToAction(nameof(Products), new { search });
        }

        // Keep the number sane: the shop cannot hold negative jars or a million of them.
        stock = Math.Clamp(stock, 0, 9999);
        var changed = product.Stock != stock;
        product.Stock = stock;
        await _db.SaveChangesAsync();

        TempData["Toast"] = changed
            ? $"{product.Name} now has {stock} on the shelf."
            : $"{product.Name} already had {stock} on the shelf.";

        return RedirectToAction(nameof(Products), new { search });
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
    // CUSTOMERS
    // =============================================================================================

    /// <summary>
    /// There is no customer account table — the shop does not ask visitors to sign up. So the
    /// customer list is rolled up from the order book: one row per email address.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Customers(string search)
    {
        var facts = await _db.Orders.AsNoTracking()
            .Select(o => new { o.Customer, o.Email, o.ItemCount, o.Total, o.Payment, o.CreatedAt })
            .ToListAsync();

        var rows = facts
            .GroupBy(f => string.IsNullOrWhiteSpace(f.Email) ? f.Customer : f.Email)
            .Select(group =>
            {
                var latest = group.OrderByDescending(f => f.CreatedAt).First();

                return new AdminCustomerRow
                {
                    Name = latest.Customer,
                    Email = latest.Email,
                    Orders = group.Count(),
                    Jars = group.Sum(f => f.ItemCount),
                    Spent = group.Where(f => f.Payment == "Paid").Sum(f => f.Total),
                    LastOrderAt = group.Max(f => f.CreatedAt),
                    IsReturning = group.Count() > 1
                };
            })
            .OrderByDescending(row => row.Spent)
            .ThenByDescending(row => row.LastOrderAt)
            .ToList();

        var term = (search ?? string.Empty).Trim();
        if (term.Length > 0)
        {
            rows = rows
                .Where(row => row.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                           || row.Email.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var model = new AdminCustomersViewModel
        {
            Customers = rows,
            Search = term,
            ReturningCount = rows.Count(row => row.IsReturning),
            Lifetime = rows.Sum(row => row.Spent)
        };

        ViewData["Title"] = "Customers";
        return View(model);
    }

    // =============================================================================================
    // SETTINGS
    // =============================================================================================

    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        var settings = await _db.Settings.FirstOrDefaultAsync() ?? new SiteSetting
        {
            BrandName = "Sabarna Pure",
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

    /// <summary>
    /// Changes the signed-in admin's password. A separate POST from the settings form on purpose:
    /// a typo in one cannot silently throw away the other, and the browser's password manager
    /// recognises the three password fields.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel form)
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var admin = string.IsNullOrWhiteSpace(email) ? null : await _auth.FindActiveAdminAsync(email);

        if (admin == null)
        {
            return RedirectToAction(nameof(Login));
        }

        // Length and the two-fields-match rule come from the view model's annotations.
        if (!ModelState.IsValid)
        {
            var first = ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage)
                .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

            TempData["Error"] = string.IsNullOrWhiteSpace(first)
                ? "Please check the password fields and try again."
                : first;

            return RedirectToAction(nameof(Settings));
        }

        if (!_auth.VerifyPassword(admin, form.CurrentPassword ?? string.Empty))
        {
            TempData["Error"] = "That is not the current password — nothing was changed.";
            return RedirectToAction(nameof(Settings));
        }

        var newPassword = (form.NewPassword ?? string.Empty).Trim();

        // A short password on a public URL is how shops get their orders edited by strangers.
        if (newPassword.Length < 8 || !newPassword.Any(char.IsLetter) || !newPassword.Any(char.IsDigit))
        {
            TempData["Error"] = "Use at least 8 characters, including one letter and one number.";
            return RedirectToAction(nameof(Settings));
        }

        if (newPassword == form.CurrentPassword)
        {
            TempData["Error"] = "The new password is the same as the old one — pick something else.";
            return RedirectToAction(nameof(Settings));
        }

        await _auth.ChangePasswordAsync(admin, newPassword);

        // saptarshi: Phase 0 — the seeded password is gone, so the panel opens properly now. The
        // cookie is re-issued so the guard stops firing without needing a sign-out.
        if (admin.MustChangePassword)
        {
            admin.MustChangePassword = false;
            await _db.SaveChangesAsync();
            await SignInAdminAsync(admin);
        }

        TempData["Toast"] = "Password changed. Use the new one the next time you sign in.";
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
