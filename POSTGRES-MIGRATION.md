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
