# aracparki.com SEO — Codebase Audit

Update this file when SEO-related inventory changes.

## robots.txt / sitemap

| Path | Note |
|------|------|
| `src/AracParki.Web/wwwroot/robots.txt` | Disallows auth/account/wizard/`/api/`/`/health`/`/admin`/`/kurumsal-hesap`; sitemap → `/sitemap.xml` |
| Dynamic sitemaps | `SitemapEndpoints.cs`: `/sitemap.xml` (index), `/sitemap-static.xml`, `/sitemap-hubs.xml`, `/sitemap-listings-{n}.xml` |
| SQL | `Listings/SitemapPublished.sql`, `Listings/CountPublished.sql` |

Static `wwwroot/sitemap.xml` removed (served dynamically).

## Meta tags

| Path | Note |
|------|------|
| `_Layout.cshtml` | title, description, robots, canonical, OG/Twitter; optional `google-site-verification`; optional GA4 |
| `SeoSettings` (`App:Seo`) | `GoogleSiteVerification`, `GoogleAnalyticsMeasurementId` |
| List SEO | `ListingSeo.cs` — allowlist canonical + noindex for thin/sort/q/page |
| Detail SEO | `ListingSeo.BuildDetailMeta` + Product JSON-LD |

## Structured data (JSON-LD)

| Type | Where |
|------|--------|
| `WebSite` + `SearchAction` | `_Layout.cshtml` |
| `Organization` | `_Layout.cshtml` |
| `Product` + `Offer` + seller + `additionalProperty` | `Pages/Ilan/Index.cshtml.cs` |
| `BreadcrumbList` | `Infrastructure/Breadcrumbs.cs` |

## List indexing policy

**Indexable allowlist:** `tip`, `kategoriId`, `markaId`, single `ilId` (page 1, default sort, no `q`).

**noindex,follow:** sort, search, `sayfa>1`, multi-city, price/year/hours facets, name-only category/city, etc.

Canonical always rebuilt from allowlist (not raw query string).

## URL structure

| Surface | Pattern |
|---------|---------|
| Listing detail | `/ilan/{adNo}` |
| List / hubs | `/ilanlar?...` (query hubs in sitemap; path hubs not yet) |
| Corporate panel | `/kurumsal-hesap` — robots Disallow + noindex |
| Public dealer pages | Not implemented yet |

## Images

| Area | Status |
|------|--------|
| Cards / rows / table | Cloudflare `srcset` via `ListingImageUrlVariants` when `/m/` URL |
| Detail gallery | lg/md/xl srcset + descriptive thumb alts |
| Helper | `Application/Listings/ListingImageUrlVariants.cs` |

## Still open (next phases)

1. Path-based hub URLs (`/ilanlar/satilik/ekskavator/istanbul`) + 301 from query
2. Optional detail slug `/ilan/{adNo}/{slug}`
3. Public `/satici/{slug}` dealer pages
4. Fill `App:Seo` values in production + Search Console property verify
5. CWV: page-scoped CSS, prod Cache-Control, font optimization
6. Image sitemap extension (optional)

## Configure production SEO

```json
"App": {
  "PublicBaseUrl": "https://www.aracparki.com",
  "Seo": {
    "GoogleSiteVerification": "<from Search Console>",
    "GoogleAnalyticsMeasurementId": "G-XXXXXXXX"
  }
}
```

Then submit `https://www.aracparki.com/sitemap.xml` in Search Console.
