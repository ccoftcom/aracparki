# aracparki.com SEO — Phased Plan

Source: [Google SEO Starter Guide](https://developers.google.com/search/docs/fundamentals/seo-starter-guide) and linked Search Central docs (Essentials, How Search Works, Helpful Content, Sitemaps, Canonicalization, Title/Snippet, Image SEO, Product SD, Ecommerce, Page Experience, Maintaining SEO).

## Current gaps (summary)

| Area | Status |
|------|--------|
| Title / description / OG / Twitter | Done |
| Product + BreadcrumbList JSON-LD | Done (enriched) |
| robots.txt + HTTPS/HSTS/CSP | Done (`/admin`, `/kurumsal-hesap`) |
| Sitemap | Done — dynamic index + listings + hubs |
| List canonical | Done — allowlist + noindex policy |
| Hub URLs (category × city) | Query hubs in sitemap; **path hubs pending** |
| Search Console / Analytics | Config hooks ready — fill `App:Seo` |
| Public dealer pages | Pending |

---

## Implementation progress (2026-07-22)

Completed in code: Faz 0.3 robots, Faz 1 sitemap + canonical, Faz 3 list/detail titles, Faz 4 Product JSON-LD enrich, Faz 5 image srcset/alt, SeoSettings + CSP for GA4.

Remaining: Faz 0.1–0.2 manual SC/GA credentials, Faz 2 path hubs + dealer pages, Faz 7 CWV polish.

## Faz 0 — Eligibility (≈1 week)

[Search Essentials](https://developers.google.com/search/docs/essentials)

| # | Work | Outcome |
|---|------|---------|
| 0.1 | Verify Search Console (`www` + apex) | Verification meta/DNS |
| 0.2 | GA4 (+ optional GTM); update CSP allowlist | Measurement |
| 0.3 | `robots.txt`: Disallow `/admin`, `/kurumsal-hesap` | Defense-in-depth |
| 0.4 | URL Inspection on home, list, detail, legal, account | Google sees same HTML as users |
| 0.5 | Soft-404 / unpublished → real 404 + noindex | Index hygiene |

**Done when:** SC property green; critical URLs indexed or have clear crawl reason.

---

## Faz 1 — Discovery & indexing (2–3 weeks) — P0

### 1.1 Dynamic sitemap index

- `sitemap-index.xml` → `sitemap-static.xml`, `sitemap-listings-{n}.xml`, `sitemap-hubs.xml`
- Published `/ilan/{adNo}` + `lastmod` on publish/update
- Respect 50k URL / file limits
- Submit in Search Console

### 1.2 List canonical policy

[Canonicalization](https://developers.google.com/search/docs/crawling-indexing/canonicalization) — filters/sorts create duplicates.

| URL type | Policy |
|----------|--------|
| `/ilanlar` + allowlist: `tip`, `kategoriId`, `markaId`, `ilId` | `index,follow` + clean canonical |
| `sort`, `q`, thin multi-facet, `sayfa>1` | `noindex,follow` |
| Page 1 | Self-canonical **without** `?sayfa=1` |

### 1.3 Crawlable internal links

- Menu / footer / hubs via HTML `<a href>`
- HTMX menus need crawlable fallback links

**Done when:** Sitemap listing count ≈ published ads; fewer “Crawled – currently not indexed” for important URLs.

---

## Faz 2 — Site architecture & URLs (3–4 weeks) — P1

### 2.1 Recommended hub URLs

```
/ilanlar/satilik
/ilanlar/satilik/ekskavator
/ilanlar/satilik/ekskavator/istanbul
/ilanlar/kiralik/{kategori}/{sehir}
```

- Legacy `/ilanlar?...` → 301 (or canonical → hub)
- Unique H1, title, meta description per hub

### 2.2 Optional detail slug

- `/ilan/{adNo}/{slug}` + 301 from bare `/ilan/{adNo}`
- Preserve 301 chain when slug changes

### 2.3 Public dealer / gallery pages

- `/satici/{slug}` — unique copy + LocalBusiness/Organization JSON-LD
- Keep panel `/kurumsal-hesap` noindex; index public face only

**Done when:** Hubs can rank for “satılık {kategori} {şehir}” intent.

---

## Faz 3 — On-page & SERP appearance (ongoing; first sprint ≈2 weeks)

### Titles

[Title links](https://developers.google.com/search/docs/appearance/title-link)

| Page | Pattern |
|------|---------|
| Home | Brand + clear promise |
| Hub | `{Intent} {Kategori} {Şehir} \| AraçParkı` |
| Listing | `{Yıl} {Marka} {Model} {Tip} - {Şehir} \| #{adNo}` |
| Dealer | `{Firma} İş Makinesi İlanları \| AraçParkı` |

One clear visual H1 aligned with title.

### Meta descriptions

[Snippets](https://developers.google.com/search/docs/appearance/snippet)

- Listing: price + hours/km + city + one sentence
- Hub: listing count + scope + CTA
- Not keyword lists; programmatic + human-readable is OK

### Breadcrumbs

Keep HTML trail in sync with BreadcrumbList JSON-LD when hubs ship.

---

## Faz 4 — Structured data (2–3 weeks)

[Product structured data](https://developers.google.com/search/docs/appearance/structured-data/product)

Classifieds → **product snippets** (merchant listings only if direct purchase later).

| Work | Detail |
|------|--------|
| Enrich Product | brand, sku, image[], offers (price, currency, availability from status), seller |
| Vehicle attrs | year, hours, fuel, city via `additionalProperty` |
| ItemList | Optional on hubs |
| Validate | Rich Results Test on sample listings |
| Merchant Center | Optional later (P2) for free listings feed |

Never fake reviews or misleading prices.

---

## Faz 5 — Images (1–2 weeks)

[Image SEO](https://developers.google.com/search/docs/appearance/google-images)

1. Descriptive `alt` on primary listing images (title + type + city)
2. Cloudflare variant `srcset`/`sizes` (card/md/lg)
3. Image sitemap; verify CDN host in SC if needed
4. Consistent `og:image` / Product `image`; avoid extreme aspect ratios
5. Prefer meaningful filenames where practical

---

## Faz 6 — Content quality & E-E-A-T (ongoing)

[Helpful content](https://developers.google.com/search/docs/fundamentals/creating-helpful-content)

| Content | Purpose |
|---------|---------|
| Category guides (human-written) | Support thin hubs |
| About / safe shopping / contact | Trust |
| Dealer profile copy | Experience signal |
| Stale listing cleanup | Freshness + UX |
| AI content | Disclose how/why if expected; no ranking manipulation |

**Do not prioritize:** meta keywords, keyword stuffing, magic word counts, treating E-E-A-T as one ranking factor, TLD keyword obsession.

---

## Faz 7 — Page experience / CWV (≈2 weeks, parallel)

[Page experience](https://developers.google.com/search/docs/appearance/page-experience)

1. Page-scoped CSS in layout (avoid loading all page CSS everywhere)
2. Prod static: long Cache-Control + fingerprint
3. Font self-host or optimize
4. LCP: cover WebP + width/height
5. No intrusive interstitials
6. Baseline via PSI + SC CWV, then improve

---

## Faz 8 — Promotion (ongoing)

Discovery often needs external links + community:

- Industry forums, associations, local B2B
- Social / WhatsApp / newsletter (over-promotion can look spammy)
- Quality outbound links; UGC links get `nofollow`/`ugc` if comments exist

---

## Faz 9 — Monitor & iterate (permanent)

| Cadence | Tool | Action |
|---------|------|--------|
| Weekly | SC Performance | Query × page CTR; refine titles/descriptions |
| Weekly | Indexing / Sitemap | Fix coverage errors |
| Monthly | Traffic-drop debug | Algo vs technical |
| Post-change | Wait 2–4 weeks | Starter Guide: delayed impact |

---

## Dependency flow

```text
Faz0 (SC + robots + analytics)
  └─► Faz1 (sitemap + canonical) ──► Faz2 (hub URLs)
         │                              │
         ├─► Faz4 (JSON-LD)             ├─► Faz3 (titles/desc)
         │                              └─► Faz8 (promo)
         └─► Faz5 (images)
Faz0 ──► Faz7 (CWV)
Faz3 + Faz4 + Faz7 ──► Faz9 (SC loop)
Faz2 + Faz6 (content) in parallel after hubs
```

---

## Code backlog

1. Dynamic sitemap endpoint / background job
2. `CanonicalIncludeQuery` → allowlist + noindex rules (`Ilanlar/Index.cshtml.cs`)
3. Hub routes + 301 map
4. Enrich `BuildProductJsonLd` (`Ilan/Index.cshtml.cs`)
5. Public dealer page
6. Image `srcset` + alt audit
7. Layout CSS splitting + cache headers
8. SC verification + GA4

---

## Out of scope / low priority

- Meta keywords
- PageRank fixation
- Ideal word-count targets
- Heading order as SEO ranking lever (still good for a11y)
- Fear of duplicate-content “penalty” (real risk: thin pages + crawl waste)
- AMP

---

## Rough timeline

| Weeks | Focus |
|-------|--------|
| 1 | Faz 0 + sitemap skeleton + canonical hotfix |
| 2–3 | Full sitemap + SC baseline |
| 4–7 | Hub URLs + title/desc templates |
| 5–8 | JSON-LD + images (parallel) |
| 8–10 | CWV + dealer pages |
| 10+ | Content + promo + monthly SC review |
