using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Sabarna.Web.Services;

namespace Sabarna.Web.Models;

// =============================================================================================
// ENTITIES — one class per database table. Every property is explained in a short comment so a
// non-technical shop owner can follow what the admin panels are actually storing.
// =============================================================================================

/// <summary>
/// A jar of ghee, a bottle of honey, a pickle or a curated combo box.
/// </summary>
public class Product
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Please give the product a name.")]
    [StringLength(160)]
    [Display(Name = "Product name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please pick a category.")]
    [StringLength(60)]
    [Display(Name = "Category")]
    public string Category { get; set; } = string.Empty;

    [Range(1, 1000000, ErrorMessage = "The selling price must be at least ₹1.")]
    [Display(Name = "Selling price (₹)")]
    public decimal Price { get; set; }

    [Range(0, 1000000, ErrorMessage = "The MRP cannot be negative.")]
    [Display(Name = "MRP / struck-through price (₹)")]
    public decimal Mrp { get; set; }

    [StringLength(40)]
    [Display(Name = "Unit (e.g. 500 ml jar)")]
    public string Unit { get; set; } = string.Empty;

    [Range(0, 5)]
    [Display(Name = "Rating out of 5")]
    public double Rating { get; set; }

    [Range(0, 100000)]
    [Display(Name = "Number of reviews")]
    public int Reviews { get; set; }

    [StringLength(300)]
    [Display(Name = "Image path")]
    public string Image { get; set; } = string.Empty;

    [StringLength(60)]
    [Display(Name = "Corner tag (e.g. Best seller)")]
    public string Tag { get; set; } = string.Empty;

    [StringLength(2000)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Visible in the shop")]
    public bool Active { get; set; } = true;

    [Range(0, 100000)]
    [Display(Name = "Stock on hand")]
    public int Stock { get; set; }

    [Display(Name = "Units sold")]
    public int Sold { get; set; }

    [Display(Name = "This product is a combo / bundle")]
    public bool IsCombo { get; set; }

    [Display(Name = "Feature on the home page")]
    public bool Featured { get; set; }

    /// <summary>
    /// Only used when <see cref="IsCombo"/> is true: a JSON array such as
    /// [{"name":"A2 Bilona Cow Ghee","qty":"500 ml","image":"/images/products/ghee-500.jpg"}].
    /// Stored as text so a combo can be edited without extra tables.
    /// </summary>
    [Display(Name = "What's inside the combo (JSON)")]
    public string ComboItemsJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // ---- Values worked out in code, never saved to the database ---------------------------------

    /// <summary>True when an image path has been filled in.</summary>
    [NotMapped]
    public bool HasImage => !string.IsNullOrWhiteSpace(Image);

    /// <summary>True while there is at least one jar on the shelf.</summary>
    [NotMapped]
    public bool InStock => Stock > 0;

    /// <summary>True when the MRP is higher than the selling price (so a "save" badge makes sense).</summary>
    [NotMapped]
    public bool OnSale => Mrp > Price;

    /// <summary>Rounded discount percentage, e.g. 14 for a ₹1,049 MRP against a ₹899 price.</summary>
    [NotMapped]
    public int SavePercent => Mrp > Price && Mrp > 0
        ? (int)Math.Round((Mrp - Price) / Mrp * 100, MidpointRounding.AwayFromZero)
        : 0;
}

/// <summary>
/// The one row that holds the shop's identity: brand name, hero text and contact details.
/// Editing it in the admin panel changes the whole site instantly.
/// </summary>
public class SiteSetting
{
    public int Id { get; set; }

    [Required]
    [StringLength(80)]
    [Display(Name = "Brand name")]
    public string BrandName { get; set; } = string.Empty;

    [StringLength(120)]
    [Display(Name = "Tagline")]
    public string Tagline { get; set; } = string.Empty;

    [StringLength(300)]
    [Display(Name = "Logo image path")]
    public string Logo { get; set; } = string.Empty;

    [StringLength(160)]
    [Display(Name = "Public email")]
    public string Email { get; set; } = string.Empty;

    [StringLength(40)]
    [Display(Name = "Phone number")]
    public string Phone { get; set; } = string.Empty;

    /// <summary>Digits only, including the country code, e.g. 919800012345.</summary>
    [StringLength(20)]
    [Display(Name = "WhatsApp number (digits only, with country code)")]
    public string Whatsapp { get; set; } = string.Empty;

    [StringLength(300)]
    [Display(Name = "Address")]
    public string Address { get; set; } = string.Empty;

    [StringLength(120)]
    [Display(Name = "Hero kicker")]
    public string HeroKicker { get; set; } = string.Empty;

    [StringLength(160)]
    [Display(Name = "Hero headline — first line")]
    public string HeroTitle1 { get; set; } = string.Empty;

    [StringLength(160)]
    [Display(Name = "Hero headline — second line")]
    public string HeroTitle2 { get; set; } = string.Empty;

    [StringLength(600)]
    [Display(Name = "Hero introduction")]
    public string HeroText { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }

    // ---- Helpers used by the views --------------------------------------------------------------

    /// <summary>Builds a wa.me link from whatever the owner typed into the WhatsApp field.</summary>
    [NotMapped]
    public string WhatsappLink
    {
        get
        {
            var digits = new string((Whatsapp ?? string.Empty).Where(char.IsDigit).ToArray());
            return digits.Length == 0 ? string.Empty : "https://wa.me/" + digits;
        }
    }

    /// <summary>Builds a tel: link that works on phones.</summary>
    [NotMapped]
    public string PhoneLink
    {
        get
        {
            var cleaned = new string((Phone ?? string.Empty)
                .Where(c => char.IsDigit(c) || c == '+').ToArray());
            return cleaned.Length == 0 ? string.Empty : "tel:" + cleaned;
        }
    }
}

/// <summary>
/// The shop owner (or staff) who may open /Admin. Passwords are stored only as an
/// ASP.NET Core Identity hash — never as plain text.
/// </summary>
public class AdminUser
{
    public int Id { get; set; }

    [Required]
    [StringLength(160)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(40)]
    public string Role { get; set; } = "admin";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// saptarshi: Phase 0 (12-Nov-2026) — true while this account is still using the password that
    /// the shop was seeded with, which is printed in the project documentation. The admin panel
    /// refuses to open anything except the password form until it is changed.
    /// </summary>
    public bool MustChangePassword { get; set; }
}

/// <summary>
/// A short-lived one-time code created after the password step. Only the SHA-256 hash is kept,
/// so a leaked database backup cannot be used to log in.
/// </summary>
public class OtpChallenge
{
    public int Id { get; set; }

    public int AdminId { get; set; }

    [Required]
    public string CodeHash { get; set; } = string.Empty;

    public int Attempts { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool Used { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// One placed order. Kept deliberately flat (no line-item table) because the shop's basket is
/// small and the owner only needs to know what to pack, for whom, and whether it is paid.
/// </summary>
public class Order
{
    public int Id { get; set; }

    [Required]
    [StringLength(40)]
    public string OrderNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string Customer { get; set; } = string.Empty;

    [StringLength(160)]
    public string Email { get; set; } = string.Empty;

    public int ItemCount { get; set; }

    [StringLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [StringLength(300)]
    public string ProductImage { get; set; } = string.Empty;

    public decimal Total { get; set; }

    /// <summary>
    /// saptarshi: changed 06-Nov-2026 — used to default to "Paid", which told a lie the moment a
    /// Razorpay payment could fail. Now an order starts as Pending and only becomes Paid after a
    /// verified signature or a verified webhook.
    /// </summary>
    [StringLength(30)]
    public string Payment { get; set; } = PaymentStatuses.Pending;

    /// <summary>UPI, Card or Netbanking. Cash on delivery is not offered anywhere.</summary>
    [StringLength(30)]
    public string PaymentMethod { get; set; } = "UPI";

    /// <summary>Processing -> Packed -> Shipped -> Delivered.</summary>
    [StringLength(30)]
    public string Status { get; set; } = OrderStatuses.Processing;

    public DateTime CreatedAt { get; set; }

    // =============================================================================================
    // saptarshi: added 06-Nov-2026 — everything below is new.
    //
    // WHY: the checkout form collected a phone number and a delivery address, and the order never
    // stored them, so a real parcel could not be shipped. These columns are added to the live
    // database by Data/SchemaPatcher.cs (ALTER TABLE ADD COLUMN), never by dropping anything.
    // =============================================================================================

    /// <summary>Where the parcel goes.</summary>
    [StringLength(400)]
    public string Address { get; set; } = string.Empty;

    /// <summary>The 10-digit mobile the courier will call.</summary>
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    /// <summary>Anything the customer typed in the "delivery note" box.</summary>
    [StringLength(300)]
    public string Note { get; set; } = string.Empty;

    // ---- Razorpay -------------------------------------------------------------------------------
    // The order id Razorpay gives us (order_XXXX). The browser is allowed to know this one.
    [StringLength(60)]
    public string RazorpayOrderId { get; set; } = string.Empty;

    // The payment id (pay_XXXX). Only ever written after a signature check.
    [StringLength(60)]
    public string RazorpayPaymentId { get; set; } = string.Empty;

    // The signature Razorpay sent back; kept as evidence for a dispute.
    [StringLength(200)]
    public string RazorpaySignature { get; set; } = string.Empty;

    /// <summary>Pending / Paid / Failed / Refunded — see <see cref="PaymentStatuses"/>.</summary>
    [StringLength(20)]
    public string PaymentStatus { get; set; } = PaymentStatuses.Pending;

    /// <summary>"browser" (verified return) or "webhook" — how we learned it was paid.</summary>
    [StringLength(20)]
    public string PaymentVerifiedBy { get; set; } = string.Empty;

    /// <summary>When the money was confirmed. Null while unpaid.</summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>Razorpay's own words when a payment fails ("insufficient funds" etc).</summary>
    [StringLength(200)]
    public string FailureReason { get; set; } = string.Empty;

    public DateTime? RefundedAt { get; set; }

    /// <summary>How much of the money went back. Zero means nothing was refunded.</summary>
    public decimal RefundAmount { get; set; }

    // ---- GST placeholders -----------------------------------------------------------------------
    // Left at zero until the shop is registered for GST. The invoice hides the tax block while
    // TaxAmount is zero, so nothing changes for the customer today and nothing has to be migrated
    // later when GSTIN arrives.
    public decimal TaxAmount { get; set; }

    [StringLength(20)]
    public string Gstin { get; set; } = string.Empty;

    [StringLength(30)]
    public string InvoiceNumber { get; set; } = string.Empty;

    // ---- SkyKing courier ------------------------------------------------------------------------
    // Filled in when SkyKing returns an AWB (docket) number. The public tracking page reads these.
    [StringLength(40)]
    public string CourierName { get; set; } = string.Empty;

    [StringLength(60)]
    public string AwbNumber { get; set; } = string.Empty;

    public DateTime? ShippedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    /// <summary>The actual jars on this order. Saved so the parcel can be packed and billed.</summary>
    public List<OrderItem> Items { get; set; } = new();
}

/// <summary>
/// saptarshi: added 06-Nov-2026 — one row per product on an order.
///
/// WHY: the old order only kept a summary string ("A2 Ghee + 2 more"), so nobody could see what was
/// actually inside the parcel. Prices are copied at the moment of the order, so a later price change
/// never rewrites history.
/// </summary>
public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public int ProductId { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public int Qty { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}

/// <summary>
/// saptarshi: added 06-Nov-2026 — the four payment states, plus the pill colours the admin uses.
/// The old <see cref="Order.Payment"/> text column is still written alongside these, so every
/// existing screen keeps working without being touched.
/// </summary>
public static class PaymentStatuses
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Failed = "Failed";
    public const string Refunded = "Refunded";

    public static string PillClass(string current)
    {
        return current switch
        {
            Paid => "pill-green",
            Pending => "pill-amber",
            Failed => "pill-red",
            Refunded => "pill-grey",
            _ => "pill-grey"
        };
    }
}

/// <summary>
/// saptarshi: added 06-Nov-2026 — every webhook Razorpay sends, kept as evidence.
///
/// WHY: if a customer ever says "I paid but my order is not confirmed", this table is the proof,
/// and it makes the handler idempotent (the same event id is never processed twice).
/// </summary>
public class PaymentEvent
{
    public int Id { get; set; }

    /// <summary>Razorpay's event id (event_XXXX). Unique per delivery attempt.</summary>
    [StringLength(120)]
    public string EventId { get; set; } = string.Empty;

    [StringLength(60)]
    public string EventName { get; set; } = string.Empty;

    [StringLength(60)]
    public string OrderNumber { get; set; } = string.Empty;

    [StringLength(60)]
    public string PaymentId { get; set; } = string.Empty;

    public bool SignatureVerified { get; set; }

    /// <summary>The raw JSON body, so nothing is lost. Never contains our own secrets.</summary>
    [StringLength(8000)]
    public string Payload { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

/// <summary>The four order states, in the order they happen.</summary>
public static class OrderStatuses
{
    public const string Processing = "Processing";
    public const string Packed = "Packed";
    public const string Shipped = "Shipped";
    public const string Delivered = "Delivered";

    public static readonly string[] All = { Processing, Packed, Shipped, Delivered };

    /// <summary>Returns the next status, or the same one when the order is already delivered.</summary>
    public static string Next(string current)
    {
        return current switch
        {
            Processing => Packed,
            Packed => Shipped,
            Shipped => Delivered,
            _ => Delivered
        };
    }

    /// <summary>Maps a status to the pill colour used in the tables.</summary>
    public static string PillClass(string current)
    {
        return current switch
        {
            Processing => "pill-amber",
            Packed => "pill-green",
            Shipped => "pill-green",
            Delivered => "pill-grey",
            _ => "pill-grey"
        };
    }
}

// =============================================================================================
// VIEW-ONLY MODELS (also live here because they describe the shop's data shape)
// =============================================================================================

/// <summary>One item inside a combo box, as stored in <see cref="Product.ComboItemsJson"/>.</summary>
public class ComboItemDto
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Free text such as "500 ml" or "1 jar".</summary>
    public string Qty { get; set; } = string.Empty;

    public string Image { get; set; } = string.Empty;
}

/// <summary>
/// A single line of the shopping basket. The whole basket is serialised to JSON inside the
/// session, which is why this class has no navigation properties.
/// </summary>
public class CartLine
{
    public int ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public string Image { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int Qty { get; set; }

    public bool IsCombo { get; set; }

    /// <summary>Price x quantity. Not stored in JSON — always recomputed from the live price.</summary>
    [JsonIgnore]
    public decimal LineTotal => Price * Qty;
}

/// <summary>Fields of the checkout form plus the basket that is being paid for.</summary>
public class CheckoutViewModel
{
    [Required(ErrorMessage = "Please tell us who the parcel is for.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "That name looks too short.")]
    [Display(Name = "Full name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "We need an email to send the order confirmation.")]
    [EmailAddress(ErrorMessage = "That email address does not look right.")]
    [StringLength(160)]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please add a mobile number — our courier will call before delivery.")]
    [RegularExpression(@"^[6-9][0-9]{9}$", ErrorMessage = "Enter a 10-digit Indian mobile number, e.g. 9800012345.")]
    [Display(Name = "Mobile number")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please paste the full delivery address.")]
    [StringLength(400, MinimumLength = 10, ErrorMessage = "Please add a little more detail (house, street, city, PIN).")]
    [Display(Name = "Delivery address")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose how you would like to pay.")]
    [Display(Name = "Payment method")]
    public string PaymentMethod { get; set; } = "UPI";

    [StringLength(300)]
    [Display(Name = "Delivery note (optional)")]
    public string Note { get; set; } = string.Empty;

    /// <summary>Filled in by the controller, never posted back by the browser.</summary>
    public List<CartLine> Lines { get; set; } = new();

    public decimal Subtotal => Lines.Sum(line => line.LineTotal);

    public decimal Shipping => CartService.ShippingFor(Subtotal);

    public decimal Total => Subtotal + Shipping;
}

/// <summary>The first step of the admin login (email + password).</summary>
public class AdminLoginViewModel
{
    [Required(ErrorMessage = "Please enter your email.")]
    [EmailAddress(ErrorMessage = "That email address does not look right.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>The second step of the admin login (the 6-digit one-time code).</summary>
public class OtpViewModel
{
    [Required(ErrorMessage = "Enter the 6-digit code.")]
    [RegularExpression(@"^[0-9]{6}$", ErrorMessage = "The code is exactly 6 digits.")]
    [Display(Name = "One-time code")]
    public string Code { get; set; } = string.Empty;
}

/// <summary>The public contact form.</summary>
public class ContactViewModel
{
    [Required(ErrorMessage = "Please tell us your name.")]
    [StringLength(80, MinimumLength = 2)]
    [Display(Name = "Your name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please add an email so we can reply.")]
    [EmailAddress(ErrorMessage = "That email address does not look right.")]
    [StringLength(160)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [RegularExpression(@"^[6-9][0-9]{9}$", ErrorMessage = "Enter a 10-digit Indian mobile number.")]
    [Display(Name = "Mobile number (optional)")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose a subject.")]
    [StringLength(120)]
    [Display(Name = "Subject")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please write your message.")]
    [StringLength(1500, MinimumLength = 10, ErrorMessage = "A few more words would help us answer properly.")]
    [Display(Name = "Message")]
    public string Message { get; set; } = string.Empty;
}
