// saptarshi: added 06-Nov-2026 — this whole file is new.
namespace Sabarna.Web.Services;

/// <summary>
/// The four Razorpay settings, filled from appsettings.json or (better) from environment variables
/// on the host. The secrets must never be committed to git — the keys in appsettings.json are empty
/// on purpose, and the real ones live in web.config's &lt;environmentVariables&gt; block on the server.
///
///   Razorpay:Enabled        false until you are ready — the shop then keeps its old behaviour
///   Razorpay:KeyId          rzp_test_... or rzp_live_...
///   Razorpay:KeySecret      the secret next to that key id
///   Razorpay:WebhookSecret  the secret you type into the Razorpay webhook screen
/// </summary>
public class RazorpayOptions
{
    public bool Enabled { get; set; }

    public string KeyId { get; set; } = string.Empty;

    public string KeySecret { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;

    public string Currency { get; set; } = "INR";

    /// <summary>
    /// True only when the switch is on AND both secrets are present. Every caller checks this, so a
    /// half-filled configuration behaves exactly like "payments not wired up yet".
    /// </summary>
    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(KeyId)
        && !string.IsNullOrWhiteSpace(KeySecret);
}
