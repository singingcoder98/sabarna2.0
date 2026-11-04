using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Matibari.Web.Services;

namespace Matibari.Web.Models;

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

    /// <summary>"Paid" today — a real gateway will keep this truthful once Razorpay is wired up.</summary>
    [StringLength(30)]
    public string Payment { get; set; } = "Paid";

    /// <summary>UPI, Card or Netbanking. Cash on delivery is not offered anywhere.</summary>
    [StringLength(30)]
    public string PaymentMethod { get; set; } = "UPI";

    /// <summary>Processing -> Packed -> Shipped -> Delivered.</summary>
    [StringLength(30)]
    public string Status { get; set; } = OrderStatuses.Processing;

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
