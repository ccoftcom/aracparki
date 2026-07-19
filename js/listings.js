(() => {
  "use strict";

  const {
    escapeHtml,
    escapeAttr,
    formatPrice,
    formatHours,
    formatTons,
    formatHp,
    badgeLabel,
    detailUrl,
  } = window.AP;

  const priceHtml = (listing) =>
    listing.priceUnit
      ? `${formatPrice(listing.price)}<small>/ ${escapeHtml(listing.priceUnit)}</small>`
      : formatPrice(listing.price);

  const listingCardHtml = (listing) => {
    const badge = badgeLabel(listing);
    const title = escapeHtml(listing.title);
    const href = escapeAttr(detailUrl(listing.id));
    const alt = escapeAttr(listing.title);
    return `
      <article class="listing-card" data-id="${escapeAttr(listing.id)}">
        <a class="listing-card-link" href="${href}">
          <div class="listing-media">
            <span class="${escapeAttr(badge.className)}">${escapeHtml(badge.text)}</span>
            <img src="${escapeAttr(listing.image)}" alt="${alt}" loading="lazy" width="900" height="620" />
          </div>
          <div class="listing-body">
            <h3>${title}</h3>
            <div class="listing-meta">
              <span>${escapeHtml(listing.year)}</span>
              <span>${escapeHtml(listing.city)} / ${escapeHtml(listing.district)}</span>
            </div>
            <div class="listing-specs">
              <span>${escapeHtml(formatHours(listing.hours))}</span>
              <span>${escapeHtml(formatTons(listing.tons))}</span>
              <span>${escapeHtml(formatHp(listing.hp))}</span>
            </div>
            <div class="listing-foot">
              <div class="listing-price">${priceHtml(listing)}</div>
              <span class="seller-tag${listing.verified ? " verified" : ""}">${listing.verified ? "Doğrulanmış · " : ""}${escapeHtml(listing.seller)}</span>
            </div>
          </div>
        </a>
      </article>`;
  };

  const classifiedRowHtml = (listing) => {
    const badge = badgeLabel(listing);
    const href = escapeAttr(detailUrl(listing.id));
    const title = escapeHtml(listing.title);
    const alt = escapeAttr(listing.title);
    return `
      <article class="classified-row">
        <a class="classified-row-link" href="${href}">
          <span class="classified-thumb">
            <img src="${escapeAttr(listing.image)}" alt="${alt}" loading="lazy" width="220" height="165" sizes="(max-width:720px) 40vw, 180px" />
          </span>
          <span class="classified-body">
            <span class="classified-title">${title}</span>
            <span class="classified-meta">
              <span class="${escapeAttr(badge.className)}">${escapeHtml(badge.text)}</span>
              <span>${escapeHtml(listing.year)}</span>
              <span>${escapeHtml(formatHours(listing.hours))}</span>
              <span>${escapeHtml(formatTons(listing.tons))}</span>
              <span>${escapeHtml(formatHp(listing.hp))}</span>
            </span>
            <span class="classified-loc">${escapeHtml(listing.city)} / ${escapeHtml(listing.district)}</span>
            <span class="classified-sub">${escapeHtml(listing.seller)}${listing.verified ? " · Doğrulanmış" : ""} · No: ${escapeHtml(listing.adNo)}</span>
          </span>
          <span class="classified-price-col">
            <span class="classified-price">${priceHtml(listing)}</span>
          </span>
        </a>
      </article>`;
  };

  window.AP.listingCardHtml = listingCardHtml;
  window.AP.classifiedRowHtml = classifiedRowHtml;
  window.AP.priceHtml = priceHtml;
})();
