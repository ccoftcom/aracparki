---
name: seo
description: >-
  Implements and reviews SEO for aracparki.com (iş makinesi classifieds) using
  Google Search Central guidance and the project phased plan. Use when working
  on SEO, sitemap, robots.txt, canonical, meta title/description, Open Graph,
  structured data/JSON-LD, breadcrumbs, hub URLs, image alt/srcset, Core Web
  Vitals, Search Console, or organic search visibility.
---

# aracparki.com SEO

Google Search Central (SEO Starter Guide + linked docs) aligned plan for this
marketplace. Prefer people-first content and crawl/index hygiene over tricks.

## When to use

- Implementing or reviewing any SEO-related change
- Adding/changing routes that affect indexing (list, detail, hubs, dealers)
- Meta tags, canonical, robots, sitemap, JSON-LD, breadcrumbs, images, CWV

## Sources of truth

1. This skill + [plan.md](plan.md) (phases, priorities, backlog)
2. [audit.md](audit.md) (current codebase state — update when SEO code changes)
3. Official docs: https://developers.google.com/search/docs/fundamentals/seo-starter-guide

## Non-negotiables

- Do **not** use meta keywords, keyword stuffing, fake reviews, or cloaking
- Duplicate/filter URLs are normal; fix with **canonical + noindex**, not panic
- Google does **not** guarantee crawl/index/rank; measure via Search Console
- Effects can take days–weeks; wait before judging ranking changes
- Ignore: magic word counts, PageRank obsession, TLD keyword myths, E-E-A-T as a single ranking factor

## Priority order (do in this sequence)

| Priority | Focus | Details |
|----------|--------|---------|
| **P0** | Crawl & index | Search Console, robots, **dynamic sitemap**, list **canonical/noindex** policy |
| **P1** | Architecture & SERP | Hub URLs, titles/descriptions, Product/Vehicle JSON-LD |
| **P2** | Media & UX | Image alt/srcset, CWV/CSS splitting, public dealer pages |
| **Ongoing** | Content & promo | E-E-A-T pages, promotion without spam, SC iteration |

Full phases, URL policies, and code backlog: [plan.md](plan.md).

## Implementation checklist

Copy and track:

```
SEO Task:
- [ ] Read plan.md section for this phase
- [ ] Check audit.md for existing files/patterns
- [ ] Match existing code style (Razor Pages, SiteUrls, Breadcrumbs)
- [ ] Keep private surfaces noindex (account, admin, ilan-ver, unpublished)
- [ ] Verify absolute canonicals via AppSettings.PublicBaseUrl
- [ ] Update audit.md if inventory changed
```

## Code touchpoints (quick map)

| Concern | Likely location |
|---------|-----------------|
| Layout meta/OG/JSON-LD shell | `src/AracParki.Web/Pages/Shared/_Layout.cshtml` |
| Absolute URLs | `src/AracParki.Web/Infrastructure/SiteUrls.cs` |
| List routes/filters | `src/AracParki.Web/Infrastructure/ListingRoutes.cs` |
| List page SEO/canonical | `src/AracParki.Web/Pages/Ilanlar/Index.cshtml.cs` |
| Detail Product JSON-LD | `src/AracParki.Web/Pages/Ilan/Index.cshtml.cs` |
| Breadcrumbs + schema | `src/AracParki.Web/Infrastructure/Breadcrumbs.cs` |
| robots / static sitemap | `src/AracParki.Web/wwwroot/robots.txt`, `sitemap.xml` |
| Security/CSP (analytics) | `Program.cs`, security headers extensions |

## List URL indexing policy (critical)

Allowlist for `index,follow` + clean canonical: `tip`, `kategoriId`, `markaId`, `ilId` only.

`noindex,follow` for: `sort`, `q`, thin multi-facet combos, `sayfa > 1`.

Page 1 canonical must **omit** `sayfa`. Detail pages: path-only canonical.

## Title / description templates

| Page | Title pattern |
|------|----------------|
| Home | Brand + clear value prop (no boilerplate spam) |
| Hub | `{Intent} {Kategori} {Şehir} \| AraçParkı` |
| Listing | `{Yıl} {Marka} {Model} {Tip} - {Şehir} \| #{adNo}` |
| Dealer | `{Firma} İş Makinesi İlanları \| AraçParkı` |

Meta descriptions: unique, human-readable, programmatic OK (price/hours/city for listings). Not keyword lists.

## Structured data

- Keep `WebSite` + `Organization` + `BreadcrumbList`
- Enrich listing `Product`/`Offer` (availability from status, seller, images, brand/sku)
- Prefer vehicle attributes via `additionalProperty` (year, hours, location)
- Validate with Rich Results Test before claiming rich-result eligibility
- Never fabricate ratings/reviews

## Before shipping SEO changes

1. Confirm noindex on auth/account/admin/wizard/unpublished
2. Confirm important CSS/JS not blocked from Googlebot
3. Prefer crawlable `<a href>` for hubs (not JS-only discovery)
4. After deploy: Search Console URL Inspection + sitemap submit when relevant

## Additional resources

- Phased plan & backlog: [plan.md](plan.md)
- Codebase SEO inventory: [audit.md](audit.md)
