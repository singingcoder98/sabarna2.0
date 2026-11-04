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
    /// <summary>Money actually collected — orders marked "Paid".</summary>
    public decimal Revenue { get; set; }

    /// <summary>How many of those paid orders there were (the average is built from this).</summary>
    public int PaidOrderCount { get; set; }

    /// <summary>Revenue divided by paid orders, computed here so the view stays dumb.</summary>
    public decimal AverageOrderValue { get; set; }

    public int OrderCount { get; set; }

    /// <summary>Orders that still need packing or dispatch.</summary>
    public int OpenOrders { get; set; }

    /// <summary>What the parcels still in the workshop are worth.</summary>
    public decimal OpenValue { get; set; }

    public int TodayOrderCount { get; set; }

    public decimal TodayRevenue { get; set; }

    public int ProductsLive { get; set; }

    public int ProductsTotal { get; set; }

    /// <summary>Products with 10 or fewer jars left on the shelf.</summary>
    public int LowStockCount { get; set; }

    public List<Order> RecentOrders { get; set; } = new();

    public List<Product> LowStockProducts { get; set; } = new();

    /// <summary>Seven days of takings, oldest first, for the little bar chart.</summary>
    public List<AdminDaySales> SalesLast7Days { get; set; } = new();

    /// <summary>What actually walks out of the door, by revenue.</summary>
    public List<AdminTopProduct> TopProducts { get; set; } = new();
}

/// <summary>One bar of the seven-day chart.</summary>
public class AdminDaySales
{
    public DateTime Day { get; set; }

    public decimal Revenue { get; set; }

    public int Orders { get; set; }

    /// <summary>Bar height as a percentage of the best day, so the tallest bar is always full.</summary>
    public int Height { get; set; }

    /// <summary>True for today, so the last bar can be highlighted.</summary>
    public bool IsToday { get; set; }
}

/// <summary>One line of the "what sells" list.</summary>
public class AdminTopProduct
{
    public string Name { get; set; } = string.Empty;

    public int Jars { get; set; }

    public decimal Revenue { get; set; }

    /// <summary>Width of the inline bar, as a percentage of the best seller.</summary>
    public int Share { get; set; }
}

/// <summary>The orders screen: the rows plus everything its filter bar needs.</summary>
public class AdminOrdersViewModel
{
    public List<Order> Orders { get; set; } = new();

    /// <summary>"All" or one of OrderStatuses.All.</summary>
    public string Status { get; set; } = "All";

    /// <summary>Whatever the owner typed in the search box.</summary>
    public string Search { get; set; } = string.Empty;

    /// <summary>Orders in the current filter (says nothing about the search term).</summary>
    public int FilteredCount { get; set; }

    /// <summary>Value of the orders currently on screen.</summary>
    public decimal FilteredValue { get; set; }

    public int TotalCount { get; set; }

    /// <summary>How many orders sit in each status, for the filter pills.</summary>
    public Dictionary<string, int> CountsByStatus { get; set; } = new();
}

/// <summary>The products screen: the rows plus the stock/visibility summary above them.</summary>
public class AdminProductsViewModel
{
    public List<Product> Products { get; set; } = new();

    public string Search { get; set; } = string.Empty;

    public int TotalCount { get; set; }

    public int ActiveCount { get; set; }

    public int HiddenCount { get; set; }

    public int OutOfStockCount { get; set; }

    /// <summary>Jars on the shelf across the whole catalogue.</summary>
    public int TotalUnits { get; set; }
}

/// <summary>The customers screen, built from the order book — there is no customer account table.</summary>
public class AdminCustomersViewModel
{
    public List<AdminCustomerRow> Customers { get; set; } = new();

    /// <summary>People who have ordered more than once.</summary>
    public int ReturningCount { get; set; }

    /// <summary>Everything everyone has spent, all time.</summary>
    public decimal Lifetime { get; set; }

    public string Search { get; set; } = string.Empty;
}

/// <summary>One person, rolled up from their orders.</summary>
public class AdminCustomerRow
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public int Orders { get; set; }

    public int Jars { get; set; }

    public decimal Spent { get; set; }

    public DateTime LastOrderAt { get; set; }

    /// <summary>True once someone has ordered twice — worth a thank-you note.</summary>
    public bool IsReturning { get; set; }
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
