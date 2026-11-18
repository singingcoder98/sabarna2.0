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
                },

                // saptarshi: 16-Nov-2026 — ecommerce SEO: the delivery price a shopper sees in the
                // merchant listing. The numbers come from CartService, so schema and basket can never
                // disagree: ₹49 flat, free above ₹999, India only.
                ["shippingDetails"] = new Dictionary<string, object>
                {
                    ["@type"] = "OfferShippingDetails",
                    ["shippingRate"] = new Dictionary<string, object>
                    {
                        ["@type"] = "MonetaryAmount",
                        ["value"] = "49.00",
                        ["currency"] = "INR"
                    },
                    ["shippingDestination"] = new Dictionary<string, object>
                    {
                        ["@type"] = "DefinedRegion",
                        ["addressCountry"] = "IN"
                    },
                    ["deliveryTime"] = new Dictionary<string, object>
                    {
                        ["@type"] = "ShippingDeliveryTime",
                        ["handlingTime"] = new Dictionary<string, object>
                        {
                            ["@type"] = "QuantitativeValue", ["minValue"] = 1, ["maxValue"] = 3,
                            ["unitCode"] = "DAY"
                        },
                        ["transitTime"] = new Dictionary<string, object>
                        {
                            ["@type"] = "QuantitativeValue", ["minValue"] = 2, ["maxValue"] = 6,
                            ["unitCode"] = "DAY"
                        }
                    }
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

    /// <summary>
    /// saptarshi: 16-Nov-2026 — Organization + WebSite + Store (a LocalBusiness subtype) in one
    /// @graph. This is what lets Google show the shop's name, phone, address and opening city in
    /// the knowledge panel, and what ties every product page to one business instead of a stranger
    /// called "Sabarna Pure" with no address.
    ///
    /// Locality is Madhyamgram, Kolkata 700129 — the owner's actual base — and the area served is
    /// named explicitly, because "Kolkata" and "Barasat" are the words a nearby customer types.
    /// </summary>
    public static string StoreGraph(string brandName, string tagline, string email, string phone,
                                    string address, string origin, string locality, string region,
                                    string postalCode)
    {
        var name = string.IsNullOrWhiteSpace(brandName) ? "Sabarna Pure" : brandName;
        var organizationId = $"{origin}/#organization";
        var storeId = $"{origin}/#store";
        var websiteId = $"{origin}/#website";

        var organization = new Dictionary<string, object>
        {
            ["@type"] = "Organization",
            ["@id"] = organizationId,
            ["name"] = name,
            ["url"] = origin,
            ["logo"] = $"{origin}/images/logo.png",
            ["email"] = email,
            ["telephone"] = phone,
            ["description"] = tagline
        };

        var website = new Dictionary<string, object>
        {
            ["@type"] = "WebSite",
            ["@id"] = websiteId,
            ["url"] = origin,
            ["name"] = name,
            ["inLanguage"] = "en-IN",
            ["publisher"] = new Dictionary<string, object> { ["@id"] = organizationId }
        };

        var store = new Dictionary<string, object>
        {
            // Store is a LocalBusiness subtype; OnlineStore is kept because the shop really does
            // sell online — Google accepts both types on one node.
            ["@type"] = new[] { "Store", "LocalBusiness", "OnlineStore" },
            ["@id"] = storeId,
            ["name"] = name,
            ["description"] = tagline,
            ["url"] = origin,
            ["image"] = $"{origin}/images/og-cover.jpg",
            ["logo"] = $"{origin}/images/logo.png",
            ["email"] = email,
            ["telephone"] = phone,
            ["slogan"] = tagline,
            ["knowsAbout"] = new[]
            {
                "Bengali achar", "kuler achar", "amsi achar", "aam achar", "mixed pickle",
                "dal bori (daler bori)", "Sundarban honey", "Bengali home food"
            },
            ["priceRange"] = "₹89–₹189",
            ["currenciesAccepted"] = "INR",
            ["paymentAccepted"] = "UPI, Credit Card, Debit Card, Netbanking",
            ["address"] = new Dictionary<string, object>
            {
                ["@type"] = "PostalAddress",
                ["streetAddress"] = address,
                ["addressLocality"] = locality,
                ["addressRegion"] = region,
                ["postalCode"] = postalCode,
                ["addressCountry"] = "IN"
            },
            ["areaServed"] = new object[]
            {
                new Dictionary<string, object> { ["@type"] = "City", ["name"] = "Kolkata" },
                new Dictionary<string, object> { ["@type"] = "City", ["name"] = "Madhyamgram" },
                new Dictionary<string, object> { ["@type"] = "City", ["name"] = "Barasat" },
                new Dictionary<string, object> { ["@type"] = "State", ["name"] = "West Bengal" },
                new Dictionary<string, object> { ["@type"] = "Country", ["name"] = "India" }
            },
            ["parentOrganization"] = new Dictionary<string, object> { ["@id"] = organizationId }
        };

        var graph = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = new object[] { organization, website, store }
        };

        return JsonSerializer.Serialize(graph, Options);
    }

    /// <summary>The shop listing, as an ordered list of the jars on the shelf.</summary>
    public static string ProductList(IReadOnlyList<Product> products, string origin, string name)
    {
        var items = products
            .Select((product, index) => new Dictionary<string, object>
            {
                ["@type"] = "ListItem",
                ["position"] = index + 1,
                // saptarshi: 16-Nov-2026 — the clean product URL, same as everywhere else.
                ["url"] = $"{origin}/product/{product.SlugOrId}",
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
