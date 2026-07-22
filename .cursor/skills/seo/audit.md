# aracparki.com SEO — Codebase Audit

Update this file when SEO-related inventory changes.

## robots.txt / sitemap

| Path | Note |
|------|------|
| `wwwroot/robots.txt` | Disallows auth/account/wizard/`/api/`/`/admin`/`/kurumsal-hesap` |
| Dynamic sitemaps | `/sitemap.xml`, `/sitemap-static.xml`, `/sitemap-hubs.xml`, `/sitemap-dealers.xml`, `/sitemap-listings-{n}.xml` |
| SQL | `SitemapPublished.sql`, `CountPublished.sql`; migration `14_seo_slugs.sql` |

## Path hubs

| Pattern | Example |
|---------|---------|
| Intent | `/ilanlar/satilik` |
| + category | `/ilanlar/satilik/paletli-ekskavator` |
| + city | `/ilanlar/satilik/paletli-ekskavator/istanbul` |

- Razor: `@page "/ilanlar/{tip?}/{kategoriSlug?}/{sehirSlug?}"`
- Query allowlist URLs 301 → path hubs when indexable
- City-only stays query: `/ilanlar?tip=satilik&ilId=`
- Helpers: `ListingRoutes.HubUrl`, `ListingSeo.BuildCanonicalListPath(..., slugs)`

## Public dealers

| Item | Note |
|------|------|
| Route | `/satici/{slug}` |
| Schema | `corporate_accounts.slug` (+ `cities.slug`) via `14_seo_slugs.sql` |
| JSON-LD | `LocalBusiness` |
| Listing detail | Link when `CorporateSlug` present |

## Meta / structured data

Unchanged foundation + enriched Product JSON-LD; list titles include intent/brand/city.

## CWV

| Change | Note |
|--------|------|
| Layout CSS | Loaded by `PageKey` (home/list/detail/auth/wizard/account/legal) |
| Prod static cache | `public,max-age=31536000,immutable` for `/css`,`/js`,`/lib`,`/assets` |
| Image lazy + spinner | `base.css` `.img-shell` + `site.js` `initLazyImageSpinners`; LCP hero uses `fetchpriority=high` (no spinner) |

## Configure production

```json
"App": {
  "PublicBaseUrl": "https://www.aracparki.com",
  "Seo": {
    "GoogleSiteVerification": "<from Search Console>",
    "GoogleAnalyticsMeasurementId": "G-XXXXXXXX"
  }
}
```

Submit `https://www.aracparki.com/sitemap.xml` after deploy (migration `14_seo_slugs.sql` runs on startup).
