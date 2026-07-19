(() => {
  "use strict";

  const {
    escapeHtml,
    fillCategorySelect,
    fillCitySelect,
    intentLabel,
    matchesListing,
    readUrlState,
    buildListUrl,
    sortListings,
    LISTINGS,
    classifiedRowHtml,
  } = window.AP;

  let state = readUrlState();

  const fillSelects = () => {
    const cat = document.getElementById("filter-category");
    const city = document.getElementById("filter-city");
    const tip = document.getElementById("filter-tip");
    const sort = document.getElementById("sort-select");
    const q = document.getElementById("search-query");

    if (cat) {
      cat.innerHTML = "";
      fillCategorySelect(cat);
    }
    fillCitySelect(city);

    if (tip) tip.value = state.filter || "all";
    if (cat) cat.value = state.category;
    if (city) city.value = state.city;
    if (sort) sort.value = state.sort || "yeni";
    if (q) q.value = state.query;
  };

  const writeUrl = () => {
    const full = buildListUrl(state);
    const pathQuery = full.startsWith("ilanlar.html") ? full.slice("ilanlar.html".length) : full;
    history.replaceState(null, "", pathQuery || "ilanlar.html");
  };

  const renderBreadcrumb = () => {
    const el = document.getElementById("breadcrumb");
    if (!el) return;
    const parts = [`<a href="index.html">Anasayfa</a>`, `<a href="ilanlar.html">İş Makineleri</a>`];
    if (state.category) parts.push(`<span>${escapeHtml(state.category)}</span>`);
    else if (state.filter !== "all") parts.push(`<span>${escapeHtml(intentLabel(state.filter))}</span>`);
    el.innerHTML = parts.join(" <span class='bc-sep'>/</span> ");
  };

  const renderList = () => {
    const root = document.getElementById("list-results");
    const title = document.getElementById("list-title");
    if (!root || !title) return;

    let items = LISTINGS.filter((item) => matchesListing(item, state));
    items = sortListings(items, state.sort || "yeni");

    const heading = state.category
      ? `${escapeHtml(state.category)} İlanları`
      : "İş Makinesi İlanları";
    title.innerHTML = `${heading} <span id="result-count">(${items.length} ilan)</span>`;

    document.title = `${state.category || "İş Makinesi"} İlanları | Araç Parkı`;

    if (!items.length) {
      root.setAttribute("aria-busy", "false");
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

    root.setAttribute("aria-busy", "false");
    root.innerHTML = items.map((listing) => classifiedRowHtml(listing)).join("");
  };

  const apply = () => {
    writeUrl();
    renderBreadcrumb();
    renderList();
  };

  fillSelects();
  renderBreadcrumb();
  renderList();

  document.getElementById("list-filter-form")?.addEventListener("submit", (e) => {
    e.preventDefault();
    state.filter = document.getElementById("filter-tip")?.value || "all";
    state.category = document.getElementById("filter-category")?.value || "";
    state.city = document.getElementById("filter-city")?.value || "";
    apply();
  });

  document.getElementById("clear-filters")?.addEventListener("click", () => {
    state = { filter: "all", category: "", city: "", query: "", sort: "yeni" };
    const tip = document.getElementById("filter-tip");
    const cat = document.getElementById("filter-category");
    const city = document.getElementById("filter-city");
    const q = document.getElementById("search-query");
    const sort = document.getElementById("sort-select");
    if (tip) tip.value = "all";
    if (cat) cat.value = "";
    if (city) city.value = "";
    if (q) q.value = "";
    if (sort) sort.value = "yeni";
    apply();
  });

  document.getElementById("search-form")?.addEventListener("submit", (e) => {
    e.preventDefault();
    state.query = document.getElementById("search-query")?.value || "";
    apply();
  });

  document.getElementById("sort-select")?.addEventListener("change", (e) => {
    state.sort = e.target.value;
    apply();
  });
})();
