---
name: status-badges
description: >-
  Designs and implements professional status/workflow labels (Taslak, İncelemede,
  Yayında, Reddedildi, Arşiv, Onay bekliyor, etc.) for aracparki.com. Use when
  adding or restyling status badges, pills, chips, or semantic labels on
  İlanlarım, admin, kurumsal, or any account UI — or when choosing colors for
  pending/success/error/info/neutral states.
---

# Status & workflow badges

Turkish classifieds UI: status labels must be readable at a glance, semantic,
and consistent across İlanlarım / Admin / Kurumsal.

## Prefer semantic classes (not ad-hoc Tailwind)

Do **not** invent one-off colors per page (`bg-amber-100 text-amber-800`, random
hex). Use domain helpers + shared badge CSS:

```html
<span class="@ListingStatus.BadgeClass(item.Status)">@ListingStatus.Label(item.Status)</span>
<span class="@CorporateStatus.BadgeClass(account.Status)">@CorporateStatus.Label(account.Status)</span>
```

Sources:
- `Domain/Listings/ListingStatus.cs` → `Label` + `BadgeClass`
- `Domain/Corporate/CorporateStatus.cs` → same
- Styles: `wwwroot/css/tokens.css` (`--status-*`) + `wwwroot/css/components/badges.css`
- Tailwind theme aliases (optional): `ap-status-ok|warn|danger|info|muted` in `Styles/tw-ui.css`

Listing-card **intent** badges (`Satılık` / `Kiralık`) stay in `cards.css`
(`.badge`, `.badge-rent`) — absolute overlays. Do not mix those with status
workflow badges.

## Color map (required)

| Meaning | Class | Examples |
|---------|--------|----------|
| Success / live | `badge badge-ok` | Yayında, Onaylı |
| In progress / review | `badge badge-info` | İncelemede, Onay bekliyor |
| Attention / deadline | `badge badge-warn` | Süresi yaklaşıyor, uyarı |
| Error / blocked | `badge badge-danger` | Reddedildi |
| Neutral / idle | `badge badge-muted` | Taslak, Arşiv, unknown |

Never leave a bare `badge` for status — it inherits the yellow listing-card
style.

## Visual rules

- Soft tinted background + matching text + light border (see `badges.css`)
- Compact: ~11px, weight 700, no ALL CAPS for status
- Inline in meta rows; not oversized pills
- Expiry hints: `.account-expiry.is-soon` → warn, `.is-past` → danger
- CSS selectors must be `.badge.badge-ok` (etc.) — a lone `.badge` in
  `cards.css` used to override status colors when loaded later on account pages.
  Listing intent yellow lives under `.listing-media .badge` only.

## Adding a new status

1. Add constant + `Label` + `BadgeClass` on the domain type
2. Pick from the map above (do not invent a sixth hue)
3. Reuse existing CSS; only extend tokens if a new **semantic** role appears
4. Cover `BadgeClass` in unit tests when behavior is non-obvious

## Tailwind

If composing a one-off in `Styles/tw-ui.css`, map to tokens:

```css
@apply bg-ap-status-info/10 text-ap-status-info border border-ap-status-info/30;
```

Then rebuild: `npm run build:css` in `AracParki.Web`. Prefer the shared
`.badge-*` classes for Razor status labels so Admin/İlanlarım stay in sync.
