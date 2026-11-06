using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Sabarna.Web.Services;

/// <summary>
/// One small piece of middleware with two jobs, both only active while the shop is a preview:
///
///   1. Keep the site out of every search engine (header + robots tag + robots.txt), so a link
///      passed around on WhatsApp can never end up indexed under a temporary address.
///   2. Optionally ask for a shared access code before showing anything, for the case where the
///      owner wants only the people they sent the link to.
///
/// Neither job interferes with the admin panel (which has its own two-step login) or with
/// static files (the gate page needs its CSS and logo to render).
/// </summary>
public sealed class PreviewGateMiddleware
{
    /// <summary>Cookie remembering that this browser already typed the code.</summary>
    public const string CookieName = ".sabarna.preview";

    /// <summary>How long the "I know the code" cookie lasts.</summary>
    public static readonly TimeSpan PassLifetime = TimeSpan.FromDays(30);

    private readonly RequestDelegate _next;

    public PreviewGateMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IOptions<PreviewOptions> optionsAccessor)
    {
        var options = optionsAccessor.Value;

        if (!options.Enabled)
        {
            await _next(context);
            return;
        }

        // Everything below can only happen while the preview is on. The template literal is a
        // header value, so it is set before anything has been written to the response.
        context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        context.Items["SitePreview"] = true;

        var needsCode = options.RequireCode
                        && !HasPass(context.Request, options)
                        && !IsAlwaysAllowed(context.Request.Path);

        if (!needsCode)
        {
            await _next(context);
            return;
        }

        var returnUrl = context.Request.Path + context.Request.QueryString;
        context.Response.Redirect($"/Preview/Gate?returnUrl={Uri.EscapeDataString(returnUrl)}");
    }

    /// <summary>True when this browser sent the right preview cookie.</summary>
    public static bool HasPass(HttpRequest request, PreviewOptions options)
    {
        var cookie = request.Cookies[CookieName];
        if (string.IsNullOrEmpty(cookie))
        {
            return false;
        }

        return FixedTimeEquals(cookie, TokenFor(options.AccessCode));
    }

    /// <summary>
    /// What the cookie stores: a hash of the code, never the code itself. Changing the code in
    /// appsettings.json therefore invalidates every cookie that was handed out before.
    /// </summary>
    public static string TokenFor(string accessCode)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"sabarna-preview:{accessCode}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Compares two strings without leaking where they differ through timing.</summary>
    public static bool FixedTimeEquals(string left, string right)
    {
        var a = Encoding.UTF8.GetBytes(left ?? string.Empty);
        var b = Encoding.UTF8.GetBytes(right ?? string.Empty);

        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    /// <summary>
    /// Paths that stay reachable with no code at all: the gate itself, the admin sign-in, the CSS,
    /// JavaScript, images and fonts the gate page needs, and the crawler files.
    /// </summary>
    private static bool IsAlwaysAllowed(PathString path)
    {
        if (!path.HasValue)
        {
            return false;
        }

        string[] allowed =
        {
            "/Preview", "/Admin", "/lib", "/css", "/images",
            "/favicon.ico", "/favicon.png", "/robots.txt", "/sitemap.xml", "/Home/Error",
            // saptarshi: added 06-Nov-2026 — Razorpay cannot type a preview code, and a redirected
            // webhook is a payment that never gets confirmed.
            "/Cart/RazorpayWebhook"
        };

        return allowed.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
