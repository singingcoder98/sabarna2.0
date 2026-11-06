// saptarshi: added 06-Nov-2026 — this whole file is new.
// -------------------------------------------------------------------------------------------------
// EmailService — the shop's only way of sending mail.
//
// Design rules, on purpose:
//   • Sending never throws into the request. A dead SMTP server must not lose an order; every method
//     returns true/false and writes the failure to the log.
//   • Nothing is sent while Email:Enabled is false, so the site runs happily before keys exist.
//   • Every message has a plain-text alternative and a plain-text-only body where possible; that is
//     what keeps mail out of the spam folder.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sabarna.Web.Models;

namespace Sabarna.Web.Services;

public class EmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => _options.IsConfigured;

    // ---------------------------------------------------------------------------------------------
    // The one method that actually talks to the mail server.
    // ---------------------------------------------------------------------------------------------
    public async Task<bool> SendAsync(string to, string subject, string htmlBody, string textBody)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(to))
        {
            _logger.LogInformation("Email skipped (not configured or no recipient): {Subject}", subject);
            return false;
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromAddress, _options.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);

            // A plain-text version. Some inboxes (and most spam filters) expect one.
            message.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(textBody, null, "text/plain"));

            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.UseStartTls,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_options.UserName, _options.Password),
                Timeout = 20000
            };

            await client.SendMailAsync(message);
            return true;
        }
        catch (Exception ex)
        {
            // Logged, never rethrown: the order is already saved at this point.
            _logger.LogError(ex, "Could not send '{Subject}' to {To}.", subject, to);
            return false;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // The admin's one-time code. Two copies go out: the owner address and the backup address, so a
    // single bounce (or a spam folder) cannot lock the owner out of their own shop.
    // ---------------------------------------------------------------------------------------------
    public async Task<bool> SendOtpAsync(string code, string adminEmail)
    {
        var subject = $"SABARNA admin code: {code}";
        var text = $"Your SABARNA admin code is {code}. It expires in 5 minutes.\n\n"
                 + "If you did not try to sign in, change the admin password immediately.";
        var html = $@"<div style=""font-family:Segoe UI,Helvetica,Arial,sans-serif;font-size:15px;color:#1e2a14"">
  <p>Your SABARNA admin code is</p>
  <p style=""font-size:30px;letter-spacing:6px;font-weight:700;margin:12px 0"">{code}</p>
  <p style=""color:#5c6a50"">It expires in 5 minutes. If you did not try to sign in, change the admin password immediately.</p>
</div>";

        var sent = false;

        if (!string.IsNullOrWhiteSpace(_options.OwnerAddress))
        {
            sent |= await SendAsync(_options.OwnerAddress, subject, html, text);
        }

        if (!string.IsNullOrWhiteSpace(_options.BackupOwnerAddress))
        {
            sent |= await SendAsync(_options.BackupOwnerAddress, subject, html, text);
        }

        // Last resort: the admin's own address on the account.
        if (!sent && !string.IsNullOrWhiteSpace(adminEmail))
        {
            sent = await SendAsync(adminEmail, subject, html, text);
        }

        return sent;
    }

    // ---------------------------------------------------------------------------------------------
    // Order emails
    // ---------------------------------------------------------------------------------------------

    /// <summary>Tells the owner money arrived, with everything needed to pack the parcel.</summary>
    public async Task<bool> SendOrderPaidToOwnerAsync(Order order)
    {
        if (string.IsNullOrWhiteSpace(_options.OwnerAddress))
        {
            return false;
        }

        var subject = $"Paid ₹{order.Total:0} — {order.OrderNumber}";
        var items = string.Join("\n", order.Items.Select(i => $"  {i.Qty} x {i.Name} = ₹{i.LineTotal:0}"));

        var text = $@"A paid order has come in.

Order      {order.OrderNumber}
Amount     ₹{order.Total:0}
Method     {order.PaymentMethod} (Razorpay {order.RazorpayPaymentId})
Customer   {order.Customer}
Phone      {order.Phone}
Email      {order.Email}
Address    {order.Address}
Note       {order.Note}

Items
{items}

Open the admin panel to mark it Packed when it leaves the farm.";

        var html = $@"<div style=""font-family:Segoe UI,Helvetica,Arial,sans-serif;font-size:15px;color:#1e2a14"">
  <h2 style=""color:#2f3e24"">Paid order — {WebUtility.HtmlEncode(order.OrderNumber)}</h2>
  <p style=""font-size:22px;font-weight:700"">₹{order.Total:0}</p>
  <p>
    <strong>{WebUtility.HtmlEncode(order.Customer)}</strong><br />
    {WebUtility.HtmlEncode(order.Phone)}<br />
    {WebUtility.HtmlEncode(order.Email)}
  </p>
  <p><strong>Deliver to</strong><br />{WebUtility.HtmlEncode(order.Address).Replace("\n", "<br />")}</p>
  <p><strong>Items</strong><br />{string.Join("<br />",
      order.Items.Select(i => $"{i.Qty} &times; {WebUtility.HtmlEncode(i.Name)} — ₹{i.LineTotal:0}"))}</p>
  <p style=""color:#5c6a50"">Payment {WebUtility.HtmlEncode(order.PaymentMethod)} · Razorpay id {WebUtility.HtmlEncode(order.RazorpayPaymentId)}</p>
</div>";

        return await SendAsync(_options.OwnerAddress, subject, html, text);
    }

    /// <summary>The customer's copy. Kept short, honest and free of marketing noise.</summary>
    public async Task<bool> SendOrderPaidToCustomerAsync(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.Email))
        {
            return false;
        }

        var subject = $"SABARNA order {order.OrderNumber} confirmed";
        var items = string.Join("\n", order.Items.Select(i => $"  {i.Qty} x {i.Name} = ₹{i.LineTotal:0}"));

        var text = $@"Thank you, {order.Customer}.

We have your payment of ₹{order.Total:0} and your order {order.OrderNumber} is being packed by hand.

{items}

We will email the SkyKing tracking number the evening it leaves the farm. Keep this order number:
you can check the parcel any time on our website.

SABARNA — small-batch ghee, honey & pickles";

        var html = $@"<div style=""font-family:Segoe UI,Helvetica,Arial,sans-serif;font-size:15px;color:#1e2a14"">
  <h2 style=""color:#2f3e24"">Thank you, {WebUtility.HtmlEncode(order.Customer)}</h2>
  <p>We have your payment of <strong>₹{order.Total:0}</strong> and order
     <strong>{WebUtility.HtmlEncode(order.OrderNumber)}</strong> is being packed by hand.</p>
  <p>{string.Join("<br />", order.Items.Select(i => $"{i.Qty} &times; {WebUtility.HtmlEncode(i.Name)} — ₹{i.LineTotal:0}"))}</p>
  <p style=""color:#5c6a50"">We will email the tracking number the evening it leaves the farm.</p>
</div>";

        return await SendAsync(order.Email, subject, html, text);
    }

    /// <summary>When a payment fails or is refunded — the owner should know before the customer calls.</summary>
    public async Task<bool> SendOrderProblemToOwnerAsync(Order order, string reason)
    {
        if (string.IsNullOrWhiteSpace(_options.OwnerAddress))
        {
            return false;
        }

        var subject = $"Payment {order.PaymentStatus}: {order.OrderNumber}";
        var text = $"Order {order.OrderNumber} is {order.PaymentStatus}.\nReason: {reason}\n"
                 + $"Customer {order.Customer} ({order.Phone})\nAmount ₹{order.Total:0}";
        var html = $@"<div style=""font-family:Segoe UI,Helvetica,Arial,sans-serif;font-size:15px"">
  <h2>Payment {WebUtility.HtmlEncode(order.PaymentStatus)} — {WebUtility.HtmlEncode(order.OrderNumber)}</h2>
  <p>{WebUtility.HtmlEncode(reason)}</p>
  <p>Customer {WebUtility.HtmlEncode(order.Customer)} · {WebUtility.HtmlEncode(order.Phone)} · ₹{order.Total:0}</p>
</div>";

        return await SendAsync(_options.OwnerAddress, subject, html, text);
    }
}
