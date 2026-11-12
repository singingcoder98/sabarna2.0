# Nayi changes website par kaise daalein

**Ye file kisi ke liye bhi likhi gayi hai — computer ka expert hone ki zaroorat nahi.**
Har step me likha hai: *kya karna hai* aur *kya dikhna chahiye*.

**Website:** https://sabarna.runasp.net
**Host:** MonsterASP.NET (site ID `site105374`)
**Project folder (aapke PC par):** `D:\MVCDotNet\sabarna2.0`

---

## Sabse pehle: `publish.cmd` kahan hai?

```
D:\MVCDotNet\sabarna2.0\publish.cmd
```

Ye **project folder ke andar sabse upar** rakha hai — wahi folder jisme `Sabarna.sln`,
`Sabarna.Web.csproj`, `Program.cs`, `Views`, `wwwroot` jaise folder hain.

**Dhoondne ka aasaan tareeka:**

1. Keyboard par **Windows key + E** dabaiye (File Explorer khulega).
2. Upar address bar me type kariye: `D:\MVCDotNet\sabarna2.0` → **Enter**.
3. List me neeche scroll kariye — `publish.cmd` naam ka file dikhega (icon: black window with gears, ya
   blank white page). Uske aage likha hoga "Windows Batch File".

> **Visual Studio me nahi milega.** Visual Studio sirf code files dikhata hai; `.cmd` file File
> Explorer me hi dikhti hai. Isliye `D:\MVCDotNet\sabarna2.0` folder ko File Explorer me kholiye.

**Tip:** `publish.cmd` par right-click → **Send to → Desktop (create shortcut)**. Agli baar desktop se
seedha double-click.

---

## Ek baar ka setup (sirf pehli baar)

1. **FileZilla** install kariye — https://filezilla-project.org (Free version). Install karte waqt
   "decline" kar dijiye agar koi extra software offer kare.
2. **FTP password** nikaal ke rakhiye: MonsterASP panel → apni site `site105374` → **FTP / File
   Manager** section → FTP account details. (Username `site105374` hi rahega.)
3. FileZilla me ek baar site save kar lijiye (File → Site Manager → New site):

| Field | Value |
| --- | --- |
| Host | `site105374.siteasp.net` |
| Port | `21` |
| Protocol | FTP – File Transfer Protocol |
| Encryption | Require explicit FTP over TLS |
| Logon Type | Normal |
| User | `site105374` |
| Password | panel se (Save password tick kar dijiye) |

Agli baar se sirf: FileZilla kholiye → site select → **Connect**.

---

## Har baar ke 6 steps

### Step 1 — Build karo (Visual Studio)

1. `Sabarna.sln` kholiye (Visual Studio me).
2. Keyboard: **Ctrl + Shift + B**.
3. Neeche **Output** window me aana chahiye:

   ```
   Build succeeded.
       0 Error(s)
   ```

**Agar error aaye:** yahin ruk jaiye. Error ka screenshot bhej dijiye. Aage nahi badhna —
website par error 500 dikhega.

### Step 2 — `publish.cmd` chalao

1. File Explorer me `D:\MVCDotNet\sabarna2.0` kholiye.
2. `publish.cmd` par **double-click**.
3. Ek black window khulegi jo khud kaam karegi. Aakhir me aisa likha aayega:

   ```
   Done.  Folder: D:\MVCDotNet\sabarna2.0\publish
   ```

4. Koi bhi key dabaiye — window band ho jayegi.

**Isne kya kiya?** `publish` naam ka naya folder bana diya, jisme website ka taiyaar version hai —
yahi folder upload hota hai.

> Black window turant band ho jaaye aur "dotnet was not found" likhe, to .NET 8 SDK install nahi hai.
> Us case me screenshot bhejiye.

### Step 3 — Panel me website roko

1. Browser me MonsterASP panel kholiye → site `site105374`.
2. **Scripting** (ya "Application") section → **Stop application** dabaiye.

**Kyun?** Server abhi chal rahi website ki files ko "pakde" baitha hai; rokne se upload fail nahi hota.

### Step 4 — FileZilla se upload

1. FileZilla kholiye → apni site → **Connect**.
2. **Left side (Local site):** address bar me `D:\MVCDotNet\sabarna2.0\publish` type kariye → Enter.
3. **Right side (Remote site):** `/wwwroot` (ye website ka asli folder hai).
4. Left side par `Ctrl + A` (sab select) — **lekin `App_Data` folder ko Ctrl-click se deselect kar
   dijiye** (Step 5 me wajah likhi hai).
5. Selected files par right-click → **Upload**.
6. Agar poochhe "file already exists" → **Overwrite** (ya "Overwrite if different size") chuniye.

**Upload hone me 2–10 minute lag sakte hain** (free plan). Neeche transfer queue me progress dikhega.
Sab line jab khatam ho jaayein aur **Failed transfers = 0** ho, tab aage badhiye.

### Step 5 — `App_Data` kabhi upload nahi karna ⚠️

Server ka `App_Data/app.db` file hi **poori dukaan** hai — saare orders, products, settings.

Usse dhak lena (overwrite) = **saara data khatam**. Isliye:

- Upload karte waqt **`App_Data` folder deselect** rakhiye.
- Server par `App_Data` folder ko **delete kabhi nahi** karna.
- FileZilla ke "Delete files" ya "Remove additional files" jaise option **kabhi tick nahi** karne.

Naye database column (jaise address, phone, payment) **apne aap** pehli baar chalne par add ho jaate
hain — database ko chhedne ki zaroorat nahi.

### Step 6 — Website chalu karo aur check karo

1. Panel → **Start application**.
2. Browser me `https://sabarna.runasp.net` kholiye.
3. **Ctrl + Shift + R** dabaiye (purana cache hataane ke liye).

**Check list:**

| Kya dekhna hai | Kahan |
| --- | --- |
| Naam "SABARNA PURE" + ped wala logo | sabse upar header |
| Poora logo cream card me | page ke neeche footer |
| Naya favicon | browser tab |
| Jar add karne par page upar nahi jaata | home page |
| Admin pages khulte hain | `/Admin` |

---

## Kuch gadbad ho jaaye to

| Dikh raha hai | Matlab | Kya karna hai |
| --- | --- | --- |
| **500 – Internal Server Error** | Build local me fail hua tha, ya upload adhoora hai | Panel → **Logs** dekhiye; Step 1 dobara; Step 4 dobara |
| Upload me **"permission denied"** | Website band nahi ki (Step 3) | Panel se **Stop**, phir wo file dobara upload |
| Upload beech me **toot gaya** | Free plan ki limit | FileZilla reconnect → wahi files dobara queue → file count milaaiye |
| **Purana logo / purana naam** | Browser cache | **Ctrl + Shift + R**; ya incognito window |
| **Database khaali lag raha** (orders gayab) | Galti se `App_Data` upload ho gaya | Turant panel se site **Stop** kariye aur backup se `app.db` wapas rakhiye — dobara mat upload karna |
| Site slow / error | App pool atka | Panel → **Restart application** |

---

## Teen sunhare niyam

1. **`App_Data` ko chhuna nahi** — na upload, na delete.
2. **Pehle build, phir publish** — error ho to upload nahi karna.
3. **Har baar Ctrl + Shift + R** — tabhi naya dikhega.

---

## Screenshots ke liye jagah (baad me bhar lijiye)

- `[ ]` Step 1 — Build succeeded
- `[ ]` Step 3 — Stop application button
- `[ ]` Step 4 — FileZilla dono panel
- `[ ]` Step 6 — naya home page
