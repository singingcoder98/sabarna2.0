# Sabarna — farm-fresh ghee, honey & pickles

A complete, production-shaped e-commerce site for a small Indian farm food business, built with
**ASP.NET Core 8 MVC + Razor views + Bootstrap 5.3.3**. Server-rendered, no JavaScript framework,
no CDN, no payment-gateway dependency to run it locally.

* **Brand:** Sabarna (ghee, honey, pickles, cold-pressed oil)
* **Market:** India — all prices in ₹ (INR), GST included
* **Payments:** online only — UPI, cards, netbanking. **Cash on delivery is not offered anywhere.**
* **Cart:** session based, no login needed
* **Admin:** two-step login (password + 6-digit one-time code) at `/Admin`

---

## 1. Prerequisites

| What | Version | Notes |
| --- | --- | --- |
| .NET SDK | **8.0** (or 8.0.x patch) | `dotnet --version` should print `8.0.*` |
| A browser | any modern one | No Node, npm, Python or database server needed |
| Disk | ~120 MB | NuGet packages + the SQLite file |

SQLite is embedded in the app, so there is nothing to install and nothing to configure.
The database file `app.db` is created *and seeded* automatically on the first run.

---

## 2. Run it

```bash
# from the folder that contains Sabarna.Web.csproj / Sabarna.sln
dotnet restore
dotnet run
```

`Sabarna.sln` (classic format) is included, so `dotnet build Sabarna.sln`, `dotnet run
--project Sabarna.Web.csproj` and "Open project/solution" in Visual Studio or Rider all work.
Do not keep a `Sabarna.slnx` in the same folder as well — two solution files make `dotnet build`
fail with `MSB1011`.

Then open **<http://localhost:5080>** (the port is set in `Properties/launchSettings.json`).

On the first start `DbSeeder` creates `app.db` with:

* the brand identity and hero copy,
* one admin account,
* 13 products (10 single jars + 3 combos, including the jaggery shelf, several featured,
  one deliberately out of stock),
* 6 orders spread across every status.

**Start over with clean demo data:** stop the app, delete `app.db` (and `app.db-shm` / `app.db-wal`
if present), and run it again. Nothing else needs to be reset.

`dotnet build` is expected to finish with **0 errors and 0 warnings**.
`app.db` is git-ignored — it is demo data, never commit it.

---

## 3. Admin panel

| | |
| --- | --- |
| URL | <http://localhost:5080/Admin> |
| Email | `admin@sabarna.in` |
| Password | `Sabarna@2024` |
| One-time code | **shown on screen** at `/Admin/VerifyOtp` while running in Development |

> **If you already ran the site before the rename:** the first start afterwards cleans the old
> brand out of your existing `app.db` — brand name, email, logo path, the address (which had the
> old name written into it twice) and any product description or tag that mentioned it. The demo
> admin is renamed to `admin@sabarna.in` with the password above. Orders, stock and prices are
> not touched.
>
> `DbSeeder.MigrateBrandAsync` runs on every start and does nothing once the row is clean, so it
> is safe to leave in forever. It is also the only place in the codebase that still contains the
> old name — it has to, in order to look for it. If the shop still shows the old name after
> pulling, you are either running a stale build (`rm -rf bin obj`, rebuild) or an old `app.db`
> from before this commit (delete `app.db*` to re-seed).

The code is valid for **5 minutes**, works **once**, and is burnt after 5 wrong attempts.
Only a SHA-256 hash of it is stored. In Development the code is displayed in a yellow
"Development mode" box because no mail provider is connected yet — see §7.

> **Before going live:** change the password (re-seed with a new hash in `Data/DbSeeder.cs`, or
> update the row in `app.db`) and run with `ASPNETCORE_ENVIRONMENT=Production` so the on-screen code
> disappears.

Screens:

* **Dashboard** (`/Admin/Dashboard`) — revenue collected, average order value, orders open and what
  they are worth, today's takings, a seven-day bar chart, the five best sellers by revenue, the
  recent orders and a restock list. Everything is computed from the order book; nothing is faked.
* **Orders** (`/Admin/Orders`) — status filter pills with live counts, search across order number,
  name and email, a total for whatever is on screen, one click to advance
  Processing → Packed → Shipped → Delivered, a "set status" dropdown for corrections, and
  **Export CSV** for the accountant.
* **Customers** (`/Admin/Customers`) — one row per person, rolled up from the order book: orders,
  jars, money spent, last order and a "returning" flag. There is no customer account table, so this
  page needs no maintenance and can never drift out of date.
* **Products** (`/Admin/Products`) — thumbnail table with stock/type/visibility pills, live↔hidden
  switch, **stock edited in place** (type a number, press Set — no form to open), search, edit modal
  per row, "add product" modal, delete with a confirmation dialog.
* **Settings** (`/Admin/Settings`) — brand name, tagline, logo path, hero kicker/headline/intro,
  email, phone, WhatsApp, address. Saved to the database and picked up by every page on the next
  request.

Every POST carries `[ValidateAntiForgeryToken]` and the whole controller sits behind the `AdminOnly`
policy, so no screen above is reachable without the two-step login. The CSV export writes a UTF-8
byte-order mark so Excel opens ₹ amounts as numbers rather than mojibake.

---

## 4. Project map

```
Sabarna.Web.csproj              net8.0 web SDK, EF Core 8 + SQLite
Program.cs                       DI, DbContext, session, cookie auth, "AdminOnly" policy, pipeline
appsettings.json                 ConnectionStrings:Default = "Data Source=app.db"
Properties/launchSettings.json   http://localhost:5080, ASPNETCORE_ENVIRONMENT=Development

Controllers/
  HomeController.cs              Index (GET + newsletter POST), About, Policies, Privacy, Terms,
                                 Shipping, Error
  ShopController.cs              Index(?category), Details(id), Combos
  CartController.cs              Index, Add, Update, Remove, Checkout (GET/POST), Success
  ContactController.cs           Index (GET + POST)
  AdminController.cs             Login, VerifyOtp, Logout, Dashboard, Orders (filter/search/status/
                                 CSV), Customers, Products, SaveProduct, UpdateStock, ToggleProduct,
                                 DeleteProduct, Settings
  SeoController.cs               /sitemap.xml and /robots.txt, generated from the database

Models/
  Entities.cs                    Product, SiteSetting, AdminUser, OtpChallenge, Order, OrderStatuses,
                                 ComboItemDto, CartLine, CheckoutViewModel, AdminLoginViewModel,
                                 OtpViewModel, ContactViewModel
  ViewModels.cs                  HomeViewModel, ShopViewModel, ProductDetailsViewModel,
                                 AdminDashboardViewModel, AdminOrdersViewModel,
                                 AdminProductsViewModel, AdminCustomersViewModel, ContactPageModel

Data/
  AppDbContext.cs                DbSets, precision, indexes, CreatedAt/UpdatedAt stamping
  DbSeeder.cs                    EnsureCreatedAsync + settings, admin, 13 products, 6 orders

Services/
  CartService.cs                 session JSON cart + combo helpers + shipping rules
  AuthService.cs                 password hash check, OTP create/verify, SHA-256
  StructuredData.cs              the JSON-LD blocks (Product, BreadcrumbList, ItemList)

Views/
  _ViewImports.cshtml            namespaces + tag helpers
  _ViewStart.cshtml              Layout = "_Layout"
  Shared/_Layout.cshtml          sticky navbar, main, dark footer, WhatsApp float, toasts, JSON-LD
  Shared/_Head.cshtml            per-page title/description, Open Graph, favicon, fonts, CSS
  Shared/_Scripts.cshtml         Bootstrap bundle + toast/reveal/confirm JS
  Shared/_ProductCard.cshtml     reusable product card
  Shared/_ComboCard.cshtml       reusable bundle card
  Shared/_AdminLayout.cshtml     sidebar shell + mobile offcanvas + top bar
  Shared/_AdminNav.cshtml        admin nav (used by both the sidebar and the offcanvas)
  Shared/_ProductFormFields.cshtml  the fields shared by the add/edit product modals
  Home/                          Index, About, Policies, Privacy, Terms, Shipping, Error
  Shop/                          Index, Details, Combos
  Cart/                          Index, Checkout, Success
  Contact/                       Index
  Admin/                         Login, VerifyOtp, Dashboard, Orders, Customers, Products, Settings

wwwroot/
  css/site.css                   the whole brand layer (~1 of 1 stylesheets)
  lib/bootstrap/                 bootstrap.min.css, bootstrap.bundle.min.js, LICENSE (v5.3.3)
  images/                        logo.png (full lockup, transparent), logo-mark.png (tree only,
                                 for the 36-44px slots), favicon.png, apple-touch-icon.png, hero,
                                 farmer photo, product photography (incl. jaggery), og-cover
  (robots.txt and sitemap.xml are no longer files — SeoController serves both)
```

A note on the two `Shared/_*` files that are not in the original brief: `_ProductFormFields.cshtml`
exists so the "add product" and "edit product" modals cannot drift apart, and `_AdminNav.cshtml` is
rendered twice (desktop sidebar + mobile offcanvas). Both are internal helpers, not new screens.

---

## 5. How the shop works

**Cart.** `CartService` keeps a JSON array of `CartLine` in the visitor's session
(`.sabarna.session`, 7-day idle timeout). Quantities are clamped to **1–20** and never exceed the
stock on hand; out-of-stock products cannot be added. Totals are always recomputed from the live
product price. Shipping is **free at ₹999 and above, a flat ₹49 below**, computed in exactly one
place (`CartService.ShippingFor`) so the basket, the checkout and the order always agree.

**Checkout.** `GET /Cart/Checkout` shows the form; `POST` re-validates everything on the server:
field rules, the payment method (only `UPI`, `Card`, `Netbanking` — anything else is rejected with a
message), product availability and stock. On success it writes the order, decrements stock, bumps the
sold counter, empties the basket and redirects to `/Cart/Success?orderNumber=…` (post/redirect/get).

**Toasts.** Actions set `TempData["Toast"]` or `TempData["Error"]`; both layouts render them as
Bootstrap toasts and a few lines of vanilla JS show them automatically.

**Security.** Every POST carries `[ValidateAntiForgeryToken]` and every posting form emits
`@Html.AntiForgeryToken()`. The admin cookie is `HttpOnly`, `SameSite=Lax`, 8 hours, sliding. Passwords
are ASP.NET Core `PasswordHasher` hashes — never plain text. `returnUrl` values are checked with
`Url.IsLocalUrl` so they cannot be used as an open redirect.

**SQLite and decimals (read this before adding price sorts).** SQLite has no native decimal
type: EF Core stores `Price`, `Mrp` and `Total` as TEXT, so any `ORDER BY`, `SUM`, `MAX` or `>`
comparison over them must not run in the database. The three places that need it — the combos band
on the home page, `/Shop/Combos` and the dashboard revenue — already read the rows first and finish
in memory, with a comment saying why. If you add another, either do the same or cast in the query
(`OrderBy(p => (double)p.Price)`).

**Admin changes are instant.** Product and settings writes go straight to SQLite; the storefront
reads on every render, so there is no cache to clear.

---

## 6. Design system in one minute

All brand tokens are CSS custom properties at the top of `wwwroot/css/site.css`:

```css
--mb-green-900 #1e2a14   --mb-green-800 #2f3e24   --mb-green-600 #4a6b3d
--mb-terra     #b5582c   --mb-cream     #fbf6ea   --mb-cream-2   #f4edd8
--mb-gold      #e9c46a   --mb-muted     #5c6a50   --mb-line      rgba(47,62,36,.12)
```

* **Type:** Fraunces (headings) + Poppins (body), fluid `clamp()` sizes, system-font fallbacks.
* **Buttons:** pill shaped. `.btn-brand`, `.btn-gold`, `.btn-terra`, `.btn-outline-brand`,
  `.btn-ghost`, plus a `.btn-ghost-light` for dark bands. Every variant is defined by setting
  Bootstrap's own `--bs-btn-*` variables — **`!important` is never used**, and `bootstrap.min.css`
  is untouched.
* **Components:** product/combo cards, pill badges, tables, form controls, toasts, FAQ accordion,
  stat cards, admin sidebar, split-screen login, empty states, dark footer, floating WhatsApp button.
* **Accessibility:** visible focus rings, `aria-label`s on every icon-only control, a skip link,
  semantic headings, alt text on all content images (the logo is marked decorative because the brand
  name sits next to it), and every animation is disabled under `prefers-reduced-motion`.

Changing the palette means editing those nine values — nothing else.

---

**The logo.** The supplied artwork is a stacked lockup: a tree, the word SABARNA, and PURE. Two
files come out of it, because one size cannot do both jobs:

| File | What it is | Where it is used |
| --- | --- | --- |
| `images/logo-mark.png` | the tree on its own, transparent | the 36–44 px slots: navbar, footer, admin sidebar, sign-in screens |
| `images/logo.png` | the full lockup, transparent | structured data (`Organization.logo`), the settings placeholder, anywhere large |
| `images/favicon.png`, `images/apple-touch-icon.png` | the tree on the cream colour | browser tab, iOS home screen |

The cream paper of the original was knocked out to transparency, so the mark sits cleanly on both
the cream navbar and the dark green footer. `SiteSetting.Logo` points at the **mark**; change it in
`/Admin/Settings` if you ever want a different image in those small slots.

---

## 7. Deliberately deferred (clearly marked `TODO` in the code)

1. **Payment gateway.** `CartController.Checkout` has a marked block where Razorpay goes. Today the
   order is simply recorded with `Payment = "Paid"` so the rest of the flow can be finished and
   tested. Everything else (order number, stock, confirmation page, admin views) is already in place.
2. **Email / SMS.** The admin OTP and the contact form are shown on screen / toasted instead of sent.
   See `AdminController.Login` (OTP) and `ContactController.Index` (contact form).
3. **Product image upload.** The admin types an image path (`/images/products/…`). The path is
   normalised, but there is no upload control yet.
4. **Order status notifications.** Advancing an order updates the database; the customer is not
   emailed yet (noted on the Orders screen).

---

## 8. Search engines and sharing

Everything below is already wired up; the only thing left for launch day is the domain itself.

**Per page**
* A unique `<title>` and meta description, set in the controller with `ViewData["Title"]` and
  `ViewData["Description"]` (never duplicated between pages).
* `rel="canonical"` built from the request. A category filter (`/Shop?category=Honey`) keeps its
  query in the canonical because it is a real page; anything else in the query string is dropped.
* Robots directive defaults to `index, follow, max-image-preview:large`. The basket, checkout,
  confirmation page and the whole admin area send `noindex` instead — transactional pages are
  not content and should never compete with the shop.
* Open Graph and Twitter cards, with a per-product share image on product pages and
  `og:type=product` there as well.

**Structured data (JSON-LD)**
* `OnlineStore` in the layout — name, address, phone, accepted payments, price range, area served.
* `Product` on every product page — name, image, SKU, brand, price in INR with `Offer`,
  availability, and an `AggregateRating` only when the product actually has reviews.
* `BreadcrumbList` on product pages, mirroring the visible trail.
* `ItemList` on the shop listing, in the order the jars appear.

**Crawling**
* `/robots.txt` is generated (`SeoController`), so its `Sitemap:` line always names the host that
  served it — the classic launch-day bug of a hard-coded domain cannot happen.
* `/sitemap.xml` is generated from the database: every visible product, every category and the
  static pages, each with a `lastmod`. Add a jar in the admin panel and it is in the sitemap before
  the next crawl; nothing to regenerate by hand.
* `UseStatusCodePagesWithReExecute` keeps the real 404/500 status code while showing the friendly
  page, which is what search engines actually need to see.
* A trailing slash (`/Shop/`) 301s to `/Shop`, so one page never has two addresses.

**Speed (Core Web Vitals)**
* The hero photograph is a CSS background, so it is `preload`ed with `fetchpriority="high"` — this
  is the largest contentful paint on the site and now starts with the HTML, not with the CSS.
* Every product image carries `width`/`height` (no layout shift), `loading="lazy"` and
  `decoding="async"`; only the hero is eager.
* Fonts are `preconnect`ed and loaded with `display=swap`; Bootstrap ships from `wwwroot`, so there
  is no third-party request on the critical path.
* The "In the kitchen" band animates with a CSS transform on the compositor — no video, no GIF,
  and it is switched off entirely under `prefers-reduced-motion`.

## 9. Before launch checklist

* [ ] Change the admin password and remove the demo hint from `Views/Admin/Login.cshtml`.
* [ ] Run in Production (`ASPNETCORE_ENVIRONMENT=Production`) so the OTP is no longer shown on screen.
* [ ] Put the app behind HTTPS (reverse proxy or hosting platform) — the app itself listens on HTTP.
* [ ] Update the domain in `wwwroot/robots.txt` and `wwwroot/sitemap.xml` (`https://sabarna.in`).
* [ ] Replace the demo photos in `wwwroot/images/` with real ones (keep the file names, or update the
      paths in `/Admin/Products`).
* [ ] Double-check the policies in `Views/Home/{Privacy,Terms,Shipping}.cshtml` with the owner — they
      are written in plain language on purpose and should match reality.
* [ ] Back up `app.db` (or move to a hosted database) — the SQLite file holds every order.

---

## 10. Content notes

Everything the shop says about itself is placeholder copy that reads like a real small business:
the farm story, the batch sizes, the "nine months of shelf life", the courier timelines. Replace it
with the owner's own words — the home page hero and contact details are editable from
`/Admin/Settings` without touching code.

**Contact details used in the demo**

| | |
| --- | --- |
| Address | Sabarna Farm, Village Sabarna, P.O. Kharagpur, West Bengal 721301 |
| Phone | +91 98000 12345 |
| Email | hello@sabarna.in |
| WhatsApp | 919800012345 (digits only, with country code) |

---

## 11. Hands-on with the running site (a 3-minute tour)

1. `/` — hero, trust marquee, product grid, combos, FAQ, newsletter.
2. Add two jars to the basket, open `/Cart`, use the +/− steppers, then *Proceed to checkout*.
3. Fill the form, choose UPI, place the order → you land on the confirmation page with an order number.
4. `/Admin` → sign in (password, then the on-screen code) → the new order is at the top of **Orders**.
5. Click **Mark Packed**, then go to **Products** and hide a jar — refresh `/Shop` and it is gone.
6. `/Admin/Settings` — change the hero headline, save, and check the home page.
