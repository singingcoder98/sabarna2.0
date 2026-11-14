using System.Text.Json;
using Sabarna.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Sabarna.Web.Data;

/// <summary>
/// Creates the SQLite file, the tables and a complete set of demo data on first run.
/// Running the app a second time does nothing — every block below checks "is this empty?"
/// before inserting, so the owner's own edits are never overwritten.
/// </summary>
public static class DbSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var hasher = services.GetRequiredService<IPasswordHasher<AdminUser>>();

        // Creates app.db and every table/index described in AppDbContext.
        await db.Database.EnsureCreatedAsync();

        await MigrateBrandAsync(db, hasher);
        await SeedSettingsAsync(db);
        await SeedAdminAsync(db, hasher);
        await SeedProductsAsync(db);
        await SeedOrdersAsync(db);
    }

    // -----------------------------------------------------------------------------------------
    // 1. Brand identity + hero copy. Everything here can be edited at /Admin/Settings.
    // -----------------------------------------------------------------------------------------
    private static async Task SeedSettingsAsync(AppDbContext db)
    {
        if (await db.Settings.AnyAsync())
        {
            return;
        }

        db.Settings.Add(new SiteSetting
        {
            BrandName = "Sabarna Pure",
            Tagline = "Small-batch ghee, honey & pickles",
            Logo = "/images/logo-mark.png",
            // saptarshi: 13-Nov-2026 — the shop's real contact details.
            Email = "orders.sabarna@gmail.com",
            Phone = "+91 89101 11809",
            Whatsapp = "918910111809",
            Address = "Sabarna Small Farms, P.O. Kharagpur, West Bengal 721301",
            HeroKicker = "Farm to home · since 2016",
            HeroTitle1 = "Ghee that still",
            HeroTitle2 = "smells of the farm",
            HeroText = "We are a family of eleven from Midnapore who make bilona ghee, wild forest honey "
                     + "and sun-cured pickles in batches small enough to know every jar by hand. "
                     + "No refining, no preservatives, no promises we cannot keep.",
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    // -----------------------------------------------------------------------------------------
    // 1b. Brand cleanup. The shop was first seeded as "Matibari"; this removes every trace of that
    //     name from a database created before the rename — the brand, the email, the logo path and
    //     the address (which had the old name written into it twice). It runs on every start and
    //     does nothing at all once the row is clean, so it is safe to leave in forever.
    // -----------------------------------------------------------------------------------------
    private static async Task MigrateBrandAsync(AppDbContext db, IPasswordHasher<AdminUser> hasher)
    {
        const string oldName = "Matibari";
        var changed = false;

        var settings = await db.Settings.FirstOrDefaultAsync();
        if (settings != null)
        {
            // saptarshi: added 08-Nov-2026 — the shop's name became "Sabarna Pure" when the new logo
            // arrived. A database seeded before that still stores the short name, and the owner's
            // own edits must survive, so only the exact old value is lifted — anything else he has
            // typed at /Admin/Settings is left alone. Runs every start; does nothing after the first.
            if (settings.BrandName.Trim().Equals("Sabarna", StringComparison.OrdinalIgnoreCase))
            {
                settings.BrandName = "Sabarna Pure";
                changed = true;
            }

            // The seeded address carried the old name twice ("Matibari Farm, Village Matibari"),
            // which a word-for-word swap would turn into nonsense. Replace the whole line instead.
            if (settings.Address != null &&
                settings.Address.Contains("Village " + oldName, StringComparison.OrdinalIgnoreCase))
            {
                settings.Address = "Sabarna Small Farms, P.O. Kharagpur, West Bengal 721301";
                changed = true;
            }

            // Any other text field still mentioning the old name is rewritten in place, so the
            // owner's own wording survives and only the brand word changes.
            changed |= ScrubOldName(settings, oldName);

            // saptarshi: 13-Nov-2026 — the real phone number and inbox, in place of the placeholders
            // that shipped with the demo data. This runs once on the live database too (it is only a
            // string swap) and it touches NOTHING else: only the exact old placeholder values are
            // replaced, so anything the shop owner typed himself is left exactly as it is.
            //   phone     +91 98000 12345     ->  +91 89101 11809
            //   whatsapp  919800012345        ->  918910111809
            //   email     hello@sabarna.in    ->  orders.sabarna@gmail.com
            changed |= ReplaceContactDetail(settings.Phone, "+91 98000 12345", "+91 89101 11809",
                                            value => settings.Phone = value);

            changed |= ReplaceContactDetail(settings.Whatsapp, "919800012345", "918910111809",
                                            value => settings.Whatsapp = value);

            changed |= ReplaceContactDetail(settings.Email, "hello@sabarna.in", "orders.sabarna@gmail.com",
                                            value => settings.Email = value);

            if (changed)
            {
                settings.UpdatedAt = DateTime.UtcNow;
            }
        }

        // A product description or tag might mention the shop by its old name. Only the handful of
        // rows that actually do are loaded, and only their wording changes.
        var mentioningProducts = await db.Products
            .Where(p => p.Name.Contains(oldName) || p.Description.Contains(oldName) || p.Tag.Contains(oldName))
            .ToListAsync();

        foreach (var product in mentioningProducts)
        {
            changed |= ScrubOldName(product, oldName);
        }

        // The demo admin keeps working under its new address. Skipped if that address is taken.
        var legacyAdmin = await db.AdminUsers.FirstOrDefaultAsync(a => a.Email == "admin@matibari.in");
        if (legacyAdmin != null && !await db.AdminUsers.AnyAsync(a => a.Email == "admin@sabarna.in"))
        {
            legacyAdmin.Email = "admin@sabarna.in";

            // The documented password changed with the brand, so the demo hash is refreshed too.
            // If this account's password was changed by hand, set it again from /Admin afterwards.
            legacyAdmin.PasswordHash = hasher.HashPassword(legacyAdmin, "Sabarna@2024");

            changed = true;
        }

        // saptarshi: Phase 0 (12-Nov-2026) — while an account still accepts the seeded password,
        // the panel stays locked to the password form. Checked on every start, so it heals itself
        // even if the flag is ever reset by hand. Nothing is written to a log about the password.
        foreach (var account in await db.AdminUsers.Where(a => !a.MustChangePassword).ToListAsync())
        {
            var stillSeeded = hasher.VerifyHashedPassword(account, account.PasswordHash, "Sabarna@2024")
                              != PasswordVerificationResult.Failed;

            if (stillSeeded)
            {
                account.MustChangePassword = true;
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// saptarshi: 13-Nov-2026 — swaps one known placeholder for the real value.
    /// Returns true only when something actually changed, and leaves any other value untouched, so a
    /// detail the owner has edited in /Admin/Settings is never overwritten by a deploy.
    /// </summary>
    private static bool ReplaceContactDetail(string current, string placeholder, string replacement,
                                            Action<string> assign)
    {
        var isOldPlaceholder = string.Equals((current ?? string.Empty).Trim(), placeholder,
                                             StringComparison.OrdinalIgnoreCase);

        // An empty field is filled in as well: a shop with no phone number on it is not a shop yet.
        var isEmpty = string.IsNullOrWhiteSpace(current);

        if (!isOldPlaceholder && !isEmpty)
        {
            return false;
        }

        assign(replacement);
        return true;
    }

    /// <summary>
    /// Rewrites every string property of an entity that still contains the pre-rename brand,
    /// leaving the rest of the owner's wording exactly as it was.
    /// </summary>
    private static bool ScrubOldName(object entity, string oldName)
    {
        var changed = false;

        foreach (var property in entity.GetType().GetProperties())
        {
            if (property.PropertyType != typeof(string) || !property.CanWrite)
            {
                continue;
            }

            var value = property.GetValue(entity) as string;
            if (value == null || !value.Contains(oldName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            property.SetValue(entity, value.Replace(oldName, "Sabarna Pure", StringComparison.OrdinalIgnoreCase));
            changed = true;
        }

        return changed;
    }

    // -----------------------------------------------------------------------------------------
    // 2. The single admin account. DEMO CREDENTIALS (change them before going live):
    //      email:    admin@sabarna.in
    //      password: Sabarna@2024
    // -----------------------------------------------------------------------------------------
    private static async Task SeedAdminAsync(AppDbContext db, IPasswordHasher<AdminUser> hasher)
    {
        if (await db.AdminUsers.AnyAsync())
        {
            return;
        }

        var admin = new AdminUser
        {
            Email = "admin@sabarna.in",
            FullName = "Sabarna Manna",
            Role = "admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,

            // saptarshi: Phase 0 — this password is written in the project notes, so the panel asks
            // for a new one before anything else is allowed.
            MustChangePassword = true
        };

        // PasswordHasher adds a per-password salt and a slow KDF — never store plain text.
        admin.PasswordHash = hasher.HashPassword(admin, "Sabarna@2024");
        db.AdminUsers.Add(admin);

        await db.SaveChangesAsync();
    }

    // -----------------------------------------------------------------------------------------
    // 3. Ten products: eight single jars plus two curated combos (one of them featured).
    // -----------------------------------------------------------------------------------------
    private static async Task SeedProductsAsync(AppDbContext db)
    {
        if (await db.Products.AnyAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;

        var products = new List<Product>
        {
            new()
            {
                Name = "A2 Bilona Cow Ghee — 500 ml",
                Category = "Ghee & Dairy",
                Price = 899m,
                Mrp = 1049m,
                Unit = "500 ml glass jar",
                Rating = 4.9,
                Reviews = 214,
                Image = "/images/products/ghee-500.jpg",
                Tag = "Best seller",
                Description = "Hand-churned from the morning milk of our own Gir cows, then simmered slowly "
                            + "on a wood fire. Grainy, fragrant and golden — the way ghee tasted before "
                            + "everything was made in a factory.",
                Active = true,
                Stock = 42,
                Sold = 186,
                IsCombo = false,
                Featured = true,
                ComboItemsJson = "[]",
                CreatedAt = now.AddDays(-120),
                UpdatedAt = now.AddDays(-3)
            },
            new()
            {
                Name = "A2 Bilona Cow Ghee — 1 litre",
                Category = "Ghee & Dairy",
                Price = 1699m,
                Mrp = 1949m,
                Unit = "1 litre glass jar",
                Rating = 4.8,
                Reviews = 97,
                Image = "/images/products/ghee-1l.jpg",
                Tag = "Value jar",
                Description = "The same slow-churned ghee in the larger jar that most families actually "
                            + "finish in a month. Keeps for nine months without a fridge.",
                Active = true,
                Stock = 18,
                Sold = 74,
                IsCombo = false,
                Featured = false,
                ComboItemsJson = "[]",
                CreatedAt = now.AddDays(-120),
                UpdatedAt = now.AddDays(-9)
            },
            new()
            {
                Name = "Wild Forest Honey — 500 g",
                Category = "Honey",
                Price = 649m,
                Mrp = 749m,
                Unit = "500 g glass jar",
                Rating = 4.9,
                Reviews = 168,
                Image = "/images/products/honey-500.jpg",
                Tag = "Raw & unheated",
                Description = "Collected by Santhal honey-gatherers from the sal forests behind our village. "
                            + "Unheated and unfiltered, so it crystallises in winter — that is how you know "
                            + "it is real.",
                Active = true,
                Stock = 36,
                Sold = 141,
                IsCombo = false,
                Featured = true,
                ComboItemsJson = "[]",
                CreatedAt = now.AddDays(-95),
                UpdatedAt = now.AddDays(-5)
            },
            new()
            {
                Name = "Sundarban Wild Honey — 250 g",
                Category = "Honey",
                Price = 349m,
                Mrp = 399m,
                Unit = "250 g glass jar",
                Rating = 4.7,
                Reviews = 63,
                Image = "/images/products/honey-250.jpg",
                Tag = "Small jar",
                Description = "A darker, smokier honey from the mangrove belt — a small jar to try before "
                            + "you commit to the big one.",
                Active = true,
                Stock = 7,
                Sold = 88,
                IsCombo = false,
                Featured = false,
                ComboItemsJson = "[]",
                CreatedAt = now.AddDays(-70),
                UpdatedAt = now.AddDays(-2)
            },
            new()
            {
                Name = "Bengali Mango Pickle — 400 g",
                Category = "Pickles",
                Price = 299m,
                Mrp = 349m,
                Unit = "400 g ceramic jar",
                Rating = 4.8,
                Reviews = 121,
                Image = "/images/products/mango-pickle.jpg",
                Tag = "Sun-cured",
                Description = "Raw totapada mangoes, cold-pressed mustard oil, panch phoron and rock salt, "
                            + "cured on the terrace for twenty-one days. Sharp, mustardy and completely "
                            + "addictive with dal and rice.",
                Active = true,
                Stock = 54,
                Sold = 203,
                IsCombo = false,
                Featured = true,
                ComboItemsJson = "[]",
                CreatedAt = now.AddDays(-150),
                UpdatedAt = now.AddDays(-11)
            },
            new()
            {
                Name = "Lemon & Green Chilli Pickle — 350 g",
                Category = "Pickles",
                Price = 279m,
                Mrp = 319m,
                Unit = "350 g ceramic jar",
                Rating = 4.6,
                Reviews = 44,
                Image = "/images/products/lemon-pickle.jpg",
                Tag = "Batch of 40",
                Description = "Thin-skinned lemons and fat green chillies salted down with turmeric. Made only "
                            + "forty jars at a time because that is how much fits on one drying mat.",
                Active = true,
                Stock = 0,           // kept at zero on purpose so the "out of stock" state can be seen
                Sold = 39,
                IsCombo = false,
                Featured = false,
                ComboItemsJson = "[]",
                CreatedAt = now.AddDays(-60),
                UpdatedAt = now.AddHours(-6)
            },
            new()
            {
                Name = "Kachi Ghani Mustard Oil — 1 litre",
                Category = "Oils",
                Price = 249m,
                Mrp = 289m,
                Unit = "1 litre bottle",
                Rating = 4.7,
                Reviews = 88,
                Image = "/images/products/mustard-oil.jpg",
                Tag = "Cold pressed",
                Description = "Pressed from Bengali black mustard seed in a wooden kachi ghani at under 40°C. "
                            + "Pungent enough to clear your sinuses, which is exactly the point.",
                Active = true,
                Stock = 25,
                Sold = 117,
                IsCombo = false,
                Featured = false,
                ComboItemsJson = "[]",
                CreatedAt = now.AddDays(-80),
                UpdatedAt = now.AddDays(-14)
            },
            new()
            {
                Name = "A2 Bilona Cow Ghee — 250 ml",
                Category = "Ghee & Dairy",
                Price = 499m,
                Mrp = 579m,
                Unit = "250 ml glass jar",
                Rating = 4.8,
                Reviews = 39,
                Image = "/images/products/ghee-250.jpg",
                Tag = "Trial jar",
                Description = "The smallest jar we fill — enough for a fortnight of dal, or a first taste before "
                            + "committing to the big one. Same milk, same wood fire, same date written by hand.",
                Active = true,
                Stock = 30,
                Sold = 54,
                IsCombo = false,
                Featured = false,
                ComboItemsJson = "[]",
                CreatedAt = now.AddDays(-45),
                UpdatedAt = now.AddDays(-7)
            },
            new()
            {
                Name = "Organic Jaggery Powder — 500 g",
                Category = "Jaggery & Gur",
                Price = 249m,
                Mrp = 299m,
                Unit = "500 g pouch",
                Rating = 4.8,
                Reviews = 63,
                Image = "/images/products/jaggery-powder.jpg",
                Tag = "New on the shelf",
                Description = "Cane juice boiled down in an open pan until it sets, then ground coarse. "
                            + "Nothing refined, nothing bleached, no sulphur — just the whole sweetness "
                            + "with the minerals still in it. A spoonful in warm water in the morning.",
                Active = true,
                Stock = 36,
                Sold = 24,
                IsCombo = false,
                Featured = true,
                ComboItemsJson = "[]",
                CreatedAt = now.AddDays(-18),
                UpdatedAt = now.AddDays(-2)
            },
            new()
            {
                Name = "Jaggery Blocks (Gur) — 1 kg",
                Category = "Jaggery & Gur",
                Price = 329m,
                Mrp = 399m,
                Unit = "1 kg block pack",
                Rating = 4.7,
                Reviews = 28,
                Image = "/images/products/jaggery-blocks.jpg",
                Tag = "Winter batch",
                Description = "The old-fashioned gur: poured into wooden moulds and left to cool overnight, "
                            + "so the blocks stay dark, damp and full of molasses. Break a piece off for "
                            + "pithe, payesh, or a cup of cha on a cold morning.",
                Active = true,
                Stock = 18,
                Sold = 9,
                IsCombo = false,
                Featured = false,
                ComboItemsJson = "[]",
                CreatedAt = now.AddDays(-18),
                UpdatedAt = now.AddDays(-5)
            },
            new()
            {
                Name = "The Everyday Duo — Ghee + Honey",
                Category = "Combos",
                Price = 1399m,
                Mrp = 1598m,
                Unit = "2 jars · gift boxed",
                Rating = 4.9,
                Reviews = 78,
                Image = "/images/products/combo-duo.jpg",
                Tag = "Save ₹199",
                Description = "Our two most-ordered jars in one cotton-wrapped box: 500 ml bilona ghee and "
                            + "500 g wild forest honey. The parcel we send most often as a housewarming gift.",
                Active = true,
                Stock = 20,
                Sold = 65,
                IsCombo = true,
                Featured = true,
                ComboItemsJson = ComboJson(
                    ("A2 Bilona Cow Ghee", "500 ml", "/images/products/ghee-500.jpg"),
                    ("Wild Forest Honey", "500 g", "/images/products/honey-500.jpg")),
                CreatedAt = now.AddDays(-40),
                UpdatedAt = now.AddDays(-4)
            },
            new()
            {
                Name = "Bengali Breakfast Box",
                Category = "Combos",
                Price = 1199m,
                Mrp = 1447m,
                Unit = "3 jars · gift boxed",
                Rating = 4.8,
                Reviews = 41,
                Image = "/images/products/combo-breakfast.jpg",
                Tag = "Save ₹248",
                Description = "Everything a Bengali breakfast asks for: ghee for the ruti, mango pickle for "
                            + "the dal, and honey for the toast you pretend you are not eating.",
                Active = true,
                Stock = 12,
                Sold = 28,
                IsCombo = true,
                Featured = false,
                ComboItemsJson = ComboJson(
                    ("A2 Bilona Cow Ghee", "500 ml", "/images/products/ghee-500.jpg"),
                    ("Bengali Mango Pickle", "400 g", "/images/products/mango-pickle.jpg"),
                    ("Wild Forest Honey", "500 g", "/images/products/honey-500.jpg")),
                CreatedAt = now.AddDays(-22),
                UpdatedAt = now.AddDays(-1)
            },
            new()
            {
                Name = "Ghee & Gur Box — 500 ml + 500 g",
                Category = "Combos",
                Price = 1089m,
                Mrp = 1148m,
                Unit = "2 jars · gift boxed",
                Rating = 4.9,
                Reviews = 16,
                Image = "/images/products/combo-gur.jpg",
                Tag = "Save ₹59",
                Description = "Ghee and gur, the two jars a Bengali kitchen empties first. One 500 ml "
                            + "bilona ghee, one 500 g jaggery powder, packed together in a cotton-wrapped "
                            + "crate that needs no wrapping paper.",
                Active = true,
                Stock = 15,
                Sold = 4,
                IsCombo = true,
                Featured = false,
                ComboItemsJson = ComboJson(
                    ("A2 Bilona Cow Ghee", "500 ml", "/images/products/ghee-500.jpg"),
                    ("Organic Jaggery Powder", "500 g", "/images/products/jaggery-powder.jpg")),
                CreatedAt = now.AddDays(-9),
                UpdatedAt = now.AddDays(-2)
            }
        };

        db.Products.AddRange(products);
        await db.SaveChangesAsync();
    }

    // -----------------------------------------------------------------------------------------
    // 4. Six sample orders so the dashboard and the orders table are never empty.
    // -----------------------------------------------------------------------------------------
    private static async Task SeedOrdersAsync(AppDbContext db)
    {
        if (await db.Orders.AnyAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;
        var year = now.Year;

        db.Orders.AddRange(
            new Order
            {
                OrderNumber = $"SB-{year}-1041",
                Customer = "Ananya Ghosh",
                Email = "ananya.ghosh@example.com",
                ItemCount = 3,
                ProductName = "A2 Bilona Cow Ghee — 500 ml + 2 more",
                ProductImage = "/images/products/ghee-500.jpg",
                Total = 1847m,
                Payment = "Paid",
                PaymentMethod = "UPI",
                Status = OrderStatuses.Delivered,
                CreatedAt = now.AddDays(-21)
            },
            new Order
            {
                OrderNumber = $"SB-{year}-1042",
                Customer = "Rohit Banerjee",
                Email = "rohit.b@example.com",
                ItemCount = 1,
                ProductName = "The Everyday Duo — Ghee + Honey",
                ProductImage = "/images/products/combo-duo.jpg",
                Total = 1399m,
                Payment = "Paid",
                PaymentMethod = "Card",
                Status = OrderStatuses.Delivered,
                CreatedAt = now.AddDays(-16)
            },
            new Order
            {
                OrderNumber = $"SB-{year}-1043",
                Customer = "Meera Iyer",
                Email = "meera.iyer@example.com",
                ItemCount = 2,
                ProductName = "Wild Forest Honey — 500 g + 1 more",
                ProductImage = "/images/products/honey-500.jpg",
                Total = 948m,
                Payment = "Paid",
                PaymentMethod = "Netbanking",
                Status = OrderStatuses.Shipped,
                CreatedAt = now.AddDays(-9)
            },
            new Order
            {
                OrderNumber = $"SB-{year}-1044",
                Customer = "Souvik Dutta",
                Email = "souvik.dutta@example.com",
                ItemCount = 4,
                ProductName = "Bengali Mango Pickle — 400 g + 3 more",
                ProductImage = "/images/products/mango-pickle.jpg",
                Total = 2196m,
                Payment = "Paid",
                PaymentMethod = "UPI",
                Status = OrderStatuses.Packed,
                CreatedAt = now.AddDays(-5)
            },
            new Order
            {
                OrderNumber = $"SB-{year}-1045",
                Customer = "Priyanka Seal",
                Email = "priyanka.seal@example.com",
                ItemCount = 1,
                ProductName = "Bengali Breakfast Box",
                ProductImage = "/images/products/combo-breakfast.jpg",
                Total = 1199m,
                Payment = "Paid",
                PaymentMethod = "Card",
                Status = OrderStatuses.Processing,
                CreatedAt = now.AddDays(-2)
            },
            new Order
            {
                OrderNumber = $"SB-{year}-1046",
                Customer = "Arjun Chatterjee",
                Email = "arjun.c@example.com",
                ItemCount = 2,
                ProductName = "Kachi Ghani Mustard Oil — 1 litre + 1 more",
                ProductImage = "/images/products/mustard-oil.jpg",
                Total = 1107m,
                Payment = "Paid",
                PaymentMethod = "UPI",
                Status = OrderStatuses.Processing,
                CreatedAt = now.AddHours(-20)
            });

        // saptarshi: added 06-Nov-2026 — the sample orders were written before the payment status
        // existed. Everything seeded is Paid, so say so, otherwise the admin pills would show a
        // confusing "Pending" on a freshly seeded database.
        foreach (var order in db.Orders.Local.Where(o => o.Payment == PaymentStatuses.Paid))
        {
            order.PaymentStatus = PaymentStatuses.Paid;
            order.PaymentVerifiedBy = "demo seed";
            order.PaidAt = order.CreatedAt.AddMinutes(3);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Turns a few (name, quantity, image) rows into the JSON stored on a combo product.</summary>
    private static string ComboJson(params (string Name, string Qty, string Image)[] items)
    {
        var list = items
            .Select(item => new ComboItemDto { Name = item.Name, Qty = item.Qty, Image = item.Image })
            .ToList();

        return JsonSerializer.Serialize(list, JsonOptions);
    }
}
