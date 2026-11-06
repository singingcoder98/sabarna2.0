using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sabarna.Web.Data;
using Sabarna.Web.Models;
using Sabarna.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Sabarna.Web.Controllers;

/// <summary>
/// The two-step checkout: basket -> delivery details -> payment -> confirmation.
/// No login is required; the basket lives in the session (see <see cref="CartService"/>).
///
/// saptarshi: Razorpay was added on 06-Nov-2026. The shape of the flow is:
///
///   1. POST /Cart/Checkout     save the order as Pending, ask Razorpay for an order id
///   2. GET  /Cart/Payment      open Razorpay Checkout in the browser
///   3. POST /Cart/VerifyPayment  the browser returns three values; we check the signature here
///   4. POST /Cart/RazorpayWebhook  Razorpay's own notification, the recovery path when the
///                                  customer closes the tab before the redirect comes back
///
/// An order becomes "Paid" only in step 3 (after a signature check) or step 4 (after a signature
/// check). The browser on its own can never make an order paid.
/// </summary>
public class CartController : Controller
{
    /// <summary>The only payment methods we accept. Cash on delivery is not an option anywhere.</summary>
    private static readonly string[] AllowedPaymentMethods = { "UPI", "Card", "Netbanking" };

    // saptarshi: added — remembers the order being paid for, so a double click cannot create two.
    private const string PendingOrderSessionKey = ".sabarna.pending-order";

    // saptarshi: added — how long a half-finished order may be reused before a fresh one is made.
    private const int PendingOrderReuseMinutes = 20;

    private readonly CartService _cart;
    private readonly AppDbContext _db;

    // saptarshi: added — the three new dependencies.
    private readonly RazorpayService _razorpay;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CartController> _logger;

    public CartController(
        CartService cart,
        AppDbContext db,
        RazorpayService razorpay,
        IServiceScopeFactory scopes,
        ILogger<CartController> logger)
    {
        _cart = cart;
        _db = db;
        _razorpay = razorpay;
        _scopes = scopes;
        _logger = logger;
    }

    // ---------------------------------------------------------------------------------------------
    // GET /Cart — step 1 of 2
    // ---------------------------------------------------------------------------------------------
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Robots"] = "noindex, follow";   // transactional pages are not content
        ViewData["Title"] = "Your basket";
        ViewData["Description"] = "Review the jars in your basket before checkout.";
        return View(_cart.GetLines());
    }

    // ---------------------------------------------------------------------------------------------
    // POST /Cart/Add — called by every "Add to basket" button on the site
    // ---------------------------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int id, int qty = 1, string returnUrl = null)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);

        // saptarshi: added 06-Nov-2026 — "Add to basket" buttons now post in the background
        // (see the last block in Views/Shared/_Scripts.cshtml). The old form post redirected back to
        // the same page, and a redirect always lands at the TOP of the page, so the customer lost
        // their place in the shop and had to scroll down again. These three answers let the button
        // reply with a small JSON message instead. When JavaScript is off, nothing below changes and
        // the shop behaves exactly as before.
        if (product == null || !product.Active)
        {
            const string gone = "That jar is no longer on the shelf. Have a look at the rest of the shop.";

            if (WantsJson())
            {
                return Json(new { ok = false, count = _cart.Count(), message = gone, reload = true });
            }

            TempData["Error"] = gone;
            return RedirectToAction(nameof(Index));
        }

        if (product.Stock <= 0)
        {
            var soldOut = $"{product.Name} is out of stock right now. We restock every fortnight.";

            if (WantsJson())
            {
                return Json(new { ok = false, count = _cart.Count(), message = soldOut });
            }

            TempData["Error"] = soldOut;
            return Back(returnUrl);
        }

        _cart.Add(product, qty);

        if (WantsJson())
        {
            return Json(new
            {
                ok = true,
                count = _cart.Count(),
                message = $"{product.Name} is in your basket.",
                productId = product.Id
            });
        }

        TempData["Toast"] = $"{product.Name} is in your basket.";
        return Back(returnUrl);
    }

    // ---------------------------------------------------------------------------------------------
    // POST /Cart/Update — the +/- steppers on the basket page
    // ---------------------------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int productId, int qty, string returnUrl = null)
    {
        if (qty <= 0)
        {
            _cart.Remove(productId);
            TempData["Toast"] = "Removed from your basket.";
            return Back(returnUrl);
        }

        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null)
        {
            _cart.Remove(productId);
            TempData["Error"] = "That jar is no longer available, so we took it out of your basket.";
            return Back(returnUrl);
        }

        var ceiling = Math.Min(product.Stock, CartService.MaxQty);
        if (product.Stock > 0 && qty > ceiling)
        {
            _cart.SetQty(productId, ceiling);
            TempData["Error"] = $"Only {ceiling} of {product.Name} available — we set the quantity to {ceiling}.";
            return Back(returnUrl);
        }

        _cart.SetQty(productId, qty);
        return Back(returnUrl);
    }

    // ---------------------------------------------------------------------------------------------
    // POST /Cart/Remove
    // ---------------------------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Remove(int id, string returnUrl = null)
    {
        _cart.Remove(id);
        TempData["Toast"] = "Removed from your basket.";
        return Back(returnUrl);
    }

    // ---------------------------------------------------------------------------------------------
    // GET /Cart/Checkout — step 2 of 2
    // ---------------------------------------------------------------------------------------------
    [HttpGet]
    public IActionResult Checkout()
    {
        var lines = _cart.GetLines();
        if (lines.Count == 0)
        {
            TempData["Error"] = "Your basket is empty — add a jar or two first.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Robots"] = "noindex, follow";
        ViewData["Title"] = "Checkout";
        ViewData["Description"] = "Delivery details and online payment. No cash on delivery.";

        return View(new CheckoutViewModel { Lines = lines, PaymentMethod = "UPI" });
    }

    // ---------------------------------------------------------------------------------------------
    // POST /Cart/Checkout — validates on the server, saves the order, then hands over to Razorpay
    // ---------------------------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(CheckoutViewModel model)
    {
        ViewData["Robots"] = "noindex, follow";
        ViewData["Title"] = "Checkout";
        ViewData["Description"] = "Delivery details and online payment. No cash on delivery.";

        var lines = _cart.GetLines();
        if (lines.Count == 0)
        {
            TempData["Error"] = "Your basket is empty — add a jar or two first.";
            return RedirectToAction(nameof(Index));
        }

        // The browser never posts the basket back, so always rebuild it from the session.
        var productIds = lines.Select(l => l.ProductId).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        foreach (var line in lines)
        {
            var product = products.FirstOrDefault(p => p.Id == line.ProductId);

            if (product == null || !product.Active)
            {
                ModelState.AddModelError(string.Empty,
                    $"{line.Name} is no longer available. Please remove it from your basket.");
                continue;
            }

            if (line.Qty > product.Stock)
            {
                ModelState.AddModelError(string.Empty,
                    product.Stock <= 0
                        ? $"{product.Name} has just gone out of stock. Please remove it from your basket."
                        : $"Only {product.Stock} of {product.Name} left — please lower the quantity.");
            }

            // Always charge the price that is live right now.
            line.Price = product.Price;
            line.Name = product.Name;
        }

        model.Lines = lines;

        // Belt and braces: a hand-crafted POST cannot sneak in cash on delivery.
        if (string.IsNullOrWhiteSpace(model.PaymentMethod)
            || !AllowedPaymentMethods.Contains(model.PaymentMethod))
        {
            ModelState.AddModelError(nameof(model.PaymentMethod),
                "Please choose UPI, card or netbanking. We do not offer cash on delivery.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);   // re-render with the visitor's input and the error messages
        }

        // saptarshi: added — if this exact visitor already has a young, unpaid order, reuse it
        // instead of creating a second one. This is the "double click on Pay" guard.
        var reusable = await FindReusablePendingOrderAsync();
        if (reusable != null)
        {
            return RedirectToAction(nameof(Payment), new { orderNumber = reusable.OrderNumber });
        }

        // -----------------------------------------------------------------------------------------
        // saptarshi: changed — the order is now saved as Pending, with its delivery details and its
        // jars. The old code marked it Paid immediately; that is only kept for the demo path below.
        // -----------------------------------------------------------------------------------------
        var order = new Order
        {
            OrderNumber = await NextOrderNumberAsync(),
            Customer = model.Name.Trim(),
            Email = model.Email.Trim(),
            Phone = (model.Phone ?? string.Empty).Trim(),
            Address = (model.Address ?? string.Empty).Trim(),
            Note = (model.Note ?? string.Empty).Trim(),
            ItemCount = lines.Sum(l => l.Qty),
            ProductName = Summarise(lines),
            ProductImage = lines[0].Image,
            Total = model.Total,
            Payment = PaymentStatuses.Pending,
            PaymentStatus = PaymentStatuses.Pending,
            PaymentMethod = model.PaymentMethod,
            Status = OrderStatuses.Processing,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var line in lines)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = line.ProductId,
                Name = line.Name,
                Qty = line.Qty,
                UnitPrice = line.Price,
                LineTotal = line.LineTotal
            });
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        HttpContext.Session.SetString(PendingOrderSessionKey, order.OrderNumber);

        // -----------------------------------------------------------------------------------------
        // saptarshi: DEMO PATH — no Razorpay keys yet. Behave exactly like the old shop did, so the
        // site keeps working while the owner is still setting the gateway up.
        // -----------------------------------------------------------------------------------------
        if (!_razorpay.IsConfigured)
        {
            await TakeStockClampedAsync(order.Items);

            order.Payment = PaymentStatuses.Paid;
            order.PaymentStatus = PaymentStatuses.Paid;
            order.PaymentVerifiedBy = "demo (no gateway configured)";
            order.PaidAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            HttpContext.Session.Remove(PendingOrderSessionKey);
            _cart.Clear();

            return RedirectToAction(nameof(Success), new { orderNumber = order.OrderNumber });
        }

        // -----------------------------------------------------------------------------------------
        // saptarshi: REAL PATH — ask Razorpay for an order id, then open their sheet.
        // -----------------------------------------------------------------------------------------
        var created = await _razorpay.CreateOrderAsync(order.OrderNumber, ToPaise(order.Total), order.ProductName);

        if (!created.Success)
        {
            _logger.LogError("Could not create a Razorpay order for {Order}: {Error}", order.OrderNumber, created.Error);

            return RedirectToAction(nameof(PaymentFailed), new
            {
                orderNumber = order.OrderNumber,
                reason = "gateway"
            });
        }

        order.RazorpayOrderId = created.Id;
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Payment), new { orderNumber = order.OrderNumber });
    }

    // ---------------------------------------------------------------------------------------------
    // GET /Cart/Payment?orderNumber=SB-2026-1047
    // The hand-off page: it opens Razorpay Checkout and waits.
    // ---------------------------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Payment(string orderNumber)
    {
        var order = await LoadOrderAsync(orderNumber);

        if (order == null)
        {
            TempData["Error"] = "We could not find that order. Please start again.";
            return RedirectToAction(nameof(Index));
        }

        if (order.PaymentStatus == PaymentStatuses.Paid)
        {
            return RedirectToAction(nameof(Success), new { orderNumber = order.OrderNumber });
        }

        if (!_razorpay.IsConfigured)
        {
            return RedirectToAction(nameof(PaymentFailed), new { orderNumber = order.OrderNumber, reason = "gateway" });
        }

        // A retry after a failure arrives here with no Razorpay order id; make one now.
        if (string.IsNullOrWhiteSpace(order.RazorpayOrderId))
        {
            var created = await _razorpay.CreateOrderAsync(order.OrderNumber, ToPaise(order.Total), order.ProductName);

            if (!created.Success)
            {
                return RedirectToAction(nameof(PaymentFailed), new { orderNumber = order.OrderNumber, reason = "gateway" });
            }

            order.RazorpayOrderId = created.Id;
            await _db.SaveChangesAsync();
        }

        ViewData["Robots"] = "noindex, nofollow";
        ViewData["Title"] = "Secure payment";
        ViewData["Description"] = "Pay for your Sabarna order by UPI, card or netbanking.";

        var viewModel = new PaymentViewModel
        {
            OrderNumber = order.OrderNumber,
            RazorpayKeyId = _razorpay.KeyId,
            RazorpayOrderId = order.RazorpayOrderId,
            AmountPaise = ToPaise(order.Total),
            Amount = order.Total,
            CustomerName = order.Customer,
            CustomerEmail = order.Email,
            CustomerPhone = order.Phone,
            PaymentMethod = order.PaymentMethod,
            VerifyUrl = Url.Action(nameof(VerifyPayment)) ?? "/Cart/VerifyPayment",
            CancelUrl = Url.Action(nameof(PaymentFailed), new { orderNumber = order.OrderNumber, reason = "cancelled" }) ?? "/Cart",
            Ready = true
        };

        return View(viewModel);
    }

    // ---------------------------------------------------------------------------------------------
    // POST /Cart/VerifyPayment — the browser returns with three Razorpay values.
    //
    // This is the ONLY place a browser can influence "Paid", and only by producing a signature that
    // matches our own HMAC. The signature is made with the key secret, which never leaves the server,
    // so a forged request simply fails.
    // ---------------------------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyPayment(PaymentVerificationInput input)
    {
        if (string.IsNullOrWhiteSpace(input.RazorpayOrderId)
            || string.IsNullOrWhiteSpace(input.RazorpayPaymentId)
            || string.IsNullOrWhiteSpace(input.RazorpaySignature))
        {
            return Json(new { ok = false, message = "Razorpay did not send back a complete payment." });
        }

        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.RazorpayOrderId == input.RazorpayOrderId);

        if (order == null)
        {
            _logger.LogWarning("VerifyPayment: no order for Razorpay order {Id}.", input.RazorpayOrderId);
            return Json(new { ok = false, message = "We could not match that payment to an order." });
        }

        // Idempotent: a refresh of the return page must not pay twice or send two emails.
        if (order.PaymentStatus == PaymentStatuses.Paid)
        {
            _cart.Clear();
            HttpContext.Session.Remove(PendingOrderSessionKey);
            return Json(new { ok = true, redirect = Url.Action(nameof(Success), new { orderNumber = order.OrderNumber }) });
        }

        if (!_razorpay.VerifyPaymentSignature(input.RazorpayOrderId, input.RazorpayPaymentId, input.RazorpaySignature))
        {
            _logger.LogWarning("VerifyPayment: signature mismatch for {Order}.", order.OrderNumber);
            return Json(new { ok = false, message = "That payment could not be verified. Nothing has been charged twice — please contact us." });
        }

        order.RazorpaySignature = input.RazorpaySignature;

        var outcome = await MarkPaidAsync(order, input.RazorpayPaymentId, "browser");

        if (outcome == PaymentOutcome.OutOfStock)
        {
            await RefundUnshippableAsync(order);
            return Json(new { ok = false, message = "Sorry — a jar sold out while you were paying. Your payment has been refunded in full." });
        }

        if (outcome == PaymentOutcome.Error)
        {
            return Json(new { ok = false, message = "We have your payment but could not confirm the order. Please call us with your order number." });
        }

        _cart.Clear();
        HttpContext.Session.Remove(PendingOrderSessionKey);
        QueuePaidEmails(order.Id);

        return Json(new { ok = true, redirect = Url.Action(nameof(Success), new { orderNumber = order.OrderNumber }) });
    }

    // ---------------------------------------------------------------------------------------------
    // POST /Cart/RazorpayWebhook — Razorpay's own notification.
    //
    // This is the recovery path: the customer pays and closes the tab, so the browser never returns.
    // There is no form, no cookie and no antiforgery token here; the signature over the raw body is
    // the only credential, which is why it is checked before anything is read out of the payload.
    // ---------------------------------------------------------------------------------------------
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> RazorpayWebhook()
    {
        // The exact bytes must be hashed, so the body is read by hand and rewound afterwards.
        Request.EnableBuffering();

        string body;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync();
        }
        Request.Body.Position = 0;

        var signature = Request.Headers["X-Razorpay-Signature"].ToString();

        if (!_razorpay.VerifyWebhookSignature(body, signature))
        {
            _logger.LogWarning("Razorpay webhook rejected: signature did not verify.");
            return Unauthorized();
        }

        string eventName = string.Empty;
        string paymentId = string.Empty;
        string razorpayOrderId = string.Empty;
        string failureReason = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            eventName = GetString(root, "event");

            if (root.TryGetProperty("payload", out var payload)
                && payload.TryGetProperty("payment", out var payment)
                && payment.TryGetProperty("entity", out var entity))
            {
                paymentId = GetString(entity, "id");
                razorpayOrderId = GetString(entity, "order_id");
                failureReason = GetString(entity, "error_description");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Razorpay webhook: could not read the payload.");
            return BadRequest();
        }

        var eventId = Request.Headers["X-Razorpay-Event-Id"].ToString();
        if (string.IsNullOrWhiteSpace(eventId))
        {
            eventId = ShortHash(body);
        }

        // Idempotency: Razorpay retries. The same event must never move stock or send mail twice.
        var alreadySeen = await _db.PaymentEvents.AsNoTracking().AnyAsync(e => e.EventId == eventId);

        var order = string.IsNullOrWhiteSpace(razorpayOrderId)
            ? null
            : await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.RazorpayOrderId == razorpayOrderId);

        _db.PaymentEvents.Add(new PaymentEvent
        {
            EventId = eventId,
            EventName = eventName,
            OrderNumber = order?.OrderNumber ?? string.Empty,
            PaymentId = paymentId,
            SignatureVerified = true,
            Payload = body.Length > 7900 ? body[..7900] : body,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        if (alreadySeen)
        {
            return Ok();   // already handled on an earlier delivery
        }

        if (order == null)
        {
            _logger.LogWarning("Razorpay webhook {Event}: no order for {Order}.", eventName, razorpayOrderId);
            return Ok();   // recorded, nothing to do
        }

        switch (eventName)
        {
            case "payment.captured":
            case "order.paid":
                if (order.PaymentStatus != PaymentStatuses.Paid)
                {
                    var outcome = await MarkPaidAsync(order, paymentId, "webhook");

                    if (outcome == PaymentOutcome.OutOfStock)
                    {
                        await RefundUnshippableAsync(order);
                    }
                    else if (outcome == PaymentOutcome.Paid)
                    {
                        QueuePaidEmails(order.Id);
                    }
                }
                break;

            case "payment.failed":
                if (order.PaymentStatus == PaymentStatuses.Pending)
                {
                    order.PaymentStatus = PaymentStatuses.Failed;
                    order.Payment = PaymentStatuses.Failed;
                    order.FailureReason = Trim(failureReason, 200);
                    await _db.SaveChangesAsync();
                    QueueProblemEmails(order.Id, order.FailureReason);
                }
                break;
        }

        return Ok();
    }

    // ---------------------------------------------------------------------------------------------
    // GET /Cart/PaymentFailed — an honest "that did not go through" page, with a way back.
    // ---------------------------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> PaymentFailed(string orderNumber, string reason)
    {
        var order = await LoadOrderAsync(orderNumber);

        if (order == null)
        {
            TempData["Error"] = "We could not find that order. Please start again.";
            return RedirectToAction(nameof(Index));
        }

        // If money did go through after all, never show a failure page.
        if (order.PaymentStatus == PaymentStatuses.Paid)
        {
            return RedirectToAction(nameof(Success), new { orderNumber = order.OrderNumber });
        }

        var why = reason switch
        {
            "gateway" => "Our payment partner did not answer. Nothing has been charged.",
            "cancelled" => "The payment window was closed before it finished. Nothing has been charged.",
            _ => "The payment did not go through. Nothing has been charged."
        };

        ViewData["Robots"] = "noindex, nofollow";
        ViewData["Title"] = "Payment did not go through";
        ViewData["Description"] = "Try the payment again — your basket is still here.";

        return View(new PaymentFailedViewModel
        {
            OrderNumber = order.OrderNumber,
            Reason = why,
            CanRetry = _cart.GetLines().Count > 0 && _razorpay.IsConfigured,
            RetryUrl = Url.Action(nameof(Payment), new { orderNumber = order.OrderNumber }) ?? "/Cart"
        });
    }

    // ---------------------------------------------------------------------------------------------
    // GET /Cart/Success?orderNumber=SB-2026-1047
    // ---------------------------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Success(string orderNumber)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return RedirectToAction("Index", "Home");
        }

        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

        if (order == null)
        {
            TempData["Error"] = "We could not find that order number. Please check your confirmation email.";
            return RedirectToAction("Index", "Home");
        }

        ViewData["Robots"] = "noindex, follow";   // transactional pages are not content
        ViewData["Title"] = "Order confirmed";
        ViewData["Description"] = "Your Sabarna parcel is being packed.";
        return View(order);
    }

    // =============================================================================================
    // saptarshi: everything below this line is new — the payment plumbing.
    // =============================================================================================

    /// <summary>How "paid" turned out.</summary>
    private enum PaymentOutcome
    {
        Paid,
        AlreadyPaid,
        OutOfStock,
        Error
    }

    /// <summary>Rupees to paise. Razorpay only ever speaks paise.</summary>
    private static long ToPaise(decimal rupees)
    {
        return (long)Math.Round(rupees * 100m, MidpointRounding.AwayFromZero);
    }

    /// <summary>Loads an order with its jars, or null.</summary>
    private async Task<Order> LoadOrderAsync(string orderNumber)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return null;
        }

        return await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
    }

    /// <summary>
    /// Marks an order paid. Callers have already checked a signature — this method never decides
    /// "paid" on its own.
    ///
    /// The stock is taken here (not at checkout), inside a transaction, with a guard on every line:
    /// if any jar sold out in the meantime the whole thing rolls back and the caller refunds.
    /// </summary>
    private async Task<PaymentOutcome> MarkPaidAsync(Order order, string paymentId, string verifiedBy)
    {
        if (order.PaymentStatus == PaymentStatuses.Paid)
        {
            return PaymentOutcome.AlreadyPaid;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            if (!await TryTakeStockAsync(order.Items))
            {
                await transaction.RollbackAsync();
                _logger.LogWarning("Order {Order} paid but a jar had sold out — refunding.", order.OrderNumber);
                return PaymentOutcome.OutOfStock;
            }

            order.RazorpayPaymentId = string.IsNullOrWhiteSpace(paymentId) ? order.RazorpayPaymentId : paymentId;
            order.Payment = PaymentStatuses.Paid;
            order.PaymentStatus = PaymentStatuses.Paid;
            order.PaymentVerifiedBy = verifiedBy;
            order.PaidAt = DateTime.UtcNow;
            order.FailureReason = string.Empty;
            order.Status = OrderStatuses.Processing;

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return PaymentOutcome.Paid;
        }
        catch (DbUpdateException ex)
        {
            // The unique index on RazorpayPaymentId is the last line of defence against a payment
            // being applied to two orders.
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Could not mark {Order} paid.", order.OrderNumber);
            return PaymentOutcome.Error;
        }
    }

    /// <summary>
    /// Takes one jar off the shelf per line, but only if it is still there:
    /// the WHERE clause is what makes this safe when two people pay at the same second.
    /// </summary>
    private async Task<bool> TryTakeStockAsync(IEnumerable<OrderItem> items)
    {
        foreach (var item in items)
        {
            var affected = await _db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE ""Products""
   SET ""Stock"" = ""Stock"" - {item.Qty},
       ""Sold""  = ""Sold""  + {item.Qty}
 WHERE ""Id"" = {item.ProductId}
   AND ""Stock"" >= {item.Qty}");

            if (affected == 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The old behaviour, kept for the demo path only: clamp at zero, never fail.</summary>
    private async Task TakeStockClampedAsync(IEnumerable<OrderItem> items)
    {
        foreach (var item in items)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);
            if (product == null)
            {
                continue;
            }

            product.Stock = Math.Max(0, product.Stock - item.Qty);
            product.Sold += item.Qty;
        }
    }

    /// <summary>Money arrived but the parcel cannot be packed — give it straight back.</summary>
    private async Task RefundUnshippableAsync(Order order)
    {
        const string reason = "A jar sold out between checkout and payment.";

        var refund = await _razorpay.RefundAsync(order.RazorpayPaymentId, ToPaise(order.Total), reason);

        order.PaymentStatus = refund.Success ? PaymentStatuses.Refunded : PaymentStatuses.Failed;
        order.Payment = order.PaymentStatus;
        order.FailureReason = refund.Success
            ? reason + " Refunded automatically."
            : reason + " Automatic refund failed — refund from the Razorpay dashboard.";

        if (refund.Success)
        {
            order.RefundedAt = DateTime.UtcNow;
            order.RefundAmount = order.Total;
        }

        await _db.SaveChangesAsync();
        QueueProblemEmails(order.Id, order.FailureReason);
    }

    /// <summary>
    /// Finds the visitor's own half-finished order, if it is young and still unpaid.
    /// Stops a double-submit from creating two orders.
    /// </summary>
    private async Task<Order> FindReusablePendingOrderAsync()
    {
        var number = HttpContext.Session.GetString(PendingOrderSessionKey);

        if (string.IsNullOrWhiteSpace(number))
        {
            return null;
        }

        var order = await _db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderNumber == number);

        if (order == null
            || order.PaymentStatus != PaymentStatuses.Pending
            || order.CreatedAt < DateTime.UtcNow.AddMinutes(-PendingOrderReuseMinutes))
        {
            HttpContext.Session.Remove(PendingOrderSessionKey);
            return null;
        }

        return order;
    }

    /// <summary>
    /// Sends the order emails on a background task with its own DI scope.
    ///
    /// WHY: mail must never slow down, or worse break, a payment response — Razorpay wants its
    /// webhook answered quickly, and the customer should not wait on an SMTP server. The order is
    /// re-read inside the scope so no disposed DbContext is touched.
    /// </summary>
    private void QueuePaidEmails(int orderId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var email = scope.ServiceProvider.GetRequiredService<EmailService>();

                var order = await db.Orders.AsNoTracking()
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null)
                {
                    return;
                }

                await email.SendOrderPaidToOwnerAsync(order);
                await email.SendOrderPaidToCustomerAsync(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Queued order email failed for order {OrderId}.", orderId);
            }
        });
    }

    private void QueueProblemEmails(int orderId, string reason)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var email = scope.ServiceProvider.GetRequiredService<EmailService>();

                var order = await db.Orders.AsNoTracking()
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order != null)
                {
                    await email.SendOrderProblemToOwnerAsync(order, reason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Queued problem email failed for order {OrderId}.", orderId);
            }
        });
    }

    /// <summary>Reads a string property without throwing when it is missing or not a string.</summary>
    private static string GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    /// <summary>A short, stable fingerprint of a webhook body, used when no event id is sent.</summary>
    private static string ShortHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes)[..32].ToLowerInvariant();
    }

    private static string Trim(string value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers that were already here
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Returns to the page the button was pressed on. Only same-site URLs are followed, which
    /// stops the returnUrl parameter from being used as an open redirect.
    /// </summary>
    // saptarshi: added 06-Nov-2026 — did this "Add to basket" post come from the in-page script
    // rather than a plain form submit? Only that header is trusted; the request itself is still
    // checked (antiforgery token, stock, quantity) exactly like a normal post.
    private bool WantsJson()
    {
        return string.Equals(Request.Headers["X-Requested-With"], "fetch", StringComparison.OrdinalIgnoreCase);
    }

    private IActionResult Back(string returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>"A thing + 2 more" — a short, human summary for the orders table.</summary>
    private static string Summarise(List<CartLine> lines)
    {
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var firstName = lines[0].Name;
        var others = lines.Sum(l => l.Qty) - lines[0].Qty;
        return others > 0 ? $"{firstName} + {others} more" : firstName;
    }

    /// <summary>SB-2026-4812 style, checked against the table so it is always unique.</summary>
    private async Task<string> NextOrderNumberAsync()
    {
        var year = DateTime.UtcNow.Year;

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = $"SB-{year}-{RandomNumberGenerator.GetInt32(1000, 10000)}";
            var taken = await _db.Orders.AsNoTracking().AnyAsync(o => o.OrderNumber == candidate);
            if (!taken)
            {
                return candidate;
            }
        }

        // Extremely unlikely fallback: ticks-based suffix.
        return $"SB-{year}-{DateTime.UtcNow.Ticks % 1000000}";
    }
}
