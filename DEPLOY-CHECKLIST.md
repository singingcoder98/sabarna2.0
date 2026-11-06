# After it is hosted — the checklist

> **Status on 6 November 2026.** Live on the MonsterASP.NET free plan at
> `https://sabarna.runasp.net` (site `site105374`), shared with the co-admins as a preview.
> Right now: preview mode **on** (not indexed), access code **off** (anyone with the link gets in),
> the admin one-time code is **drawn on screen** (`Otp__ShowOnScreen`), **no payment gateway**, and
> the content is still the seeded demo catalogue with placeholder contact details.
> Every unticked box below is still to do — nothing else stands between this and a real shop.

Everything that has to happen **once the shop is on a real address**, in the order it should happen.
Tick the boxes as you go; every item says *why*, so nothing has to be remembered from a chat.

The two companion documents: `README.md` (§10 is the deploy walkthrough, §11 is the pre-launch
list) and this file, which is the list for **the days after** deployment.

---

## 0. Decisions already taken — do not re-open them

| Question | Answer | Why |
| --- | --- | --- |
| Vercel? | **No** | no .NET runtime, read-only filesystem (SQLite cannot save an order), sessions die between requests |
| MonsterASP.NET free plan? | **Yes** | real ASP.NET Core 8 + IIS, a free `*.runasp.net` address, nothing to containerise |
| Database? | **one SQLite file** | nothing to install, nothing to connect; `App_Data/app.db` on IIS |
| Preview mode? | **on** | the shared link is `noindex` until launch day |
| Access code? | **off** (`RequireCode: false`) | anyone with the link should see the shop |

Walkthrough for each: `README.md` §10.

---

## 1. The first ten minutes on the new host

Do these before telling anyone the address exists.

- [ ] **The home page loads** at `https://<your-subdomain>.runasp.net`.
- [ ] **`/Shop` shows 13 products** with prices and stock — that means the database seeded.
- [ ] **`App_Data/app.db` exists** in the panel's file manager. This is the real test of write
      permission; a read-only host fails here and nowhere earlier.
- [ ] **A test order goes all the way through:** add to cart → refresh the page (cart survives) →
      checkout → success page → cart is empty.
- [ ] **`/Admin/Login` works:** `admin@sabarna.in` / `Sabarna@2024`, then the six-digit code appears
      in the yellow box, then the dashboard renders.
- [ ] **The yellow "Demo account" box is *absent* on that page** — a public address must never print
      the credentials. It only shows on your own machine (Development). If you see it on the host,
      the environment is not `Production`.
- [ ] **`/Admin/Orders` lists 6 demo orders**; change one to *Shipped* and see the badge move.
- [ ] **Admin edits reach the shopfront:** change a price in `/Admin/Products`, save, refresh
      `/Shop` — new price, no rebuild, no cache to clear.
- [ ] **Narrow the window below 992 px:** navbar collapses to a hamburger, the admin sidebar becomes
      an offcanvas drawer, product grids fall to one column.
- [ ] **Take a backup now**, before any real data exists: download `App_Data/app.db` and keep it as
      "the clean install".

---

## 2. Before you send the link to anyone else

- [ ] **Change the admin password.** `/Admin/Settings` → *Admin password* card at the bottom
      (current password, new password twice). The seeded `Sabarna@2024` is printed in the README —
      a public URL with a published password is how shops get their orders edited by strangers.
- [ ] **Decide who gets in.** `RequireCode: true` in `appsettings.json` (or the
      `Preview__RequireCode` environment variable) puts a single shared code in front of the whole
      shop, 30 days per visitor. Leave it `false` for "anyone with the link".
- [ ] **Enter the real contact details** in `/Admin/Settings` — email, phone, WhatsApp, address.
      Saved to the database and live on the next page render; no redeploy.
- [ ] **Deal with the 6 demo orders.** They look real to a customer. Either delete the rows or keep
      them knowingly (they are useful for showing the admin panel off).
- [ ] **Know what the free plan is.** Cold starts after a quiet spell, a small disk quota, no
      backups of its own. Fine for a preview, not for a launch announcement to 200 people.

---

## 3. Launch day — the switches

Do them in this order, then tell people.

- [ ] **`Preview:Enabled` → `false`.** This is the launch switch. It flips three things at once: the
      `noindex` header disappears, `robots.txt` starts allowing crawlers, and the sitemap becomes
      submittable. Redeploy, or set the `Preview__Enabled` environment variable.
- [ ] **`Otp__ShowOnScreen` → `false`** — but only once e-mail/SMS delivery exists (§4). Until then,
      leaving it `true` means anyone who reaches `/Admin` with the password can read the code.
- [ ] **Google Search Console:** add the property, verify, submit `https://<address>/sitemap.xml`.
      Expect a week or two before searches find the shop.
- [ ] **Replace the placeholders.** All of these are still demo values:

      | Placeholder | Where to change it |
      | --- | --- |
      | Address "P.O. Kharagpur, West Bengal 721301" (was Santiniketan 731235 in the original brief) | `/Admin/Settings` |
      | Phone `+91 98000 12345`, WhatsApp `919800012345`, email `hello@sabarna.in` | `/Admin/Settings` |
      | Product photographs (`wwwroot/images/products/`) | file upload is not built yet — copy files over and set the path |
      | `og-cover.jpg` (the picture WhatsApp shows when the link is shared) | replace the file |

- [ ] **A real domain, when you buy one:** point it at the host, add it to the panel, and the app
      follows the request host on its own — canonical URLs, `og:url` and the sitemap all use
      whatever host the page was served from. No code change.

---

## 4. What is still not built (marked `TODO` in the code)

These are known gaps, not bugs. Each one has a single, obvious place to fix it.

| Feature | Where the TODO lives | What finishing looks like |
| --- | --- | --- |
| ~~**Online payment**~~ | **Code is finished (6 Nov 2026)** — `CartController` + `Services/RazorpayService.cs`. It stays in demo mode until the keys are filled in. See "Payments — the five switches" below. |
| **Admin one-time code by e-mail/SMS** | `Controllers/AdminController.cs` — after the password is accepted | Wire an SMTP or API provider, send `code`, and delete the `Otp__ShowOnScreen` line from `web.config`. |
| **Contact form by e-mail** | `Controllers/ContactController.cs` — after the message is saved | The message is already stored; forward it to the owner's inbox as well. |
| **Newsletter sign-up** | `Controllers/HomeController.cs` — the footer form | Hand the address to a mailing-list provider. |
| **Product image upload** | `Views/Admin/_ProductFormFields.cshtml` | A file input, saved to `wwwroot/images/products/`, with the path written back to the product. |

### Payments — the five switches

The Razorpay code is in place but switched off, so nothing changes until you fill these in:

| Where | Key | Value |
| --- | --- | --- |
| Panel env vars / `web.config` | `Razorpay__Enabled` | `true` |
| | `Razorpay__KeyId` | `rzp_test_…` first, `rzp_live_…` later |
| | `Razorpay__KeySecret` | from the Razorpay dashboard |
| | `Razorpay__WebhookSecret` | a password you invent, typed in both places |
| Razorpay dashboard | Payment capture | **Automatic** — otherwise money sits authorised and never captured |

Webhook URL to add in the Razorpay dashboard, with events `payment.captured`, `payment.failed`,
`order.paid`:

```
https://sabarna.runasp.net/Cart/RazorpayWebhook
```

How an order becomes Paid: only after a signature check on the server — either the one the browser
returns with (HMAC of `order_id|payment_id` with the key secret) or the one on the webhook (HMAC of
the raw body with the webhook secret). A screenshot from a customer is never proof; the order page in
`/Admin/Orders` shows which one confirmed it.

Stock is taken when the order is paid, with a guard per line. If a jar sold out in the meantime the
payment is refunded automatically and the customer is told.

---

## 5. Keeping it alive

- [ ] **Back up `App_Data/app.db` weekly** (monthly is not enough once orders are real). Download it
      through the panel, keep the last four copies, and keep one **off** the host.
- [ ] **Restoring is copying the file back** — stop the app, upload, start. Nothing to import.
- [ ] **Watch the disk quota** on the free plan; product photos are the thing that grows.
- [ ] **After every redeploy,** check that `App_Data` and your orders are still there. If the
      database resets, the publish settings deleted files that were not in the package.
- [ ] **Keep this file updated** with anything you decide on the server — the next person (or the
      next session with an AI) reads the repo, not the chat.

---

## 6. One limitation worth planning around: the cart lives in server memory

Session data is held in the app's memory (`.sabarna.session` cookie, `AddDistributedMemoryCache`).
That is fine for a preview, and **the cart empties whenever the app restarts** — which on shared
hosting means an idle timeout (roughly 20 minutes of no visitors) or any redeploy.

When real customers arrive, pick one:

1. **Cookie cart (recommended).** Keep ids and quantities in a cookie instead of session state. No
   server memory, survives restarts, ~4 KB is plenty for forty items. The change is contained in
   `Services/CartService.cs`.
2. **Cart table.** A `CartItems` table keyed by a cart-id cookie — survives restarts and also lets
   you see abandoned carts.

Do not "fix" it by raising `IdleTimeout`: the store is memory, so the previous process's data is
gone regardless of how long the cookie claims to live.

---

## 7. Never do these

1. Never tick **"Remove additional files at destination"** when republishing — it deletes
   `App_Data/app.db`.
2. Never delete the `App_Data` folder "to clean up".
3. Never upload over `app.db` while the app is running; stop it first.
4. Never leave `Otp__ShowOnScreen` on once mail delivery works.
5. Never post the access code anywhere public if you switch `RequireCode` on.
6. Never commit `app.db` — it is git-ignored on purpose.
7. Never take real money on the free plan with the payment gateway unfinished (§4).

---

## 8. Quick reference

| Thing | Value |
| --- | --- |
| Project folder on this PC | `D:\MVCDotNet\sabarna2.0` |
| MonsterASP site ID (this one changes if the site is recreated) | `site105374` — check the overview page before deploying |
| Local run | `dotnet build Sabarna.sln` → `dotnet run` → <http://localhost:5080> |
| Build the upload folder | double-click `publish.cmd` → everything lands in `publish\` |
| Seeded admin | `admin@sabarna.in` / `Sabarna@2024` — **change it** (§2) |
| Database | `app.db` locally; `App_Data/app.db` on IIS; `/data/app.db` in Docker |
| Session cookie | `.sabarna.session` · Admin cookie `.sabarna.admin` · Preview pass `.sabarna.preview` |
| Switches | `Preview:Enabled`, `Preview:RequireCode`, `Preview:AccessCode`, `Otp:ShowOnScreen` |
| Environment overrides | `ASPNETCORE_ENVIRONMENT`, `ConnectionStrings__Default`, `Preview__Enabled`, `Preview__RequireCode`, `Preview__AccessCode`, `Otp__ShowOnScreen` |
| Money rules | free delivery ≥ ₹999, else ₹49 · qty 1–20 · UPI / Card / Netbanking only |
| Order numbers | `SB-{year}-{1000…9999}` |
