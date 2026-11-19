using Sabarna.Web.Data;
using Sabarna.Web.Models;
using Sabarna.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;   // saptarshi: AddResponseCompression lives here
using Microsoft.AspNetCore.HttpsPolicy;   // saptarshi: HttpsRedirectionOptions
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// ---------------------------------------------------------------------------------------------
// Sabarna.Web — application entry point.
//
// This file wires together the four building blocks of the site:
//   1. the database (SQLite file, created and seeded on first run)
//   2. the session cart (a small JSON blob inside the visitor's session cookie/session store)
//   3. cookie authentication for the single admin account (password + one-time code)
//   4. the request pipeline (static files -> routing -> session -> auth -> MVC)
// ---------------------------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

// ---- 1. MVC (controllers + Razor views) ------------------------------------------------------
builder.Services.AddControllersWithViews();

// saptarshi: 16-Nov-2026 — Core Web Vitals. HTML, CSS and JS were leaving the server uncompressed;
// brotli cuts a page to roughly a fifth of its size on the wire, which is the cheapest ranking win
// on a shared host. Images are already compressed, so they are deliberately left out of the list.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes
        .Concat(new[] { "image/svg+xml", "application/xml", "text/plain", "application/ld+json" });
});

// ---- 1b. Preview mode -----------------------------------------------------------------------
// While the shop is being shown to people before launch it must stay out of search engines (and
// can ask for a shared code). Both switches live in appsettings.json under "Preview".
builder.Services.Configure<PreviewOptions>(builder.Configuration.GetSection("Preview"));

// saptarshi: added 06-Nov-2026 — Razorpay and mail settings, read from configuration so the real
// secrets can live in environment variables on the host instead of in git.
builder.Services.Configure<RazorpayOptions>(builder.Configuration.GetSection("Razorpay"));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));

// saptarshi: 16-Nov-2026 — the public base address (canonical, og:url, sitemap, JSON-LD) comes from
// configuration instead of the request host, so the domain can change without touching code.
builder.Services.Configure<SeoOptions>(builder.Configuration.GetSection("Seo"));

// saptarshi: Phase 0 (12-Nov-2026) — HTTPS on/off in one place.
// "Security:ForceHttps" is true unless someone deliberately turns it off. It exists because a host
// without a certificate would otherwise redirect every visitor to an address that does not answer:
// the escape hatch is one environment variable, not a code change.
var forceHttps = builder.Configuration.GetValue<bool?>("Security:ForceHttps") ?? true;

// saptarshi: Phase 0 fix (14-Nov-2026) — the redirect needs to know WHERE https lives.
// Behind IIS the app is never told its own public port, so UseHttpsRedirection gives up quietly
// with "Failed to determine the https port" and plain http keeps working. That is exactly what the
// live site was doing. 443 is the answer for every normal host; HttpsPort in appsettings (or the
// HttpsPort environment variable) overrides it if a host ever uses something else.
builder.Services.Configure<HttpsRedirectionOptions>(options =>
{
    options.HttpsPort = builder.Configuration.GetValue<int?>("HttpsPort") ?? 443;
});

// Behind a tunnel (cloudflared, ngrok) or a hosting proxy, the request arrives as plain HTTP on
// localhost. Reading the forwarded headers is what keeps https, the public host name, canonical
// URLs and the sitemap correct from the visitor's point of view.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ---- 2. Database -----------------------------------------------------------------------------
// saptarshi: 18-Nov-2026 — SQLite or PostgreSQL, decided by configuration (see Data/DatabaseSetup.cs).
//
// The old code, kept here so the change is visible:
//     var connectionString = builder.Configuration.GetConnectionString("Default");
//     if (string.IsNullOrWhiteSpace(connectionString)) connectionString = "Data Source=app.db";
//     builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
//
// Nothing else in the application knows the difference: the same entities, the same queries, the
// same admin screens. Only the connection string and the provider change.
var databaseKind = DatabaseSetup.Resolve(builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = DatabaseSetup.DefaultSqliteConnectionString;
}

// saptarshi: 18-Nov-2026 — the classic migration mistake: provider switched to Postgres but the old
// SQLite connection string left behind. Npgsql would answer with a cryptic "Format of the initialization
// string does not conform", so it is caught here and said in plain words instead.
if (databaseKind == DatabaseKind.Postgres
    && !connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)
    && !connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("[!] Database:Provider is Postgres, but ConnectionStrings:Default still looks like " +
                      "the SQLite one (it needs Host=, Database=, Username=, Password=). " +
                      "Set ConnectionStrings__Default on the host.");
}

builder.Services.AddDbContext<AppDbContext>(options => DatabaseSetup.Configure(options, databaseKind, connectionString));

// ---- 3. Session (used for the shopping cart and the pending admin login) ----------------------
builder.Services.AddDistributedMemoryCache();

// saptarshi: added 12-Nov-2026 — a small in-memory cache, used only to count wrong admin passwords
// so nobody can guess one forever. It is per-process on purpose: no database, nothing to clean up.
builder.Services.AddMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".sabarna.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;   // required, so it may be set before cookie consent

    // saptarshi: Phase 0 — the basket cookie never travels on a plain-HTTP request when HTTPS is on.
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = forceHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

    options.IdleTimeout = TimeSpan.FromDays(7);
});

builder.Services.AddHttpContextAccessor();

// saptarshi: added 06-Nov-2026 — the payment client (one HttpClient, managed by the framework) and
// the mail sender. Both are harmless when their settings are empty: they simply say "not configured".
builder.Services.AddHttpClient<RazorpayService>();
builder.Services.AddScoped<EmailService>();

// saptarshi: Phase 0 — automatic database copies. Singleton because it holds no per-request state;
// the hosted service is the timer that calls it.
builder.Services.AddSingleton<BackupService>();
builder.Services.AddHostedService<BackupHostedService>();

// ---- 4. Domain services ---------------------------------------------------------------------
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();

// ---- 5. Admin authentication: cookie based, password + 6-digit OTP ---------------------------
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".sabarna.admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;

        // saptarshi: Phase 0 — the admin cookie is the keys to the shop; it must not be readable by
        // script (above) and must not be sent over plain HTTP while HTTPS is on.
        options.Cookie.SecurePolicy = forceHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/Admin/Login";
        options.LogoutPath = "/Admin/Logout";
        options.AccessDeniedPath = "/Admin/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    // Every admin screen carries [Authorize(Policy = "AdminOnly")].
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
});

var app = builder.Build();

// saptarshi: 18-Nov-2026 — say out loud which database this build is talking to. A wrong provider
// after a migration is the kind of thing that is silent for a week and then costs an afternoon.
app.Logger.LogInformation("Database provider: {Provider}.", databaseKind);

// ---- Create the SQLite schema and the demo data on first run --------------------------------
// saptarshi: changed 06-Nov-2026 — the patcher runs BEFORE the seeder, because the live database
// already exists and new columns (Address, RazorpayOrderId, ...) have to be added to it by
// ALTER TABLE. EnsureCreated only ever builds tables in a file that is brand new.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // saptarshi: 18-Nov-2026 — on PostgreSQL this is the first real network call. If the connection
    // string is wrong the shop would otherwise die here with a wall of Npgsql text and no clue,
    // so the failure is caught, written to the log in plain words, and the app carries on starting
    // (the pages will show the friendly error page instead of the host's 500 page).
    try
    {
        await db.Database.EnsureCreatedAsync();

        await SchemaPatcher.EnsureAsync(db, app.Logger);

        await DbSeeder.SeedAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex,
            "The database could not be prepared. Provider: {Provider}. Check ConnectionStrings:Default " +
            "(on the host: the ConnectionStrings__Default environment variable) and that the database " +
            "server is reachable.", databaseKind);
    }
}

// ---- Request pipeline -----------------------------------------------------------------------
// First, so every later middleware sees the real scheme and host.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// saptarshi: Phase 0 (12-Nov-2026) — plain http is sent to https before anything else happens, so a
// password or an admin cookie never travels in clear text. Behind IIS the request reaches the app as
// https already (ForwardedHeaders reads X-Forwarded-Proto), which is why this does not loop.
// 14-Nov-2026: the port is now set explicitly (above). Without it the middleware had no idea where
// to redirect and silently did nothing — the live site stayed on http while the cookies were Secure,
// which also meant a sign-in could never stick.
if (forceHttps && !app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Friendly error page for 404/403/500. The original status code is preserved for search engines.
app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

// A couple of cheap hardening headers. No inline script/style is blocked, so the site keeps
// working with the vendored Bootstrap and the small vanilla-JS snippets.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

// A page should have exactly one address. /Shop/ answers with a 301 to /Shop so search engines
// do not treat the two as competing copies of the same content.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (path is { Length: > 1 } && path.EndsWith('/'))
    {
        var target = path.TrimEnd('/') + context.Request.QueryString;
        context.Response.Redirect(target, permanent: true);
        return;
    }

    await next();
});

// saptarshi: 16-Nov-2026 — compress the response first, then serve the file.
app.UseResponseCompression();

// saptarshi: 16-Nov-2026 — static files get a cache header. The CSS and JS already carry
// ?v=<hash> (asp-append-version), so a long cache is safe for them; images are seven days, because
// a product photo is replaced under the SAME file name when the owner refreshes a jar.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        var path = context.Context.Request.Path.Value ?? string.Empty;
        var isImage = path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                   || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                   || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
                   || path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);

        context.Context.Response.Headers.CacheControl = isImage
            ? "public,max-age=604800"
            : "public,max-age=2592000";
    }
});

// Preview gate: noindex everywhere, and optionally a shared code before anything is shown.
app.UseMiddleware<PreviewGateMiddleware>();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// saptarshi: 16-Nov-2026 — clean product URLs: /product/kuler-achar-500g instead of /Shop/Details/7.
// The old numeric address still works and redirects (301) to this one, so no bookmark or WhatsApp
// link from the last two years breaks.
app.MapControllerRoute(
    name: "product",
    pattern: "product/{slug}",
    defaults: new { controller = "Shop", action = "Product" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
