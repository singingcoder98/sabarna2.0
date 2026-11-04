# Matibari — farm-fresh ghee, honey & pickles

A complete, production-shaped e-commerce site for a small Indian farm food business, built with
**ASP.NET Core 8 MVC + Razor views + Bootstrap 5.3.3**. Server-rendered, no JavaScript framework,
no CDN, no payment-gateway dependency to run it locally.

* **Brand:** Matibari (ghee, honey, pickles, cold-pressed oil)
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
# from the folder that contains Matibari.Web.csproj
dotnet restore
dotnet run
```

Then open **<http://localhost:5080>** (the port is set in `Properties/launchSettings.json`).

On the first start `DbSeeder` creates `app.db` with:

* the brand identity and hero copy,
* one admin account,
* 10 products (8 single jars + 2 combos, several featured, one deliberately out of stock),
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
| Email | `admin@matibari.in` |
| Password | `Matibari@2024` |
| One-time code | **shown on screen** at `/Admin/VerifyOtp` while running in Development |

The code is valid for **5 minutes**, works **once**, and is burnt after 5 wrong attempts.
Only a SHA-256 hash of it is stored. In Development the code is displayed in a yellow
"Development mode" box because no mail provider is connected yet — see §7.

> **Before going live:** change the password (re-seed with a new hash in `Data/DbSeeder.cs`, or
> update the row in `app.db`) and run with `ASPNETCORE_ENVIRONMENT=Production` so the on-screen code
> disappears.

Screens:

* **Dashboard** — revenue, order count, live/total products, low stock, recent orders, restock list.
* **Orders** — filter by status; one click advances Processing → Packed → Shipped → Delivered.
* **Products** — thumbnail table with stock/type/visibility pills, live↔hidden switch, edit modal per
  row, "add product" modal, delete with a confirmation dialog.
* **Settings** — brand name, tagline, logo path, hero kicker/headline/intro, email, phone, WhatsApp,
  address. Saved to the database and picked up by every page on the next request.

---

## 4. Project map

```
Matibari.Web.csproj              net8.0 web SDK, EF Core 8 + SQLite
Program.cs                       DI, DbContext, session, cookie auth, "AdminOnly" policy, pipeline
appsettings.json                 ConnectionStrings:Default = "Data Source=app.db"
Properties/launchSettings.json   http://localhost:5080, ASPNETCORE_ENVIRONMENT=Development

Controllers/
  HomeController.cs              Index (GET + newsletter POST), About, Policies, Privacy, Terms,
                                 Shipping, Error
  ShopController.cs              Index(?category), Details(id), Combos
  CartController.cs              Index, Add, Update, Remove, Checkout (GET/POST), Success
  ContactController.cs           Index (GET + POST)
  AdminController.cs             Login, VerifyOtp, Logout, Dashboard, Orders, AdvanceOrder,
                                 Products, SaveProduct, ToggleProduct, DeleteProduct, Settings

Models/
  Entities.cs                    Product, SiteSetting, AdminUser, OtpChallenge, Order, OrderStatuses,
                                 ComboItemDto, CartLine, CheckoutViewModel, AdminLoginViewModel,
                                 OtpViewModel, ContactViewModel
  ViewModels.cs                  HomeViewModel, ShopViewModel, ProductDetailsViewModel,
                                 AdminDashboardViewModel, ContactPageModel

Data/
  AppDbContext.cs                DbSets, precision, indexes, CreatedAt/UpdatedAt stamping
  DbSeeder.cs                    EnsureCreatedAsync + settings, admin, products, orders

Services/
  CartService.cs                 session JSON cart + combo helpers + shipping rules
  AuthService.cs                 password hash check, OTP create/verify, SHA-256

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
  Admin/                         Login, VerifyOtp, Dashboard, Orders, Products, Settings

wwwroot/
  css/site.css                   the whole brand layer (~1 of 1 stylesheets)
  lib/bootstrap/                 bootstrap.min.css, bootstrap.bundle.min.js, LICENSE (v5.3.3)
  images/                        logo, hero, farmer photo, product photography, og-cover, favicon
  robots.txt, sitemap.xml
```

A note on the two `Shared/_*` files that are not in the original brief: `_ProductFormFields.cshtml`
exists so the "add product" and "edit product" modals cannot drift apart, and `_AdminNav.cshtml` is
rendered twice (desktop sidebar + mobile offcanvas). Both are internal helpers, not new screens.

---

## 5. How the shop works

**Cart.** `CartService` keeps a JSON array of `CartLine` in the visitor's session
(`.matibari.session`, 7-day idle timeout). Quantities are clamped to **1–20** and never exceed the
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

## 8. Before launch checklist

* [ ] Change the admin password and remove the demo hint from `Views/Admin/Login.cshtml`.
* [ ] Run in Production (`ASPNETCORE_ENVIRONMENT=Production`) so the OTP is no longer shown on screen.
* [ ] Put the app behind HTTPS (reverse proxy or hosting platform) — the app itself listens on HTTP.
* [ ] Update the domain in `wwwroot/robots.txt` and `wwwroot/sitemap.xml` (`https://matibari.in`).
* [ ] Replace the demo photos in `wwwroot/images/` with real ones (keep the file names, or update the
      paths in `/Admin/Products`).
* [ ] Double-check the policies in `Views/Home/{Privacy,Terms,Shipping}.cshtml` with the owner — they
      are written in plain language on purpose and should match reality.
* [ ] Back up `app.db` (or move to a hosted database) — the SQLite file holds every order.

---

## 9. Content notes

Everything the shop says about itself is placeholder copy that reads like a real small business:
the farm story, the batch sizes, the "nine months of shelf life", the courier timelines. Replace it
with the owner's own words — the home page hero and contact details are editable from
`/Admin/Settings` without touching code.

**Contact details used in the demo**

| | |
| --- | --- |
| Address | Matibari Farm, Village Matibari, P.O. Kharagpur, West Bengal 721301 |
| Phone | +91 98000 12345 |
| Email | hello@matibari.in |
| WhatsApp | 919800012345 (digits only, with country code) |

---

## 10. Hands-on with the running site (a 3-minute tour)

1. `/` — hero, trust marquee, product grid, combos, FAQ, newsletter.
2. Add two jars to the basket, open `/Cart`, use the +/− steppers, then *Proceed to checkout*.
3. Fill the form, choose UPI, place the order → you land on the confirmation page with an order number.
4. `/Admin` → sign in (password, then the on-screen code) → the new order is at the top of **Orders**.
5. Click **Mark Packed**, then go to **Products** and hide a jar — refresh `/Shop` and it is gone.
6. `/Admin/Settings` — change the hero headline, save, and check the home page.
