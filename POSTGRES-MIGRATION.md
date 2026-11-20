# SQLite se PostgreSQL par shift karna — pura tarika

> saptarshi: 18-Nov-2026 — ye file owner ke liye hai. Code me sab kuch ho gaya hai; bas database
> banana hai aur **2 environment variable** daalne hain. Build karne ke baad hi ye chalega, kyunki
> `Npgsql.EntityFrameworkCore.PostgreSQL` package naya juda hai.

---

## 0. Pehle ye samajh lein: kya badalta hai, kya nahi

| Cheez | Pehle (SQLite) | Ab (PostgreSQL) |
|---|---|---|
| Database | ek file `App_Data/app.db` | ek alag server (internet par) |
| Backup | file copy, `/Admin/Backups` se download | JSON export + provider ke apne copies |
| Speed | bahut tez (local file) | network se thoda slow, par 13 products par farak nahi |
| Do log ek saath admin | file lock lag sakta hai | koi dikkat nahi |
| Restore | file wapas rakh do | provider ke dashboard se point-in-time |
| Aapka kaam | kuch nahi | 2 env var |

**Important:** SQLite file **delete nahi hoti**. Aap `Database__Provider` hata dein to site wapas
SQLite par chali jayegi — purane orders sameth. Yaani ye switch **do-tarfa** hai, ek-tarfa nahi.

---

## 1. PostgreSQL kahan se lein

MonsterASP ke free plan me Postgres ho bhi sakta hai aur nahi bhi — panel me
**Databases → Add database** khol kar dekhein. `PostgreSQL` ka option dikhe to wahi sabse aasan hai.

Nahi dikhe to ye free options hain (site kahin bhi host ho, chalega):

| Service | Free limit | Notes |
|---|---|---|
| **Neon** (`neon.tech`) | 0.5 GB, hamesha free | sabse aasan; automatic point-in-time restore |
| **Supabase** (`supabase.com`) | 500 MB | dashboard + storage bhi deta hai |
| **Aiven** | 1 GB trial | 1 mahine baad band |
| Railway / Render | paid | — |

> 13 products aur mahine ke kuch sau orders ke liye **0.5 GB barson chalega**.

### Neon par database banane ka tarika (5 minute)

1. `neon.tech` → Sign up (GitHub/Google se)
2. **Create project** → Name: `sabarna` → Region: **Singapore** (India se sabse kam latency)
3. Dashboard → **Connection string** → *Pooled connection* copy karein. Aisa dikhega:
   ```
   postgresql://neondb_owner:AbCdEf123@ep-cool-name-123456.ap-southeast-1.aws.neon.tech/neondb?sslmode=require
   ```
4. Us link ko **.NET format** me badalna hai (Npgsql `postgresql://` link nahi padhta):
   ```
   Host=ep-cool-name-123456.ap-southeast-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=AbCdEf123;SSL Mode=Require;Trust Server Certificate=true
   ```

---

## 2. App ko Postgres par le jana — 2 environment variables

MonsterASP panel → site `site105374` → **Environment Variables** → Add:

| Name | Value |
|---|---|
| `Database__Provider` | `Postgres` |
| `ConnectionStrings__Default` | `Host=…;Port=5432;Database=neondb;Username=…;Password=…;SSL Mode=Require;Trust Server Certificate=true` |

> ⚠️ Do underscore `__` (double), ek nahi. `Database__Provider`, `ConnectionStrings__Default`.

Phir **Restart** karein.

### Kya hoga pehli baar

1. App Postgres se judega
2. `EnsureCreated()` saari tables bana dega (Products, Orders, OrderItems, Settings, …)
3. Seeder 13 products, address, phone — sab bhar dega
4. Log me dikhega: `Database provider: Postgres.`
5. `/Shop` kholein — 13 products, sahi price

**Admin password dobara set karna padega** (naya database = naya user). `/Admin/Login` →
"Forgot password?" / setup screen.

---

## 3. Pehle wala data (orders) le jana — agar chahiye

Site preview par hai, to zyadatar orders test hi honge — unhe chhodna theek hai.
Par agar aap kuch rak hna chahte hain:

1. **Switch se PEHLE** SQLite par: `/Admin/Backups` → **"Download all data (JSON)"**
2. File `sabarna-YYYYMMDD-HHmmss.json` aapke Downloads me aa jayegi — usme har product, har order,
   har setting plain text me hai (Notepad me khul jayegi)
3. Switch ke baad wo file aapke paas safe hai; usse rows wapas daalne ke liye mujhe bol dein,
   main import script bana dunga

---

## 4. Backup ka naya matlab

| | SQLite | PostgreSQL |
|---|---|---|
| `/Admin/Backups` | `sabarna-*.db` files | `sabarna-*.json` snapshots |
| Automatic | roz ek copy | roz ek JSON snapshot |
| Download | haan | haan |
| "Download all data (JSON)" | haan | haan |

JSON snapshot **poora data rakhta hai** (orders + items + payment log + products + settings) — par
ye database ka byte-copy nahi hai. Isliye:

- **Point-in-time restore** → Neon/Supabase dashboard se (Neon me 7 din tak "Restore" button)
- **Long-term copy** → mahine me ek baar JSON download karke PC me rakh lein

---

## 5. Connection string ke examples

**Neon**
```
Host=ep-xxx-123456.ap-southeast-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=***;SSL Mode=Require;Trust Server Certificate=true
```

**Supabase** (Settings → Database → Connection string → *Session pooler*)
```
Host=aws-0-ap-south-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.abcdefgh;Password=***;SSL Mode=Require;Trust Server Certificate=true
```

**Local PC par test** (PostgreSQL installed)
```
Host=localhost;Port=5432;Database=sabarna;Username=postgres;Password=***;SSL Mode=Prefer
```

---

## 6. Agar kuch galat ho — error aur uska matlab

| Log me ye dikhe | Matlab | Fix |
|---|---|---|
| `Database provider: Sqlite` (Postgres chahiye tha) | env var nahi mila | naam check: `Database__Provider` = `Postgres`, phir Restart |
| `28P01: password authentication failed` | password galat | connection string dobara copy |
| `no pg_hba.conf entry ... SSL off` | SSL chahiye | `SSL Mode=Require` add karein |
| `The server does not support SSL` | local Postgres | `SSL Mode=Prefer` |
| `Name or service not known` / timeout | host galat ya firewall | `Host=` check; Neon par `-pooler` wala host try karein |
| `relation "Products" already exists` | pehle se tables hain | normal nahi hota; database naya banayein |
| Site 500, log me `Npgsql` | connection fail | app start hota hai par pages error denge; env var theek karke Restart |
| 13 products ki jagah 0 | seeding fail | log me `The database could not be prepared` dekhein |

Error kahan dikhega: MonsterASP panel → **Logs** / `App_Data/logs` ya panel ka log viewer.

---

## 7. Wapas SQLite par (panic button)

1. Panel → Environment Variables → `Database__Provider` **delete** kar dein
   (`ConnectionStrings__Default` bhi delete — warna SQLite usse parse nahi kar payega)
2. Restart
3. Site purane `App_Data/app.db` par wapas — orders sameth, jaisa tha waisa

> Isliye `app.db` ko **delete na karein** jab tak Postgres par 2–3 hafte sukoon se na chal jaye.

---

## 8. Aage kya (jab shop badi ho)

Abhi schema `EnsureCreated()` se banta hai — naye database ke liye theek hai, par baad me column
add karna mushkil hota hai (SQLite ke liye `SchemaPatcher` hai, Postgres me kuch nahi).

Jis din doosri baar schema badalna ho, us din **EF Core Migrations** on kar dena chahiye:

```
dotnet tool install --global dotnet-ef
dotnet ef migrations add FirstPostgresMigration
```

Tab se har column change ek migration file ban jayega, aur production me `Database.Migrate()` chalega.
Ye kaam main aage kar sakta hoon — bolein to.

---

## 9. Checklist

| # | Kaam | ✅ |
|---|---|---|
| 1 | Postgres database bana (Neon/Supabase/host) | |
| 2 | Connection string `.NET` format me likha | |
| 3 | (Optional) SQLite par `/Admin/Backups` → Download all data (JSON) | |
| 4 | `Database__Provider` = `Postgres` | |
| 5 | `ConnectionStrings__Default` = poora string | |
| 6 | Restart | |
| 7 | Log me `Database provider: Postgres` | |
| 8 | `/Shop` — 13 products | |
| 9 | Admin login + naya password | |
| 10 | Test COD order → `/Admin/Orders` me dikhe | |
| 11 | `/Admin/Backups` → "Back up now" → `.json` file bane | |

---

# PART 2 — Download kya karna hai, aur setup ka poora order

> saptarshi: 18-Nov-2026 — neeche har cheez ka naam, size aur jagah likhi hai. Jahan "optional" likha
> hai wahan sirf dekhne ke liye hai, kaam ke liye zaroori nahi.

## A. Download list

| # | Cheez | Zaroori? | Size | Kahan se |
|---|---|---|---|---|
| 1 | **Npgsql.EntityFrameworkCore.PostgreSQL** | ✅ haan | ~1.5 MB | **Visual Studio khud le aayega** — NuGet restore. Alag se kuch nahi karna |
| 2 | **.NET 8 SDK** | ✅ (pehle se hai) | 200 MB | VS ke saath already installed — project `net8.0` par chalta hai |
| 3 | **PostgreSQL server** | ❌ **NAHI** | 350 MB | Iski zarurat nahi — cloud (Neon) par database rahega |
| 4 | **pgAdmin 4** | 🟡 optional | 130 MB | `pgadmin.org/download` — database ko aankhon se dekhna ho to. Nahi chahiye to Neon ka **SQL Editor** browser me hi kaam kar dega |
| 5 | **DBeaver Community** | 🟡 optional (pgAdmin ka behtar option) | 120 MB | `dbeaver.io/download` — ek hi tool me Postgres + SQLite dono khul jate hain |
| 6 | **Neon account** | ✅ haan | — | `neon.tech` (browser, koi download nahi, credit card nahi) |
| 7 | **FileZilla** | ✅ (pehle se hai) | — | — |
| 8 | **Notepad** | ✅ | — | connection string paste karne ke liye |

**Chhota jawab: kuch bhi install nahi karna. Bas VS me Pull maarna hai (package khud aayega) aur Neon par account banana hai.**

Agar database apni aankhon se dekhna hai (order aa raha hai ya nahi), to **DBeaver** le lein — ek hi
window me Neon ka Postgres khul jayega.

## B. Setup — poora order (12 step)

### Step 1 — Visual Studio me naya code laayein

1. VS kholen → **Git → Pull** (branch `arena/c683ad80-sabarna2-0`)
2. **`Ctrl+Shift+B`**
3. Restore ke waqt neeche "Restoring packages…" chalega, `Npgsql.EntityFrameworkCore.PostgreSQL` download hoga
4. Build **0 errors** hona chahiye

> Agar `NU1101: Unable to find package Npgsql.EntityFrameworkCore.PostgreSQL` aaye → internet nahi hai
> ya NuGet source off hai (Tools → Options → NuGet Package Manager → Package Sources). Us page par
> `https://api.nuget.org/v3/index.json` tick hona chahiye.

### Step 2 — Publish folder banaayein

1. `publish.cmd` double-click
2. "Publish succeeded" aaye
3. `D:\MVCDotNet\sabarna2.0\publish` kholein — ab list me **naye** files dikhne chahiye:
   `Npgsql.dll`, `Npgsql.EntityFrameworkCore.PostgreSQL.dll`
   *(ye do files na dikhein to publish purana hai — Step 1 dobara)*

### Step 3 — Ye build pehle SQLite par upload karein (Postgres abhi nahi)

1. FileZilla → left: `D:\MVCDotNet\sabarna2.0\publish`, right: `/`
2. `Ctrl+A` → **Ctrl+click se `App_Data` aur `web.config` deselect**
3. **Upload** → *Overwrite* → ✅ Always use this action
4. Panel → **Restart**
5. `Ctrl+Shift+R` → `/Shop` par 13 products dikhein

> Pehle SQLite par chala kar dekhein. Ek saath do badlav (naya build + naya database) karne se
> gadbad kahan hui ye pata nahi chalta.

### Step 4 — Neon account

1. Browser me `neon.tech` → **Sign up** (Google/GitHub se, credit card nahi lagta)
2. **Create project**
   - Name: `sabarna`
   - Region: **Singapore** (India se sabse kam latency) — na mile to `AWS ap-southeast-1`
   - Postgres version: jo default aaye, chhod dein
3. **Create** → 20–30 second me project ban jayega

### Step 5 — Connection string copy

1. Neon dashboard → apna project → **Dashboard** → **Connect** (ya *Connection string*)
2. **Pooled connection** ON rakhein
3. Aisa link copy hoga:
   ```
   postgresql://neondb_owner:AbCd123xyz@ep-cool-name-123456-pooler.ap-southeast-1.aws.neon.tech/neondb?sslmode=require
   ```
4. **Isse .NET format me badalna hai.** Notepad kholein aur ye pattern bhar dein:

   ```
   Host=<jo @ ke baad hai>;Port=5432;Database=neondb;Username=<jo // ke baad>;Password=<jo : ke baad>;SSL Mode=Require;Trust Server Certificate=true
   ```

   Upar wale example ka natija:
   ```
   Host=ep-cool-name-123456-pooler.ap-southeast-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=AbCd123xyz;SSL Mode=Require;Trust Server Certificate=true
   ```

> ⚠️ Password me `;` ya `=` ho to wo string tod dega — Neon password aisa nahi banata, par check kar lein.

### Step 6 — (Optional) Pehle PC se connection test

DBeaver install kiya ho to:
1. DBeaver → **New Connection → PostgreSQL**
2. Host / Port / Database / Username / Password alag-alag fields me daalein
3. **SSL** tab → *require*
4. **Test Connection** → green tick

Green tick matlab Neon theek hai. Ye step app se pehle problem pakad leta hai.

### Step 7 — Panel me do environment variables

MonsterASP panel → site `site105374` → **Environment Variables** (kuch panel me *App Settings*) → **Add**:

| Name | Value |
|---|---|
| `Database__Provider` | `Postgres` |
| `ConnectionStrings__Default` | Step 5 wala poora .NET string |

**Save** → phir **Restart**.

> ⚠️ Naam me **do underscore** hain: `Database__Provider`. Ek underscore se kaam nahi karega.

### Step 8 — Pehla start (yahi asli test hai)

1. Browser me `/Shop` kholein — pehla load **20–40 second** le sakta hai (tables ban rahe hain, 13 products seed ho rahe hain)
2. Panel → **Logs** kholein. Ye lines dikhni chahiye:
   ```
   Database provider: Postgres.
   SchemaPatcher: PostgreSQL — schema is created by EnsureCreated, payment indexes ensured.
   ```
3. `/Shop` refresh → **13 products**, sahi price (Biuli Bor-i ₹110, Amsi Achar 500 g ₹189, Kul Achar 250 g ₹99, Pure Honey 100 g ₹90)

Kuch na dikhe to **PART 1 ka section 6** (error table) dekhein.

### Step 9 — Admin ka naya password

Naya database = naya admin account. Pehli baar:

| Field | Value |
|---|---|
| URL | `/Admin/Login` |
| Email | `admin@sabarna.in` |
| Password | `Sabarna@2024` |

Login karte hi panel **naya password** maangega (purana likha hua hai isliye). **Abhi badal dein** —
8+ character, ek capital, ek number, ek symbol.

### Step 10 — Test order

1. `/Shop` → ek product → **Add to cart** → checkout
2. Payment method: **COD** (Razorpay abhi off hai)
3. Order place karein
4. `/Admin/Orders` → naya order dikhna chahiye

Ye dikh gaya matlab **read + write dono kaam kar rahe hain**.

### Step 11 — Backup test

1. `/Admin/Backups` → **Back up now**
2. List me `sabarna-YYYYMMDD-HHMMSS.json` aana chahiye
3. **Download** → Notepad me kholein → orders/products dikhne chahiye

### Step 12 — Purana data (agar chahiye)

Switch se pehle SQLite par **"Download all data (JSON)"** dabaya tha? Wo file abhi bhi aapke PC me hai.
Usme purane test orders hain. Unhe Postgres me daalna ho to mujhe bol dein — import script bana dunga.
Nahi chahiye to file rakh dein, koi dikkat nahi.

---

## C. Ek line me poora order

```
Git Pull → Ctrl+Shift+B → publish.cmd → FileZilla upload (App_Data + web.config chhodkar)
→ Restart → /Shop check (SQLite par) → Neon account → connection string .NET format me
→ panel: 2 env var → Restart → log me "Database provider: Postgres" → /Shop 13 products
→ admin login + naya password → test COD order → Backups → JSON download
```
