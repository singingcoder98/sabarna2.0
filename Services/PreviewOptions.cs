namespace Sabarna.Web.Services;

/// <summary>
/// Controls how the shop behaves while it is being shown to people but is not launched yet.
/// Bound from the "Preview" section of appsettings.json, so the owner can change it without
/// touching code — and turn it off on launch day with a single word.
/// </summary>
public class PreviewOptions
{
    /// <summary>
    /// While true the shop tells every crawler to stay away: an X-Robots-Tag header on every
    /// response, a noindex meta tag, and a robots.txt that disallows everything. The site works
    /// normally for humans; it simply never enters a search index before launch.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When true, a visitor must type <see cref="AccessCode"/> once before they see the shop.
    /// Leave false if you are happy for anyone holding the link to browse; turn it on if you only
    /// want the people you send the code to.
    /// </summary>
    public bool RequireCode { get; set; }

    /// <summary>The shared password for the preview. Only used while <see cref="RequireCode"/> is on.</summary>
    public string AccessCode { get; set; } = "sabarna-preview";
}
