(() => {
  "use strict";

  const {
    ILLER,
    CATEGORIES,
    LISTINGS,
    escapeHtml,
    formatPrice,
    formatHours,
    formatTons,
    formatHp,
    badgeLabel,
    intentLabel,
    matchesListing,
    readUrlState,
    buildListUrl,
    detailUrl,
    sortListings,
  } = window.AP;

  let state = readUrlState();

  const fillSelects = () => {
    const cat = document.getElementById("filter-category");
    const city = document.getElementById("filter-city");
    const tip = document.getElementById("filter-tip");
    const sort = document.getElementById("sort-select");
    const q = document.getElementById("search-query");

    CATEGORIES.forEach((c) => {
      const opt = document.createElement("option");
      opt.value = c.name;
      opt.textContent = c.name;
      cat.appendChild(opt);
    });
    ILLER.forEach((name) => {
      const opt = document.createElement("option");
      opt.value = name;
      opt.textContent = name;
      city.appendChild(opt);
    });

    tip.value = state.filter;
    cat.value = state.category;
    city.value = state.city;
    sort.value = state.sort || "yeni";
    if (q) q.value = state.query;
  };

  const writeUrl = () => {
    const full = buildListUrl(state);
    const pathQuery = full.startsWith("ilanlar.html") ? full.slice("ilanlar.html".length) : full;
    history.replaceState(null, "", pathQuery || "ilanlar.html");
  };

  const renderBreadcrumb = () => {
    const el = document.getElementById("breadcrumb");
    const parts = [`<a href="index.html">Anasayfa</a>`, `<a href="ilanlar.html">İş Makineleri</a>`];
    if (state.category) parts.push(`<span>${escapeHtml(state.category)}</span>`);
    else if (state.filter !== "all") parts.push(`<span>${escapeHtml(intentLabel(state.filter))}</span>`);
    el.innerHTML = parts.join(" <span class='bc-sep'>/</span> ");
  };

  const renderList = () => {
    const root = document.getElementById("list-results");
    const title = document.getElementById("list-title");
    let items = LISTINGS.filter((item) => matchesListing(item, state));
    items = sortListings(items, state.sort || "yeni");

    if (state.category) {
      title.innerHTML = `${escapeHtml(state.category)} İlanları <span id="result-count">(${items.length} ilan)</span>`;
    } else {
      title.innerHTML = `İş Makinesi İlanları <span id="result-count">(${items.length} ilan)</span>`;
    }

    document.title = `${state.category || "İş Makinesi"} İlanları | Araç Parkı`;

    if (!items.length) {
      root.innerHTML = `
        <div class="listing-empty empty-state">
          <p>Bu kriterlere uygun ilan bulunamadı.</p>
          <button type="button" class="btn btn-machine" id="empty-clear">Filtreleri temizle</button>
          <a class="btn btn-ghost" href="index.html">Anasayfaya dön</a>
        </div>`;
      document.getElementById("empty-clear")?.addEventListener("click", () => {
        document.getElementById("clear-filters")?.click();
      });
      return;
    }

    root.innerHTML = items
      .map((listing) => {
        const badge = badgeLabel(listing);
        const href = detailUrl(listing.id);
        const price = listing.priceUnit
          ? `${formatPrice(listing.price)} <small>/ ${escapeHtml(listing.priceUnit)}</small>`
          : formatPrice(listing.price);
        return `
        <article class="classified-row">
          <a class="classified-hit" href="${href}" aria-label="${escapeHtml(listing.title)} ilanını aç"></a>
          <a class="classified-thumb" href="${href}" tabindex="-1">
            <img src="${escapeHtml(listing.image)}" alt="" loading="lazy" width="220" height="165" sizes="(max-width:720px) 40vw, 180px" />
          </a>
          <div class="classified-body">
            <a class="classified-title" href="${href}">${escapeHtml(listing.title)}</a>
            <div class="classified-meta">
              <span class="${escapeHtml(badge.className)}">${escapeHtml(badge.text)}</span>
              <span>${listing.year}</span>
              <span>${formatHours(listing.hours)}</span>
              <span>${formatTons(listing.tons)}</span>
              <span>${formatHp(listing.hp)}</span>
            </div>
            <div class="classified-loc">${escapeHtml(listing.city)} / ${escapeHtml(listing.district)}</div>
            <div class="classified-sub">${escapeHtml(listing.seller)}${listing.verified ? " · Doğrulanmış" : ""} · No: ${escapeHtml(listing.adNo)}</div>
          </div>
          <div class="classified-price-col">
            <div class="classified-price">${price}</div>
            <a class="btn btn-machine btn-sm" href="${href}">İncele</a>
          </div>
        </article>`;
      })
      .join("");
  };

  const apply = () => {
    writeUrl();
    renderBreadcrumb();
    renderList();
  };

  fillSelects();
  renderBreadcrumb();
  renderList();

  document.getElementById("list-filter-form").addEventListener("submit", (e) => {
    e.preventDefault();
    state.filter = document.getElementById("filter-tip").value || "all";
    state.category = document.getElementById("filter-category").value || "";
    state.city = document.getElementById("filter-city").value || "";
    apply();
  });

  document.getElementById("clear-filters").addEventListener("click", () => {
    state = { filter: "all", category: "", city: "", query: "", sort: "yeni" };
    document.getElementById("filter-tip").value = "all";
    document.getElementById("filter-category").value = "";
    document.getElementById("filter-city").value = "";
    document.getElementById("search-query").value = "";
    document.getElementById("sort-select").value = "yeni";
    apply();
  });

  document.getElementById("search-form").addEventListener("submit", (e) => {
    e.preventDefault();
    state.query = document.getElementById("search-query").value || "";
    apply();
  });

  document.getElementById("sort-select").addEventListener("change", (e) => {
    state.sort = e.target.value;
    apply();
  });
})();
