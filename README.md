# Araç Parkı (.NET)

İş makinesi ilan pazarı — ASP.NET Core Razor Pages, Dapper, PostgreSQL, HTMX, Alpine.js (CSP), FluentValidation, Serilog, MailKit (Brevo SMTP).

## Mimari

```
src/
  AracParki.Web/               Presentation (Razor + HTMX + Alpine CSP)
  AracParki.Application/       Services, DTOs, validators
  AracParki.Domain/            Constants / entities
  AracParki.Infrastructure/    Dapper, SQL files, Postgres, SMTP
database/                      Schema + seed (SQL) — docker init only
```

## Sıfırdan çalıştırma

Repo kökünden (`aracparki.com/`):

```bash
# Postgres verisini sil + şemayı/seed’i baştan yükle (ilk açılış ~1–2 dk)
docker compose down -v
docker compose up -d

# Hazır olunca (kökten):
./watch.sh

# veya:
# dotnet run --project src/AracParki.Web
```

`./watch.sh` 7133/5245 portlarını boşaltır, sonra `dotnet watch` (HTTPS) başlatır.

Yapılandırma: `src/AracParki.Web/appsettings.json` (+ `appsettings.Development.json`).

- Uygulama: https://localhost:7133 (HTTP: http://localhost:5245)  
- Health: https://localhost:7133/health  

`docker compose down -v` volume’u siler; bir sonraki `up` `database/01`…`07` dosyalarını boş volume’a uygular. Ayrı migration klasörü yoktur — şema dosyaları kanonik kaynaktır.

## Database (init sırası)

| File | Purpose |
|------|---------|
| `01_schema.sql` | Tablolar, index’ler, trigger’lar |
| `02_cities.sql` | 81 cities |
| `03_districts.sql` | 973 districts |
| `04_neighborhoods.sql` | ~73.5k neighborhoods |
| `05_equipment_catalog.sql` | Groups, brands, models, attributes |
| `06_demo.sql` | Sellers + demo listings |
| `07_accounts.sql` | Accounts, tokens, seller link, saved_searches |

## Auth & e-posta

| Route | Purpose |
|-------|---------|
| `/giris` | Login (requires confirmed email) |
| `/kayit` | Register → sends Brevo verification email |
| `/eposta-dogrula` | Confirm email / resend verification |
| `/sifremi-unuttum` | Forgot password → reset email |
| `/sifre-sifirla` | Reset with token (invalidates sessions) |
| `/cikis` | Logout (POST) |

`EmailSettings` + `App:PublicBaseUrl` in appsettings drive SMTP and link generation. Tokens are stored hashed; raw tokens only appear in the email. Re-issue invalidates prior unused tokens; consume is atomic. Cookie tickets carry a `security_stamp` that rotates on password reset.

Password rules: min 8 chars, ≥1 letter, ≥1 digit, no triple repeat, must not contain name/email local-part.

Icons: **Lucide** (`lucide-static@1.25.0`) via `<ap-icon name="…" />`.

## API

| Endpoint | Description |
|----------|-------------|
| `GET /api/catalog/categories` | Categories |
| `GET /api/catalog/category-groups` | Groups + children |
| `GET /api/catalog/brands` | All brands |
| `GET /api/catalog/categories/{id}/brands` | Brands by category |
| `GET /api/catalog/categories/{id}/brands/{brandId}/models` | Models |
| `GET /api/catalog/categories/{id}/attributes` | Spec schema |
| `GET /api/catalog/facets/brands` | Brand facet counts |
| `GET /api/locations/...` | City → district → neighborhood → street |

## Test

```bash
dotnet test src/AracParki.slnx
```
