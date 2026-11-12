# Where this project stands

Written 6 November 2026, the day the shop went up on a real address. Read this first when coming
back to the project; `README.md` explains how it works, `DEPLOY-CHECKLIST.md` lists what is left.

## Live right now

| | |
| --- | --- |
| Address | <https://sabarna.runasp.net> |
| Host | MonsterASP.NET, free plan, EU server, site id `site105374` |
| FTP | `site105374.siteasp.net`, port 21, user `site105374` |
| Physical root | `/wwwroot` (upload the *contents* of `publish\`) |
| State | preview mode **on** (noindex), access code **off**, admin code shown on screen |
| Local project | `D:\MVCDotNet\sabarna2.0` |

## What is built

**Storefront.** Home (dark hero, category strip, featured products, CSS-only motion band, testimonials,
orange newsletter), Shop with search, category and price filters plus sorting, product details with
related items, combos page, session cart, checkout, order confirmation, about, contact, the four
policy pages, and a branded error page. 13 seeded products: ghee in three sizes, two honeys, two
pickles, mustard oil, and three jaggery items (powder, blocks, Ghee + Gur combo), plus three combos.

**Cart and orders.** Session JSON cart that survives a refresh, quantity clamped 1–20, free delivery
over ₹999 else ₹49, payment restricted server-side to UPI / Card / Netbanking (no cash on delivery
anywhere), order numbers `SB-2026-1000…`, PRG with toast and error messages, cart emptied on success.

**Admin console.** Cookie login with a six-digit one-time code, dashboard with sales analytics, orders
with search and status changes and CSV export, customers, product management (create, edit, stock,
show/hide, delete), shop settings, and a password-change form. Every POST is antiforgery-protected.

**Data.** EF Core 8 on SQLite, `EnsureCreatedAsync` plus an idempotent `DbSeeder` (13 products,
6 orders across every status, settings, one admin). A brand-migration pass scrubs the old name from
existing databases. The database is one file: `app.db` locally, `App_Data/app.db` on IIS,
`/data/app.db` in Docker.

**Design.** Bootstrap 5.3.3 vendored offline, one custom stylesheet (3,783 lines, no `!important`),
Fraunces and Poppins with `clamp()` scaling, the green/terracotta/gold palette, pill buttons, cards,
alternating cream bands, a dark combos band, sticky navbar, admin sidebar that becomes an offcanvas
drawer under 992 px, focus rings and aria labels throughout.

**SEO and sharing.** Per-page titles and descriptions, canonical URLs, Open Graph and Twitter cards,
JSON-LD for Product, BreadcrumbList and ItemList, `/sitemap.xml` and `/robots.txt` generated from the
database, trailing-slash redirects, and a preview mode that forces `noindex, nofollow` everywhere
while the shop is unlaunched.

**Deployment.** `web.config` for IIS (Production, SQLite in `App_Data`, the on-screen admin code),
`App_Data` published as content, `Dockerfile` + `.dockerignore` for container hosts, `publish.cmd`
for a double-click Release build, and a Visual Studio publish profile for the MonsterASP site.

**Documentation.** `README.md` (13 sections: run it, admin, project map, how the shop works, design
system, deferred work, SEO, preview, deploying, pre-launch, content notes, a tour),
`DEPLOY-CHECKLIST.md` (everything after hosting, with the current state at the top), this file.

## Size of the thing

32 Razor views · 7 controllers, 44 actions · 17 C# files · 1 stylesheet (3,783 lines) · 25 images
(logo, mark, favicon, apple-touch, hero, about, OG cover, 13 product photos) · 26 commits on
`arena/c683ad80-sabarna2-0`.

## Picking it up again

```bash
git checkout arena/c683ad80-sabarna2-0
git pull
dotnet build Sabarna.sln      # 0 errors, 0 warnings
dotnet run                    # http://localhost:5080
```

Admin: `admin@sabarna.in` / `Sabarna@2024` (change it in `/Admin/Settings` if not already done).
Reset the demo data by deleting `app.db` and running again.

## Pushing a change to the live site (site105374, sabarna.runasp.net)

> Hand this to anyone who has never deployed: **`UPLOAD-STEPS.md`** says the same thing in plain
> language — where `publish.cmd` lives, what each window should show, and what to do when it breaks.

Anything you change in Visual Studio stays on this PC until it is published. Six steps, every time:

1. **Build first.** `Ctrl+Shift+B`. Fix errors here — the host never shows a build error, only a 500.
2. **Double-click `publish.cmd`** in `D:\MVCDotNet\sabarna2.0`. It wipes and rebuilds `publish\`, and
   deletes any local `App_Data\app.db` that slipped in. Wait for `Done. Folder: …\publish`.
3. **Stop the site in the panel** (MonsterASP → your site → *Scripting* → **Stop application**), or
   upload will fail on `Sabarna.Web.dll` with "permission denied" because IIS has it open.
4. **FileZilla**: host `site105374.siteasp.net`, port `21`, FTP over TLS, user `site105374`, password
   from the panel. Remote folder `/wwwroot`. Select **everything inside `publish\`** (not the folder)
   and upload, choosing **Overwrite**. Never tick "remove files at destination".
5. **Never upload `App_Data`.** `publish\App_Data` only contains a README, but the server's
   `App_Data\app.db` is the shop: orders, products, settings. Skipping the folder is free insurance.
   (Column changes are applied automatically on start by `Data/SchemaPatcher.cs` — that is why the
   database must be left alone.)
6. **Start the site again**, open the shop and press **`Ctrl+Shift+R`**. CSS and JS are
   version-stamped, images are not, so a plain refresh may still show the old logo.

If a free-plan upload drops halfway: reconnect, re-queue the same files, and check the file count in
`/wwwroot` against `publish\` before starting the site.

## What is next

`DEPLOY-CHECKLIST.md` §4 is the list, in priority order. The four that matter:

1. **Razorpay** — `Controllers/CartController.cs`, the marked TODO. Until it exists no order takes real money.
2. **Admin code by e-mail or SMS** — `Controllers/AdminController.cs`, then delete `Otp__ShowOnScreen` from `web.config`.
3. **Contact form and newsletter delivery** — `ContactController.cs`, `HomeController.cs`.
4. **Cart that survives a restart** — session lives in memory; a cookie cart in `CartService.cs` fixes it.
