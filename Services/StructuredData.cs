using System.Globalization;
using System.Text.Json;
using Sabarna.Web.Models;

namespace Sabarna.Web.Services;

/// <summary>
/// Builds the JSON-LD blocks that go into &lt;script type="application/ld+json"&gt;.
///
/// Keeping them here (instead of inline in a view) means the shop's structured data is written
/// once, stays valid JSON — System.Text.Json escapes anything dangerous — and can be reasoned
/// about without reading Razor. Views call the methods and wrap the result in Html.Raw.
/// </summary>
public static class StructuredData
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// A rich Results-eligible Product: name, image, price, currency and availability.
    /// Google needs at least name + offers to show a price in the search result.
    /// </summary>
    public static string Product(Product product, string storeName, string pageUrl, string absoluteImageUrl)
    {
        var brand = string.IsNullOrWhiteSpace(storeName) ? "Sabarna Pure" : storeName;

        var productSchema = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["name"] = product.Name,
            ["description"] = string.IsNullOrWhiteSpace(product.Description)
                ? product.Name
                : product.Description,
            ["image"] = new[] { absoluteImageUrl },
            ["url"] = pageUrl,
            ["sku"] = $"SB-{product.Id:D4}",
            ["category"] = product.Category,
            ["brand"] = new Dictionary<string, object>
            {
                ["@type"] = "Brand",
                ["name"] = brand
            },
            ["offers"] = new Dictionary<string, object>
            {
                ["@type"] = "Offer",
                ["url"] = pageUrl,
                ["priceCurrency"] = "INR",
                ["price"] = product.Price.ToString("0.00", CultureInfo.InvariantCulture),
                ["itemCondition"] = "https://schema.org/NewCondition",
                ["availability"] = product.InStock
                    ? "https://schema.org/InStock"
                    : "https://schema.org/OutOfStock",
                ["seller"] = new Dictionary<string, object>
                {
                    ["@type"] = "Organization",
                    ["name"] = brand
                }
            }
        };

        // Only claimed when the shop actually holds reviews; an empty rating is worse than none.
        if (product.Reviews > 0 && product.Rating > 0)
        {
            productSchema["aggregateRating"] = new Dictionary<string, object>
            {
                ["@type"] = "AggregateRating",
                ["ratingValue"] = product.Rating.ToString("0.0", CultureInfo.InvariantCulture),
                ["reviewCount"] = product.Reviews,
                ["bestRating"] = 5,
                ["worstRating"] = 1
            };
        }

        return JsonSerializer.Serialize(productSchema, Options);
    }

    /// <summary>
    /// BreadcrumbList, mirroring the visible trail on the page. Google replaces the URL in the
    /// result with the breadcrumb, which reads far better than a raw path.
    /// </summary>
    public static string Breadcrumbs(IReadOnlyList<(string Name, string Url)> trail)
    {
        var items = trail
            .Select((crumb, index) => new Dictionary<string, object>
            {
                ["@type"] = "ListItem",
                ["position"] = index + 1,
                ["name"] = crumb.Name,
                ["item"] = crumb.Url
            })
            .ToList();

        var schema = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = items
        };

        return JsonSerializer.Serialize(schema, Options);
    }

    /// <summary>The shop listing, as an ordered list of the jars on the shelf.</summary>
    public static string ProductList(IReadOnlyList<Product> products, string origin, string name)
    {
        var items = products
            .Select((product, index) => new Dictionary<string, object>
            {
                ["@type"] = "ListItem",
                ["position"] = index + 1,
                ["url"] = $"{origin}/Shop/Details/{product.Id}",
                ["name"] = product.Name
            })
            .ToList();

        var schema = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "ItemList",
            ["name"] = name,
            ["itemListElement"] = items
        };

        return JsonSerializer.Serialize(schema, Options);
    }
}
