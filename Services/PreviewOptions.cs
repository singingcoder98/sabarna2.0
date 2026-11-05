namespace Sabarna.Web.Services;

/// <summary>
/// Controls how the shop behaves while it is being shown to people but has not launched yet.
/// Bound from the "Preview" section of appsettings.json, so the owner can change it without
/// touching code — and turn it off on launch day with a single word.
/// </summary>
public class PreviewOptions
{
    /// <summary>
    /// While true the shop tells every crawler to stay away: an X-Robots-Tag header on every
    /// response, a noindex meta tag on every page, and a robots.txt that disallows everything.
    ///
    /// Nothing is hidden from people. Anyone with the link sees the whole shop — this only stops
    /// a temporary address (a tunnel, a free host) from being indexed before launch day.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
