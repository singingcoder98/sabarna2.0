# Apna domain kharid kar site LIVE karna — exact steps

> saptarshi: 18-Nov-2026 — ye file sirf aapke liye hai (owner). Isme code change **zero** hai.
> Site pehle se hi domain ke liye taiyaar hai: har absolute URL (canonical, og:url, sitemap,
> JSON-LD, llms.txt) `Seo:BaseUrl` se banta hai. Bas 3 setting badalni hai aur DNS jodna hai.

---

## 0. Sabse pehle: sahi order

Galat order me karne par Google **temporary address (`site105374.sabarna.runasp.net`) ko index
kar lega** aur baad me use hatana mushkil hota hai.

Sahi order:

| # | Kaam | Kyun pehle |
|---|---|---|
| 1 | Domain kharido | — |
| 2 | Panel me domain jodo + **free SSL** issue karo | SSL ke bina HTTPS nahi chalega |
| 3 | `https://domain` khul ke dikhne lage | — |
| 4 | **Ab** `Seo__BaseUrl` set karo | sitemap/canonical naye domain par jayenge |
| 5 | **Ab** `Preview__Enabled=false` karo | is se pehle off kiya to runasp address index ho jata |
| 6 | Search Console me sitemap submit | — |

> **Rule:** Jab tak `https://aapka-domain` browser me 200 OK na de, tab tak Preview **ON** rehne do.

---

## 1. Domain kharidna (₹500–1,200 / saal)

Naam ke options (availability khud check karein — GoDaddy / Namecheap / Hostinger ke search box me):

- `sabarnapure.in` ← **best** (brand + `.in` = India signal, local SEO me chhota bonus)
- `sabarnapure.com`
- `sabarna.in` (chhota, yaad rakhne me aasan — par sabse pehle ye check karein, jaldi bik jate hain)

Kya **na** karein:

- Hosting saath me na kharidein — MonsterASP wali hosting chalu hai.
- Privacy protection (WHOIS guard) free milta hai to lein, warna ₹300 extra — optional.

---

## 2. MonsterASP panel me domain jodna

1. `admin.monsterasp.net` → login → site `site105374` → **Domains** (ya *Custom Domains*)
2. **Add Domain** → `sabarnapure.in` type karein → Save
3. Panel do me se ek tareeka batayega:

**Tareeka A — Nameservers (aasan, recommended)**

- Panel 2–4 nameserver dikhayega, jaise `ns1.monsterasp.net`
- Domain registrar ke panel me → **DNS / Nameservers** → *Custom nameservers* → wahi daal dein
- 30 min – 24 ghante me chalu

**Tareeka B — A record**

- Panel ek IP (jaise `178.63.…) dikhayega
- Registrar → DNS → `A` record: Host `@` → value = wahi IP → TTL 3600
- `www` ke liye: `CNAME` → Host `www` → value = `sabarnapure.in`

4. **SSL (zaroori):** Panel → **SSL / Certificates** → domain chunein → **Issue free Let's Encrypt**
   - Ye DNS resolve hone ke **baad** hi chalega. 5–15 min lagte hain.
   - Issue hone par panel me **"Force HTTPS"** / *Redirect to HTTPS* ON kar dein.

5. Check: browser me `https://sabarnapure.in` → tala (🔒) dikhe aur **aapki site** khule.
   DNS check: `dnschecker.org` par domain daal kar dekhein sab jagah green ho.

---

## 3. App ki 3 setting — panel ke Environment Variables me

MonsterASP panel → site → **Environment Variables** (kuch panel me *App Settings* kehte hain) →
Add / Edit:

| Name | Value | Kyun |
|---|---|---|
| `Seo__BaseUrl` | `https://sabarnapure.in` | canonical, og:url, sitemap, JSON-LD sab isi par |
| `Preview__Enabled` | `false` | `noindex` header hat jayega, site index ho sakegi |
| `Security__ForceHttps` | `true` *(ya variable delete kar dein — default hi `true` hai)* | pehle `false` kiya tha kyunki SSL nahi tha; ab SSL hai |

> **Note:** `Seo__BaseUrl` me **https** aur **bina last slash** — `https://sabarnapure.in` ✅,
> `http://sabarnapure.in/` ❌.

Save karne ke baad **Restart** karein (panel → Scripting → Restart).

### Ya phir appsettings.json se (dono me se ek hi kaafi hai)

`appsettings.json` me:

```json
"Preview": { "Enabled": false },
"Seo": { "BaseUrl": "https://sabarnapure.in" },
"Security": { "ForceHttps": true }
```

par iske liye file upload karni padegi. **Environment Variable better hai** — appsettings.json
naye publish me overwrite ho jata hai, env var nahi.

---

## 4. Purana address 301 se naya domain par (recommended)

Isse jo WhatsApp/Google link `site105374.sabarna.runasp.net` par hain wo naye domain par
permanently redirect ho jayenge, aur duplicate content ka risk khatam.

**Yahan `web.config` upload nahi karna** — panel ke **File Manager** se server wali copy edit karni hai:

1. Panel → **File Manager** → root (`/`) → `web.config` → pehle **Download** karke backup rakhein
2. `<system.webServer>` ke andar, `</system.webServer>` se **pehle** ye block paste karein:

```xml
    <rewrite>
      <rules>
        <!-- saptarshi: purane runasp address se naye domain par permanent redirect -->
        <rule name="OldHostToNewDomain" stopProcessing="true">
          <match url="(.*)" />
          <conditions>
            <add input="{HTTP_HOST}" pattern="^(?!sabarnapure\.in$)(?!www\.sabarnapure\.in$)(.+)$" />
          </conditions>
          <action type="Redirect" url="https://sabarnapure.in/{R:1}" redirectType="Permanent" />
        </rule>
        <!-- www ko bina www par (ek hi address) -->
        <rule name="WWWtoRoot" stopProcessing="true">
          <match url="(.*)" />
          <conditions>
            <add input="{HTTP_HOST}" pattern="^www\.sabarnapure\.in$" />
          </conditions>
          <action type="Redirect" url="https://sabarnapure.in/{R:1}" redirectType="Permanent" />
        </rule>
      </rules>
    </rewrite>
```

3. Save → Restart → `site105374.sabarna.runasp.net` type karke dekhein: naye domain par bhej dena chahiye.

> ⚠️ Ye step **optional** hai. Na karein to bhi site chalegi (canonical naya domain batata hai),
> bas purana address bhi khula rahega.

---

## 5. Email naye domain se (spam se bachne ke liye — important)

Abhi Brevo kisi verified sender se mail bhejta hai. Domain lene ke baad:

1. Brevo → **Senders, Domains & IPs** → **Domains** → Add `sabarnapure.in`
2. Brevo 3–4 DNS record dega (2 DKIM + 1 Brevo code + SPF) → registrar ke DNS me add karein
3. Verify (5 min – 24 ghante)
4. Panel → Environment Variables: `Email__FromAddress` = `orders@sabarnapure.in`
   (`Email__UserName`/`Password` Brevo wale hi rahenge)

Iske bina order-confirmation mail **spam** me ja sakta hai.

---

## 6. Google Search Console + Bing (go-live ke baad pehle din)

1. `search.google.com/search-console` → **Add property** → **Domain** type → `sabarnapure.in`
2. Verify: **DNS TXT record** (registrar me add) — sabse aasan, ek hi baar
3. **Sitemaps** → `sitemap.xml` submit → `https://sabarnapure.in/sitemap.xml`
4. `bing.com/webmasters` → same tarika, sitemap submit
5. **URL Inspection** → `https://sabarnapure.in/` → *Request indexing*

Check: `https://sabarnapure.in/llms.txt` aur `/robots.txt` khulne chahiye aur robots.txt me
`Disallow` **nahi** hona chahiye (Preview off hone ke baad).

---

## 7. Google Business Profile

- Business name: **SABARNA PURE**
- Address: **Vivekananda Nagar, Madhyamgram, Kolkata, West Bengal 700129**
- Website: `https://sabarnapure.in` ← naya domain
- Category: *Pickle store* / *Food products supplier*
- Phone + photo + timings = website par jo likha hai **bilkul wahi** (NAP match)

---

## 8. Razorpay (jab start karein)

- KYC me **Website** field = `https://sabarnapure.in`
- Webhook URL = `https://sabarnapure.in/Cart/RazorpayWebhook`
- Callback/redirect khud handle hota hai, kuch set karne ki zarurat nahi

---

## 9. Go-live checklist (tick karke aage badhein)

| # | Cheez | Kaise check karein |
|---|---|---|
| 1 | Domain resolve | `https://sabarnapure.in` khulta hai |
| 2 | SSL tala 🔒 | URL ke aage tala |
| 3 | http → https | `http://sabarnapure.in` type karein, https par aa jaye |
| 4 | `Seo__BaseUrl` | page source me `<link rel="canonical" href="https://sabarnapure.in/">` |
| 5 | Preview off | page source me **`noindex` nahi** hona chahiye |
| 6 | sitemap | `/sitemap.xml` me `https://sabarnapure.in/...` URLs |
| 7 | llms.txt | `/llms.txt` me naya domain |
| 8 | Product page | `/product/kuler-achar-500g` khulta hai, price + photo |
| 9 | Guides | `/guides` — 6 articles |
| 10 | Cart + COD order | ek test order khud karein |
| 11 | Admin login | `/Admin/Login` — password + OTP |
| 12 | Email | test order par confirmation mail aaye (spam folder bhi dekh lein) |
| 13 | Old address | `site105374.sabarna.runasp.net` → naya domain par redirect |

---

## 10. Kya nahi karna

- ❌ `App_Data` upload — orders/DB chale jayenge
- ❌ `web.config` **upload** — server wali copy me email + HTTPS setting hai (edit karna hai to File Manager se, backup ke baad)
- ❌ Domain set hone se pehle `Preview__Enabled=false`
- ❌ `Seo__BaseUrl` me last slash ya `http://`
- ❌ Domain ke bina Google Business Profile me website daalna
