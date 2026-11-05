using Microsoft.Extensions.Options;

namespace Sabarna.Web.Services;

/// <summary>
/// Keeps an unlaunched shop out of search engines — and does nothing else. Everyone who is given
/// the link sees the shop exactly like a visitor on launch day: no code, no wall, no sign-in.
///
/// A temporary address (a tunnel, a free host) must never be indexed: it would compete with the
/// real domain later, and a page full of unpublished prices has no business in a search result.
/// One switch in appsettings.json turns all of this off on launch day.
/// </summary>
public sealed class PreviewNoIndexMiddleware
{
    private readonly RequestDelegate _next;

    public PreviewNoIndexMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IOptions<PreviewOptions> optionsAccessor)
    {
        if (optionsAccessor.Value.Enabled)
        {
            // Header for crawlers that read HTTP instead of HTML…
            context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

            // …and a flag the head partial turns into <meta name="robots">.
            context.Items["SitePreview"] = true;
        }

        await _next(context);
    }
}
