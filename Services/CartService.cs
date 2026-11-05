using System.Text.Json;
using Sabarna.Web.Models;
using Microsoft.AspNetCore.Http;

namespace Sabarna.Web.Services;

/// <summary>
/// The shopping basket. It lives in the visitor's session as a small JSON string, which means:
///   • no login is needed to shop,
///   • the basket survives a page refresh or a closed tab (7-day session),
///   • nothing is written to the database until an order is actually placed.
/// </summary>
public class CartService
{
    // ---- Business rules, in one place so the cart, the checkout and the order agree ------------
    public const int MinQty = 1;
    public const int MaxQty = 20;
    public const decimal FreeShippingThreshold = 999m;
    public const decimal ShippingFee = 49m;

    private const string SessionKey = "sabarna.cart";

    /// <summary>Case-insensitive JSON (matches the camelCase we store in the session).</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CartService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // ---------------------------------------------------------------------------------------------
    // Reading
    // ---------------------------------------------------------------------------------------------

    /// <summary>Every line in the basket, in the order the visitor added them.</summary>
    public List<CartLine> GetLines()
    {
        var json = GetSession().GetString(SessionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<CartLine>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<CartLine>>(json, JsonOptions) ?? new List<CartLine>();
        }
        catch (JsonException)
        {
            // A corrupted/old cart should never break the site — start again cleanly.
            return new List<CartLine>();
        }
    }

    /// <summary>Total number of jars (not lines) in the basket — used by the navbar badge.</summary>
    public int Count() => GetLines().Sum(line => line.Qty);

    /// <summary>Value of the goods, before shipping.</summary>
    public decimal Total() => GetLines().Sum(line => line.LineTotal);

    /// <summary>Free above ₹999, otherwise a flat ₹49 courier charge.</summary>
    public static decimal ShippingFor(decimal subtotal)
    {
        if (subtotal <= 0)
        {
            return 0m;
        }

        return subtotal >= FreeShippingThreshold ? 0m : ShippingFee;
    }

    // ---------------------------------------------------------------------------------------------
    // Writing
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Adds a product, clamping the quantity to 1..20 and never exceeding the stock on hand.
    /// Out-of-stock products are silently refused here (the controllers warn the visitor first).
    /// </summary>
    public void Add(Product product, int quantity)
    {
        if (product == null || product.Stock <= 0)
        {
            return;
        }

        var lines = GetLines();
        var requested = Math.Clamp(quantity, MinQty, MaxQty);
        var ceiling = Math.Min(product.Stock, MaxQty);
        var line = lines.FirstOrDefault(l => l.ProductId == product.Id);

        if (line == null)
        {
            lines.Add(new CartLine
            {
                ProductId = product.Id,
                Name = product.Name,
                Unit = product.Unit,
                Image = product.Image,
                Price = product.Price,
                Qty = Math.Clamp(requested, MinQty, ceiling),
                IsCombo = product.IsCombo
            });
        }
        else
        {
            // Keep the line fresh in case the owner edited the price or the name.
            line.Name = product.Name;
            line.Unit = product.Unit;
            line.Image = product.Image;
            line.Price = product.Price;
            line.IsCombo = product.IsCombo;
            line.Qty = Math.Clamp(line.Qty + requested, MinQty, ceiling);
        }

        Save(lines);
    }

    /// <summary>Sets an exact quantity. A quantity of zero (or less) removes the line.</summary>
    public void SetQty(int productId, int quantity)
    {
        var lines = GetLines();
        var line = lines.FirstOrDefault(l => l.ProductId == productId);
        if (line == null)
        {
            return;
        }

        if (quantity <= 0)
        {
            lines.Remove(line);
        }
        else
        {
            line.Qty = Math.Clamp(quantity, MinQty, MaxQty);
        }

        Save(lines);
    }

    public void Remove(int productId)
    {
        var lines = GetLines();
        var line = lines.FirstOrDefault(l => l.ProductId == productId);
        if (line == null)
        {
            return;
        }

        lines.Remove(line);
        Save(lines);
    }

    /// <summary>Empties the basket — called once an order has been placed.</summary>
    public void Clear() => GetSession().Remove(SessionKey);

    // ---------------------------------------------------------------------------------------------
    // Combo helpers
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Reads the "what's inside" list of a combo product. Returns an empty list for ordinary
    /// products (and for a combo whose JSON has been typed badly).
    /// </summary>
    public static List<ComboItemDto> GetComboItems(Product product)
    {
        if (product == null || string.IsNullOrWhiteSpace(product.ComboItemsJson))
        {
            return new List<ComboItemDto>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<ComboItemDto>>(product.ComboItemsJson, JsonOptions)
                   ?? new List<ComboItemDto>();
        }
        catch (JsonException)
        {
            return new List<ComboItemDto>();
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Internals
    // ---------------------------------------------------------------------------------------------

    private void Save(List<CartLine> lines)
    {
        if (lines.Count == 0)
        {
            Clear();
            return;
        }

        GetSession().SetString(SessionKey, JsonSerializer.Serialize(lines, JsonOptions));
    }

    private ISession GetSession()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            throw new InvalidOperationException("The cart needs an active HTTP request (session is unavailable).");
        }

        return context.Session;
    }
}
