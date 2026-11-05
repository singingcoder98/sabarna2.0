using System.Security.Cryptography;
using Sabarna.Web.Data;
using Sabarna.Web.Models;
using Sabarna.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Sabarna.Web.Controllers;

/// <summary>
/// The two-step checkout: basket -> delivery details -> confirmation.
/// No login is required; the basket lives in the session (see <see cref="CartService"/>).
/// </summary>
public class CartController : Controller
{
    /// <summary>The only payment methods we accept. Cash on delivery is not an option anywhere.</summary>
    private static readonly string[] AllowedPaymentMethods = { "UPI", "Card", "Netbanking" };

    private readonly CartService _cart;
    private readonly AppDbContext _db;

    public CartController(CartService cart, AppDbContext db)
    {
        _cart = cart;
        _db = db;
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

        if (product == null || !product.Active)
        {
            TempData["Error"] = "That jar is no longer on the shelf. Have a look at the rest of the shop.";
            return RedirectToAction(nameof(Index));
        }

        if (product.Stock <= 0)
        {
            TempData["Error"] = $"{product.Name} is out of stock right now. We restock every fortnight.";
            return Back(returnUrl);
        }

        _cart.Add(product, qty);
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
    // POST /Cart/Checkout — validates on the server, then creates the order
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

        // -----------------------------------------------------------------------------------------
        // TODO (deferred): PAYMENT GATEWAY — this is the single place to add Razorpay.
        // Create the Razorpay order here, redirect to its checkout, and only then save the order
        // with Payment = "Paid". Right now the order is simply recorded as paid so the rest of the
        // flow (confirmation page, admin dashboard, stock) can be finished and tested.
        // -----------------------------------------------------------------------------------------
        var order = new Order
        {
            OrderNumber = await NextOrderNumberAsync(),
            Customer = model.Name.Trim(),
            Email = model.Email.Trim(),
            ItemCount = lines.Sum(l => l.Qty),
            ProductName = Summarise(lines),
            ProductImage = lines[0].Image,
            Total = model.Total,
            Payment = "Paid",
            PaymentMethod = model.PaymentMethod,
            Status = OrderStatuses.Processing,
            CreatedAt = DateTime.UtcNow
        };

        // Take the jars off the shelf so the shop cannot oversell.
        foreach (var line in lines)
        {
            var product = products.FirstOrDefault(p => p.Id == line.ProductId);
            if (product == null)
            {
                continue;
            }

            product.Stock = Math.Max(0, product.Stock - line.Qty);
            product.Sold += line.Qty;
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        _cart.Clear();   // the basket is emptied only after the order is safely saved

        return RedirectToAction(nameof(Success), new { orderNumber = order.OrderNumber });
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

    // ---------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Returns to the page the button was pressed on. Only same-site URLs are followed, which
    /// stops the returnUrl parameter from being used as an open redirect.
    /// </summary>
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

    /// <summary>MB-2026-4812 style, checked against the table so it is always unique.</summary>
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
