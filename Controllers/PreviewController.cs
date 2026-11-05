using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sabarna.Web.Services;

namespace Sabarna.Web.Controllers;

/// <summary>
/// The "you were sent this link" door. Only reachable while Preview:RequireCode is on; if the owner
/// leaves the preview open, <see cref="PreviewGateMiddleware"/> never sends anyone here.
/// </summary>
public class PreviewController : Controller
{
    private readonly PreviewOptions _options;

    public PreviewController(IOptions<PreviewOptions> options)
    {
        _options = options.Value;
    }

    [HttpGet]
    public IActionResult Gate(string returnUrl = null)
    {
        // Already holding a valid pass? Then there is nothing to ask for.
        if (!_options.Enabled || !_options.RequireCode || PreviewGateMiddleware.HasPass(Request, _options))
        {
            return SafeRedirect(returnUrl);
        }

        ViewData["Title"] = "Private preview";
        ViewData["Description"] = "Sabarna is being shown to a few people before it opens to everyone.";
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Enter(string code, string returnUrl = null)
    {
        if (!_options.RequireCode)
        {
            return SafeRedirect(returnUrl);
        }

        if (!PreviewGateMiddleware.FixedTimeEquals((code ?? string.Empty).Trim(), _options.AccessCode.Trim()))
        {
            TempData["Error"] = "That code does not match. Check the message you were sent and try again.";
            return RedirectToAction(nameof(Gate), new { returnUrl });
        }

        Response.Cookies.Append(PreviewGateMiddleware.CookieName, PreviewGateMiddleware.TokenFor(_options.AccessCode),
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                // Secure only when the page really is https (it is, behind a tunnel).
                Secure = Request.IsHttps,
                IsEssential = true,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.Add(PreviewGateMiddleware.PassLifetime)
            });

        TempData["Toast"] = "The door is open — have a look around.";
        return SafeRedirect(returnUrl);
    }

    /// <summary>
    /// Returns the visitor to the page they asked for, but only if it is a page on this site —
    /// an open redirect is not a gift a shop should hand out.
    /// </summary>
    private IActionResult SafeRedirect(string returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }
}
