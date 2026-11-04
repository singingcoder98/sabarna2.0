namespace Matibari.Web.Models;

// =============================================================================================
// VIEW MODELS — the exact shape each screen needs. Controllers fill these in and pass them to
// the Razor views with @model, so a view can never accidentally reach into the database itself.
// =============================================================================================

/// <summary>Everything the home page needs in one object.</summary>
public class HomeViewModel
{
    /// <summary>Brand name, hero copy and contact details (edited in /Admin/Settings).</summary>
    public SiteSetting Settings { get; set; } = new();

    /// <summary>Visible, non-combo products — the "Straight from the farm" grid.</summary>
    public List<Product> Products { get; set; } = new();

    /// <summary>Visible combos / bundles — the dark "special combos" band.</summary>
    public List<Product> Combos { get; set; } = new();

    /// <summary>Products marked "Featured" (used for the small hero trust strip).</summary>
    public List<Product> Featured { get; set; } = new();

    /// <summary>How many parcels have been delivered — a real number, not a marketing invention.</summary>
    public int OrdersDelivered { get; set; }

    /// <summary>How many products are on sale right now.</summary>
    public int ProductsOnline { get; set; }
}

/// <summary>The shop listing with its category filter pills.</summary>
public class ShopViewModel
{
    public List<Product> Products { get; set; } = new();

    /// <summary>Categories that actually have visible products.</summary>
    public List<string> Categories { get; set; } = new();

    /// <summary>The category currently filtered on, or null/empty for "All".</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Total number of visible products, ignoring the filter.</summary>
    public int TotalCount { get; set; }
}

/// <summary>One product page: the product itself, its combo contents and three suggestions.</summary>
public class ProductDetailsViewModel
{
    public Product Product { get; set; } = new();

    /// <summary>Parsed contents of a combo — an empty list for ordinary products.</summary>
    public List<ComboItemDto> ComboItems { get; set; } = new();

    /// <summary>Three "you may also like" products from the same category.</summary>
    public List<Product> Related { get; set; } = new();
}

/// <summary>The numbers and tables shown on the admin dashboard.</summary>
public class AdminDashboardViewModel
{
    public decimal Revenue { get; set; }

    public int OrderCount { get; set; }

    /// <summary>Orders that still need packing or dispatch.</summary>
    public int OpenOrders { get; set; }

    public int ProductsLive { get; set; }

    public int ProductsTotal { get; set; }

    /// <summary>Products with 10 or fewer jars left on the shelf.</summary>
    public int LowStockCount { get; set; }

    public List<Order> RecentOrders { get; set; } = new();

    public List<Product> LowStockProducts { get; set; } = new();
}

/// <summary>
/// The contact page needs two things at once: the shop's details for the side panel, and the
/// form the visitor is filling in. This small wrapper keeps the view strongly typed for both.
/// </summary>
public class ContactPageModel
{
    public SiteSetting Settings { get; set; } = new();

    public ContactViewModel Form { get; set; } = new();
}
