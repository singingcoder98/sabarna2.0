// saptarshi: added 06-Nov-2026 — this whole file is new.
// -------------------------------------------------------------------------------------------------
// RazorpayService — the only place that talks to Razorpay.
//
// Three jobs:
//   1. Create an order on Razorpay's side (so the amount is fixed before the customer pays).
//   2. Verify the signature Razorpay sends back to the browser. This is what makes it safe to show
//      "Paid": the signature is an HMAC of the order id and payment id, made with our secret key.
//      A visitor cannot forge it, so the browser cannot lie about having paid.
//   3. Verify the signature on a webhook, the same idea with the webhook secret.
//
// Everything is written so a gateway failure can never crash the shop: each call returns a result
// object with Success = false and a short reason.
// -------------------------------------------------------------------------------------------------

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sabarna.Web.Services;

/// <summary>A tiny result object shared by the three calls below.</summary>
public sealed class RazorpayResult
{
    public bool Success { get; init; }

    public string Id { get; init; } = string.Empty;

    public string Error { get; init; } = string.Empty;

    public static RazorpayResult Ok(string id) => new() { Success = true, Id = id };

    public static RazorpayResult Fail(string error) => new() { Success = false, Error = error };
}

public class RazorpayService
{
    private const string ApiBase = "https://api.razorpay.com/v1/";

    private readonly HttpClient _http;
    private readonly RazorpayOptions _options;
    private readonly ILogger<RazorpayService> _logger;

    public RazorpayService(HttpClient http, IOptions<RazorpayOptions> options, ILogger<RazorpayService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>False when the switch is off or a key is missing — callers then use the demo path.</summary>
    public bool IsConfigured => _options.IsConfigured;

    public string KeyId => _options.KeyId;

    public string Currency => string.IsNullOrWhiteSpace(_options.Currency) ? "INR" : _options.Currency;

    // ---------------------------------------------------------------------------------------------
    // 1. Create the order on Razorpay's side.
    //    amountPaise = rupees x 100. Razorpay only understands paise, never rupees.
    // ---------------------------------------------------------------------------------------------
    public async Task<RazorpayResult> CreateOrderAsync(string receipt, long amountPaise, string customerNote)
    {
        if (!IsConfigured)
        {
            return RazorpayResult.Fail("Razorpay is not configured.");
        }

        // Notes are limited to 15 small fields on Razorpay's side; one line is plenty here.
        var payload = new Dictionary<string, object>
        {
            ["amount"] = amountPaise,
            ["currency"] = Currency,
            ["receipt"] = receipt,
            ["notes"] = new Dictionary<string, string>
            {
                ["order"] = receipt,
                ["shop"] = "Sabarna"
            }
        };

        _ = customerNote;   // kept in the signature on purpose: the parameter documents the intent

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiBase + "orders")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = BasicAuth();

            using var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Razorpay create-order failed ({Status}): {Body}", (int)response.StatusCode, body);
                return RazorpayResult.Fail($"Razorpay answered {(int)response.StatusCode}.");
            }

            using var document = JsonDocument.Parse(body);
            var id = document.RootElement.TryGetProperty("id", out var idElement)
                ? idElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(id))
            {
                _logger.LogError("Razorpay create-order returned no id: {Body}", body);
                return RazorpayResult.Fail("Razorpay returned no order id.");
            }

            return RazorpayResult.Ok(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Razorpay create-order threw.");
            return RazorpayResult.Fail("Could not reach Razorpay.");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 2. The signature that comes back with the browser redirect.
    //    Razorpay signs "{order_id}|{payment_id}" with our key secret. If our own HMAC matches the
    //    signature we were given, the payment is real and belongs to that exact order.
    // ---------------------------------------------------------------------------------------------
    public bool VerifyPaymentSignature(string orderId, string paymentId, string signature)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(paymentId))
        {
            return false;
        }

        var expected = HmacHex($"{orderId}|{paymentId}", _options.KeySecret);
        return FixedTimeEquals(expected, signature);
    }

    // ---------------------------------------------------------------------------------------------
    // 3. The signature on a webhook: HMAC of the WHOLE raw body with the webhook secret.
    //    The body must be the exact bytes Razorpay sent — never re-serialised JSON.
    // ---------------------------------------------------------------------------------------------
    public bool VerifyWebhookSignature(string rawBody, string signature)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            return false;
        }

        var expected = HmacHex(rawBody ?? string.Empty, _options.WebhookSecret);
        return FixedTimeEquals(expected, signature);
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Refund. Used when a payment is confirmed but the stock turned out to be gone.
    //    The customer gets their money back inside the same request, not "later".
    // ---------------------------------------------------------------------------------------------
    public async Task<RazorpayResult> RefundAsync(string paymentId, long amountPaise, string reason)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(paymentId))
        {
            return RazorpayResult.Fail("Razorpay is not configured.");
        }

        var payload = new Dictionary<string, object>
        {
            ["amount"] = amountPaise,
            ["speed"] = "normal",
            ["notes"] = new Dictionary<string, string> { ["reason"] = reason }
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiBase + "payments/" + Uri.EscapeDataString(paymentId) + "/refund")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = BasicAuth();

            using var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Razorpay refund failed ({Status}): {Body}", (int)response.StatusCode, body);
                return RazorpayResult.Fail($"Razorpay answered {(int)response.StatusCode}.");
            }

            using var document = JsonDocument.Parse(body);
            var id = document.RootElement.TryGetProperty("id", out var idElement)
                ? idElement.GetString()
                : null;

            return RazorpayResult.Ok(id ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Razorpay refund threw.");
            return RazorpayResult.Fail("Could not reach Razorpay.");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------

    private AuthenticationHeaderValue BasicAuth()
    {
        var raw = $"{_options.KeyId}:{_options.KeySecret}";
        return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));
    }

    /// <summary>Lowercase hex HMAC-SHA256 — the format Razorpay signs with.</summary>
    private static string HmacHex(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Compares two hex strings without leaking, through the time it takes, how much of the
    /// signature matched. A normal string == is fine for most things; not for signatures.
    /// </summary>
    private static bool FixedTimeEquals(string left, string right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
        {
            return false;
        }

        var leftBytes = Encoding.UTF8.GetBytes(left.ToLowerInvariant());
        var rightBytes = Encoding.UTF8.GetBytes(right.ToLowerInvariant());

        if (leftBytes.Length != rightBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
