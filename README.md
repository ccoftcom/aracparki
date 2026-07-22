# Araç Parkı (.NET)

İş makinesi ilan pazarı — ASP.NET Core Razor Pages, Dapper, PostgreSQL, HTMX, Alpine.js (CSP), FluentValidation, Serilog, MailKit (Brevo SMTP).

## Mimari

```
src/
  AracParki.Web/               Presentation (Razor + HTMX + Alpine CSP)
  AracParki.Application/       Services, DTOs, validators
  AracParki.Domain/            Constants / entities
  AracParki.Infrastructure/    Dapper, SQL files, Postgres, SMTP
database/                      Schema + seed (SQL) — applied on app startup
```

## Sıfırdan çalıştırma

Repo kökünden (`aracparki.com/`):

```bash
# Postgres + Redis volume’u silip temiz başlat
docker compose down -v
docker compose up -d

# Uygulama açılışında database/*.sql otomatik uygulanır (ilk seferde neighborhoods ~1–2 dk)
./watch.sh

# veya:
# dotnet run --project src/AracParki.Web
```

`./watch.sh` 7133/5245 portlarını boşaltır, Tailwind CSS (`npm run watch:css`) ve `dotnet watch` (HTTPS) başlatır. Çıkışta CSS watch da durur.

Yapılandırma: `src/AracParki.Web/appsettings.json` (+ `appsettings.Development.json`).

- Uygulama: https://localhost:7133 (HTTP: http://localhost:5245)  
- Health: https://localhost:7133/health (`postgres` + `redis`)  
- Redis: `localhost:6380` → container `6379` (docker compose `redis:7-alpine`, AOF + `allkeys-lru`)

Session, catalog/listing cache, auth stamp revalidation ve WhatsApp OTP rate-limit Redis üzerinden çalışır (`ConnectionStrings:Redis`, key prefix `aracparki:`).

Şema/seed dosyaları `database/01`…`07` — uygulama her başlangıçta `schema_migrations` tablosuna bakıp yeni veya içeriği değişmiş script’leri uygular. `Database:MigrateOnStartup` ile kapatılabilir.

## Database (uygulama sırası)

| File | Purpose |
|------|---------|
| `01_schema.sql` | Tablolar, index’ler, trigger’lar (final şema) |
| `02_cities.sql` | 81 cities |
| `03_districts.sql` | 973 districts |
| `04_neighborhoods.sql` | ~73.5k neighborhoods |
| `05_equipment_catalog.sql` | Groups, brands, models, attributes, attachments, OEM specs |
| `06_demo.sql` | Sellers + demo listings |
| `07_accounts.sql` | Accounts, OTP/drafts, tokens, seller link, saved_searches |
| `08_media.sql` | listing_images enrichment, watermark_templates, media_upload_sessions |

OEM model metriklerini (HP, kapasite) yeniden üretmek için: `python3 scripts/generate_model_specs.py` (`05` içindeki `OEM_MODEL_SPECS` bloğunu günceller).

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

## Medya (Cloudflare R2 + Images)

İlan görselleri production’da **local filesystem’a yazılmaz**. Akış:

1. Wizard `OnPostUpload` → `IListingImageStorage`
2. `CloudflareMedia` tam yapılandırılmışsa (`Enabled` + `WorkerBaseUrl` + `IngestSecret`) byte’lar Worker `/v1/ingest`’e gider
3. Worker: MIME/boyut/çözünürlük doğrular, EXIF temizler, orientation düzeltir, master’ı R2’ye yazar (**watermark yok**)
4. Delivery: `GET /m/{storageKey}?v=card|lg|…` — `thumb` temiz; `card`/`md`/`lg`/`xl`/`og` ortada büyük, düşük opacity logo watermark

Worker projesi: `workers/media/` (kurulum için oradaki README).

`Enabled=false` **veya** `IngestSecret` boşsa → `LocalListingImageStorage` (`wwwroot/uploads/listings/`). Harici URL ile görsel ekleme yok; yalnızca yüklenen dosyalar (`/uploads/listings/…` veya medya Worker `/m/…`) kabul edilir. Şema: `database/08_media.sql`.

### İlan Ver UX (taslak + fotoğraf)

- Hesap başına **tek** taslak (`listing_wizard_drafts`); her adımda session + DB dual-write.
- `/ilan-ver` açılışında anlamlı taslak varsa **modal**: “Taslağa devam et” / “Yeni ilan” (telefon doğrulamasından sonra).
- Foto adımı: **çoklu seçim**, XHR progress kuyruğu, galeride kapak yap / kaldır.
- Shortcut’lar: `?devam=1`, `?yeni=1`.

### Yapılandırma alanları

| Alan | Açıklama |
|------|----------|
| `CloudflareMedia:Enabled` | Cloudflare pipeline açık/kapalı |
| `CloudflareMedia:WorkerBaseUrl` | Worker origin (ingest + API çağrıları) |
| `CloudflareMedia:PublicBaseUrl` | Görsel delivery origin (genelde Worker ile aynı) |
| `CloudflareMedia:IngestSecret` | Worker `INGEST_SECRET` ile **birebir aynı** Bearer token — **repoya koyma** |
| `CloudflareMedia:DefaultWatermarkCode` | Watermark şablon kodu (varsayılan `default`) |

Mevcut Worker (örnek): `https://aracparki-media.dry-meadow-d8d8.workers.dev`

### Development (lokal)

1. Worker deploy edilmiş ve R2 bucket hazır olmalı (`workers/media` README).
2. `appsettings.Development.json` içinde URL’ler ve `Enabled: true` tanımlı; **secret dosyada boş** bırakılır.
3. Secret’ı User Secrets’a yaz (Worker’daki `INGEST_SECRET` ile aynı değer):

```bash
cd workers/media
npx wrangler secret put INGEST_SECRET
# istediğin değeri gir (ör. güçlü bir parola)

cd ../../src/AracParki.Web
dotnet user-secrets set "CloudflareMedia:IngestSecret" "<aynı-değer>"
```

4. Uygulamayı Development ile başlat (`./watch.sh` veya `dotnet run`). User Secrets otomatik yüklenir.
5. Kontrol: ilan ver → foto yükle → URL `…workers.dev/m/masters/…` olmalı; `wwwroot/uploads` dolmamalı.

User Secrets listesi (değerleri maskelemeden dikkatli kullan):

```bash
cd src/AracParki.Web
dotnet user-secrets list
```

### Production

`appsettings.json` içinde `Enabled` + Worker URL’leri tutulabilir; **`IngestSecret` asla commit edilmez**. Hosting ortamında environment variable / secret store ile ver:

```bash
# Çift alt çizgi = ASP.NET Core config hierarchy
CloudflareMedia__Enabled=true
CloudflareMedia__WorkerBaseUrl=https://aracparki-media.dry-meadow-d8d8.workers.dev
CloudflareMedia__PublicBaseUrl=https://aracparki-media.dry-meadow-d8d8.workers.dev
CloudflareMedia__IngestSecret=<wrangler-daki-INGEST_SECRET-ile-aynı>
```

Docker / Compose örneği:

```yaml
environment:
  CloudflareMedia__Enabled: "true"
  CloudflareMedia__WorkerBaseUrl: "https://aracparki-media.dry-meadow-d8d8.workers.dev"
  CloudflareMedia__PublicBaseUrl: "https://aracparki-media.dry-meadow-d8d8.workers.dev"
  CloudflareMedia__IngestSecret: "${CLOUDFLARE_MEDIA_INGEST_SECRET}"
```

Prod checklist:

- [ ] Worker canlı (`GET …/health` → `{"ok":true}`)
- [ ] `INGEST_SECRET` Worker’da set (`wrangler secret put INGEST_SECRET`)
- [ ] Aynı secret host env’de (`CloudflareMedia__IngestSecret`)
- [ ] `WorkerBaseUrl` / `PublicBaseUrl` production Worker origin
- [ ] (İleride) custom domain: `media.aracparki.com` → Worker route + `PUBLIC_BASE_URL` güncelle

### Ortak notlar

- Dev ve prod şu an **aynı** `workers.dev` Worker + R2 bucket kullanabilir; ileride staging için ayrı bucket/Worker önerilir.
- Secret değişince hem Worker’ı hem (dev) User Secrets / (prod) env’i güncelle.
- Watermark PNG: `npx wrangler r2 object put aracparki-media/assets/watermarks/default.png --file=../../src/AracParki.Web/wwwroot/assets/logo/logo.png --remote`

### Image API (auth)

| Endpoint | Açıklama |
|----------|----------|
| `GET /api/listings/{adNo}/images` | Görseller + variants |
| `GET /api/listings/{adNo}/images/{id}/variants` | Variant URL’leri |
| `PATCH /api/listings/{adNo}/images/reorder` | `{ "imageIds": [..] }` |
| `PATCH /api/listings/{adNo}/images/{id}/cover` | Kapak |
| `DELETE /api/listings/{adNo}/images/{id}` | Soft delete (7 gün grace) |

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
