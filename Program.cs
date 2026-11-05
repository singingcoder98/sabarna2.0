using Sabarna.Web.Data;
using Sabarna.Web.Models;
using Sabarna.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
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

// ---- 1b. Preview mode -----------------------------------------------------------------------
// While the shop is being shown to people before launch it must stay out of search engines (and
// can ask for a shared code). Both switches live in appsettings.json under "Preview".
builder.Services.Configure<PreviewOptions>(builder.Configuration.GetSection("Preview"));

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
// A single SQLite file next to the app. No server to install, no connection secrets to keep.
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = "Data Source=app.db";
}
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

// ---- 3. Session (used for the shopping cart and the pending admin login) ----------------------
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".sabarna.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;   // required, so it may be set before cookie consent
    options.IdleTimeout = TimeSpan.FromDays(7);
});

builder.Services.AddHttpContextAccessor();

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

// ---- Create the SQLite schema and the demo data on first run --------------------------------
// EnsureCreatedAsync is all a single-file SQLite database needs; there is nothing to migrate.
using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

// ---- Request pipeline -----------------------------------------------------------------------
// First, so every later middleware sees the real scheme and host.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
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

app.UseStaticFiles();

// Preview gate: noindex everywhere, and optionally a shared code before anything is shown.
app.UseMiddleware<PreviewGateMiddleware>();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
