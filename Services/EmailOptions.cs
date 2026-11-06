// saptarshi: added 06-Nov-2026 — this whole file is new.
namespace Sabarna.Web.Services;

/// <summary>
/// Outgoing mail. Brevo's free plan (300 emails a day, no card) is the recommended provider:
///
///   Email:Enabled       false until you fill the rest in — the site works, it just sends nothing
///   Email:Host          smtp-relay.brevo.com
///   Email:Port          587
///   Email:UseStartTls   true   (587 upgrades the connection with STARTTLS)
///   Email:UserName      your Brevo login email
///   Email:Password      the SMTP key Brevo shows you (not the API key)
///   Email:FromAddress   the address you verified with Brevo
///   Email:OwnerAddress  where order alerts should land (usually the same as FromAddress)
///   Email:BackupOwnerAddress  a second inbox for the admin code, so one bounce cannot lock you out
/// </summary>
public class EmailOptions
{
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public bool UseStartTls { get; set; } = true;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "SABARNA";

    public string OwnerAddress { get; set; } = string.Empty;

    /// <summary>A second address for the admin's one-time code. Bounces happen.</summary>
    public string BackupOwnerAddress { get; set; } = string.Empty;

    /// <summary>True only when the switch is on and there is enough information to actually send.</summary>
    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(FromAddress);
}
