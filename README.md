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

### Is the database actually connected?

There is nothing to connect *to* and nothing to install — SQLite is a library inside the app and the
database is one file on disk. Three lines explain the whole thing:

| Where | What it says |
| --- | --- |
| `appsettings.json` | `"ConnectionStrings": { "Default": "Data Source=app.db" }` — the file name, no server, no password |
| `Program.cs` | `AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString))` — every page talks to that one context |
| `Program.cs` | `EnsureCreatedAsync()` then `DbSeeder.SeedAsync()` — creates the file and fills it on first start |

Four ways to see it with your own eyes:

1. **The file appears.** Run the app, then look in the project folder next to `Sabarna.Web.csproj`
   — `app.db` is there (it is git-ignored, so tick *Show All Files* in Solution Explorer). It is
   **not** in `bin\`, because the content root is the project folder.
2. **The shop shows data.** Open `/Shop` — 13 products, prices, stock. Those rows came out of the
   file, not out of the view.
3. **The admin writes to it.** Change a price in `/Admin/Products`, save, refresh `/Shop` — the new
   price is already live. One database, two windows onto it.
4. **Delete it and watch it come back.** Stop the app, delete `app.db`, start again: the file
   reappears with the demo catalogue, because the app creates and seeds it itself.

To look inside, install **DB Browser for SQLite** (<https://sqlitebrowser.org>, free, Windows) and
open `app.db`. Tables: `Products`, `Categories`, `Orders`, `OrderItems`, `Settings`, `AdminUsers`.
Close the app before editing rows there — SQLite gives one writer at a time.

**Moving the file** is one line: `"Default": "Data Source=App_Data/app.db"` in `appsettings.json`
(the folder must exist first). That is exactly what the deployed `web.config` does — see §10.

> The local `app.db` is *yours*, not the site's. It is git-ignored, so whatever orders and edits you
> make on this machine stay on this machine. A freshly deployed host seeds its own empty database.

---

## 3. Admin panel

| | |
| --- | --- |
| URL | <http://localhost:5080/Admin> |
| Email | `admin@sabarna.in` |
| Password | `Sabarna@2024` |
| One-time code | **shown on screen** at `/Admin/VerifyOtp` while running in Development (or when `Otp:ShowOnScreen` is on) |
| Change the password | `/Admin/Settings` → *Admin password* card: current password, new password twice |

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
"No email is sent yet" box because no mail provider is connected yet — see §7.

> **The seeded credentials above are never printed by a hosted site.** The yellow box on
> `/Admin/Login` that shows them is drawn only when the app runs locally (Development), so a
> visitor on your public address sees a plain sign-in form. The password is in this README, which
> is where only the owner looks — change it anyway (`/Admin/Settings`), and see
> `DEPLOY-CHECKLIST.md` §2.

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
appsettings.json                 ConnectionStrings:Default = "Data Source=app.db", Preview switches
Properties/launchSettings.json   http://localhost:5080, ASPNETCORE_ENVIRONMENT=Development
Dockerfile                       .NET 8 build + runtime image, port 8080, database at /data
web.config                       IIS entry point + env vars for shared hosting (MonsterASP, Azure)
DEPLOY-CHECKLIST.md              everything to do after hosting: first checks, security, launch day
App_Data/                        where SQLite writes app.db on IIS; published, never served
.dockerignore                    keeps bin/, obj/ and app.db out of the image

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
  PreviewController.cs           /Preview/Gate — the door in front of an unlaunched shop

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
  PreviewOptions.cs              the "Preview" section of appsettings, bound to a class
  PreviewGateMiddleware.cs       noindex while unlaunched, optional shared access code

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
  Preview/                       Gate
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

## 9. Showing it to people before launch (no domain yet)

The shop can live at a temporary address for as long as you need, without buying a domain and
without it ever appearing in Google.

**A link, in one command.** On the machine running the shop, with `dotnet run` already going:

```bash
# Cloudflare's quick tunnel — free, no account, prints a public https link
winget install --id Cloudflare.cloudflared     # Windows, once
cloudflared tunnel --url http://localhost:5080
# → https://something-random.trycloudflare.com
```

Send that link to whoever needs to see it. `ngrok http 5080` does the same job if you prefer ngrok
(free account required). The link works **only while your PC, the app and the tunnel are running**,
and a quick tunnel gets a new name each time it starts — which is fine for "look at this and tell
me what you think", and the reason nothing must be indexed under it.

**It cannot end up in Google.** `Preview:Enabled` is `true` in `appsettings.json`, which makes the
shop do all of this:

* send `X-Robots-Tag: noindex, nofollow` on every response,
* write `<meta name="robots" content="noindex, nofollow">` into every page,
* serve a `robots.txt` that disallows everything,
* and leave the sitemap unsubmitted (there is nothing to submit until launch).

**Only the people you send it to** — if you want the link to ask for a code as well, flip one
switch:

```json
  "Preview": {
    "Enabled": true,
    "RequireCode": true,
    "AccessCode": "choose-something-only-your-people-know"
  }
```

Visitors then land on `/Preview/Gate`, type the code once, and get a cookie for 30 days. Changing
`AccessCode` invalidates every cookie already handed out. The admin panel keeps its own two-step
login and is not affected.

**Two details that make the link behave properly**

* Requests come in through the tunnel as plain HTTP on localhost. `UseForwardedHeaders` is wired up
  first in the pipeline, so `https`, the public host name, canonical URLs, the sitemap and the
  preview cookie's `Secure` flag all reflect what the visitor actually sees.
* `AllowedHosts` is `*` in `appsettings.json`, so a random tunnel host name is served rather than
  being rejected by Kestrel.

**On launch day**, three things: point the domain at the host, set `"Enabled": false`, and only
then submit `/sitemap.xml` in Google Search Console. Everything in §8 starts working the moment
that switch goes off.

---

## 10. Deploying it somewhere permanent

### Vercel will not run this

Vercel runs static files plus serverless functions in Node, Python, Go and Ruby. There is no .NET
runtime, and even a hand-written function would not help:

| What the shop needs | What Vercel gives you |
| --- | --- |
| A long-running ASP.NET Core process (Kestrel) | short-lived serverless invocations |
| A writable disk — SQLite is a file, written by every order | a read-only bundle plus `/tmp` that is wiped |
| A session store that survives between requests | a new, empty process for most requests (the cart would empty itself) |

The failure would be loud on the first checkout, not subtle: `EnsureCreated`/`SaveChanges` throws on
a read-only filesystem. Pulling this project into Vercel will build nothing. (If you ever want the
Vercel dashboard to look busy, that is exactly what it is for — Next.js, not ASP.NET.)

### The three routes that do work

**1. A link today, no domain, no card** — the tunnel in §9. Free, and the shop runs on your own PC.

**2. Free .NET hosting — MonsterASP.NET, step by step.** Nothing to containerise, and the free plan
is enough for a preview shop on a `something.runasp.net` address. (Azure App Service F1 works the
same way: publish from Visual Studio, then set the same two environment variables.)

*Part A — create the site*

1. **Plan:** Free.
2. **Domain:** choose **Free subdomain**, type a name (min 3 characters: letters, digits, dashes —
   e.g. `sabarna`), then pick `runasp.net` or `tryasp.net`. Your address becomes
   `sabarna.runasp.net`.
3. **Subscription / Package:** whatever the free option says.
4. **Location / Datacenter:** the one closest to your visitors (Europe is usually fine from India).
5. **Example application:** choose **Empty** if it is offered — a sample app is only something to
   delete afterwards.
6. **Create**, then open the site in the panel and set the runtime to **ASP.NET Core 8.0**. That one
   setting decides whether the site runs at all.

*Part B — publish the files*

From Visual Studio (easiest):

1. Right-click the project → **Publish** → target **Web Server (IIS)** → **Web Deploy**.
2. Copy the four values from the panel's **Publish / Web Deploy** page: server
   (`https://…:8172/msdeploy.axd?site=…`), site name, user name, password.
3. Destination URL: `https://sabarna.runasp.net`.
4. **Settings** → Configuration `Release`, Target framework `net8.0`, and **untick "Remove
   additional files at destination"** — that checkbox wipes `App_Data/app.db` on every redeploy,
   and that file is the entire shop.
5. **Publish.**

*Faster: use the checked-in publish profile.* The repository already contains
`Properties/PublishProfiles/MonsterASP.pubxml`, filled in with the site's Web Deploy address, site
name and user name (password blank — Visual Studio asks once and stores it encrypted on your
machine). Right-click the project → **Publish** → pick **MonsterASP**, or *Import profile* if it is
not listed. It already sets Release, keeps remote files (`DeleteExistingFiles=False`, so
`App_Data/app.db` survives), and excludes the local demo database.

> **The physical root is `\wwwroot` on the host.** Whatever route you use, `Sabarna.Web.dll`,
> `web.config` and `App_Data` must end up directly in that folder — not in a `publish` subfolder and
> not inside `wwwroot\wwwroot`. The project's own `wwwroot` (the one with `css/`, `images/`) travels
> as a subfolder inside the published output, which is correct: the site's content root is the folder
> the DLL sits in.

From the command line or the panel's file manager:

```bash
dotnet publish Sabarna.Web.csproj -c Release -o publish
```

Upload the **contents of `publish`** (not the folder itself) to the site root over FTP, or zip them
and extract with the file manager. `Sabarna.Web.dll`, `web.config` and `App_Data` must all sit in
the root.

*Part C — what the shipped `web.config` already does*

Nothing to configure in the panel. It sets:

| Setting | Value | Why |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | friendly error page, HSTS, no stack traces |
| `ConnectionStrings__Default` | `Data Source=App_Data/app.db` | IIS keeps `App_Data` writable |
| `Otp__ShowOnScreen` | `true` | no mail provider yet — the admin code is drawn on screen |

That last line is a deliberate bridge, not a feature: delete it the day e-mail/SMS delivery is
wired up (README §7), otherwise anyone who reaches `/Admin` with your password can read the code.

*Part D — check it*

1. Open `https://sabarna.runasp.net` — the home page should render. The first request creates
   `App_Data/app.db` and seeds the catalogue.
2. Go to `/Admin/Login`, sign in with `admin@sabarna.in` / `Sabarna@2024`; the six-digit code
   appears in the yellow box on the next screen.
3. The free address is not indexed: `Preview:Enabled` is still `true`, so the whole site is
   `noindex` until launch day.
4. Download `App_Data/app.db` now and then — that file is the shop.

*If it does not come up*

| Symptom | Cause and fix |
| --- | --- |
| **500.19** | Runtime not set to ASP.NET Core 8.0 in the panel, or `web.config` is missing from the site root |
| **500.30** | The app started and crashed — usually the database path; check `logs/stdout` after setting `stdoutLogEnabled="true"` |
| `unable to open database file` | `App_Data` was not published, or is not writable — create it with the file manager |
| **404 on every page** | Files landed in a subfolder (`publish`, `wwwroot`) instead of the site root |
| Login works, no code on screen | The `Otp__ShowOnScreen` line is missing from `web.config` |

**Free tier caveats.** These plans are for showing the shop to people, not for taking money: the
first request after a quiet spell can be slow, disk quotas are small, and the only backup of
`App_Data/app.db` is the one you take yourself.

**3. Any Docker host** — Fly.io, Railway, Render, or a ₹400/month VPS (DigitalOcean, Hetzner). The
repository ships a `Dockerfile` for exactly this:

```bash
docker build -t sabarna .
docker run -p 8080:8080 -v sabarna-data:/data sabarna
```

The image listens on **8080**, runs as the non-root `app` user, keeps the database at
`/data/app.db` and sets `ASPNETCORE_ENVIRONMENT=Production`. On Fly/Railway/Render, attach a volume
at `/data` — without it every redeploy starts the shop empty, because a container's own filesystem
is discarded.

### Settings every host understands

Configuration is read from environment variables, so nothing has to be edited in the image:

| Variable | Why |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` — HSTS on, the dev OTP box hidden, the error page instead of a stack trace |
| `ConnectionStrings__Default` | where `app.db` lives (the only path that must be persistent) |
| `Preview__Enabled` | `true` while unlaunched (noindex), `false` on launch day |
| `Preview__RequireCode` / `Preview__AccessCode` | the optional shared code |
| `ASPNETCORE_URLS` | already `http://+:8080` in the image; set it if you bind the app yourself |

Two things people forget: behind any proxy or tunnel `UseForwardedHeaders` is already wired up (so
`https` and the public host are correct), and `AllowedHosts` is `*` in `appsettings.json`, so a new
host name is served rather than rejected.

> **Back up `app.db`.** It is the entire shop — products, orders, settings. On a VPS that is
> `cp app.db backup/`; on a managed host, download the file now and then. Moving to Azure SQL or
> Postgres later is a change of one package and one connection string, not a rewrite.

**After the deploy, switch to `DEPLOY-CHECKLIST.md`.** It is the list for the days after hosting —
the ten-minute smoke test, the security switch-flip before you share the link, launch day, backups,
and the five features that are still TODO comments in the code.

---

## 11. Before launch checklist

The full version, with reasons and order, is in **`DEPLOY-CHECKLIST.md`**. The short version:

* [ ] **Change the admin password** — `/Admin/Settings` → *Admin password*. The seeded
      `Sabarna@2024` is in this README and in every clone of the repository.
* [ ] **Decide on the preview gate** — `Preview:RequireCode` (see §9), then leave the link open or
      put the shared code in front of it.
* [ ] **Turn preview off on launch day** — `Preview:Enabled: false`. That single switch removes the
      `noindex` header and lets `robots.txt` and `sitemap.xml` be crawled.
* [ ] **HTTPS** — any real host or tunnel provides it; the app itself listens on plain HTTP behind it
      and trusts the `X-Forwarded-*` headers (`UseForwardedHeaders`).
* [ ] **Real photographs** in `wwwroot/images/` and `wwwroot/images/products/` (keep the file names,
      or update the paths in `/Admin/Products`).
* [ ] **Real contact details and address** in `/Admin/Settings` — no redeploy needed.
* [ ] **Read the three policies** in `Views/Home/{Privacy,Terms,Shipping}.cshtml` with the owner —
      they are plain language on purpose and must match what the shop actually does.
* [ ] **Back up `App_Data/app.db`** on a schedule — that file holds every order.
* [ ] **Do not take real money yet** — the payment gateway is still a TODO (`CartController`), so
      every order is recorded as paid without a payment. See `DEPLOY-CHECKLIST.md` §4.

---

## 12. Content notes

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

## 13. Hands-on with the running site (a 3-minute tour)

1. `/` — hero, trust marquee, product grid, combos, FAQ, newsletter.
2. Add two jars to the basket, open `/Cart`, use the +/− steppers, then *Proceed to checkout*.
3. Fill the form, choose UPI, place the order → you land on the confirmation page with an order number.
4. `/Admin` → sign in (password, then the on-screen code) → the new order is at the top of **Orders**.
5. Click **Mark Packed**, then go to **Products** and hide a jar — refresh `/Shop` and it is gone.
6. `/Admin/Settings` — change the hero headline, save, and check the home page.
