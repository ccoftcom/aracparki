(() => {
  "use strict";

  const {
    POPULAR_CITIES,
    CATEGORIES,
    LISTINGS,
    escapeHtml,
    showToast,
    fillCategorySelect,
    fillCitySelect,
    matchesListing,
    readUrlState,
    buildListUrl,
    iconSvg,
    STORAGE_RECENT,
    loadJson,
    listingCardHtml,
  } = window.AP;

  const MAX_RECENT = 6;
  let state = { ...readUrlState(), filter: readUrlState().filter || "all" };
  if (!window.location.search) {
    state = { filter: "all", category: "", city: "", query: "", sort: "yeni" };
  }

  const recent = loadJson(STORAGE_RECENT, []).slice(0, MAX_RECENT);

  const goToList = () => {
    window.location.href = buildListUrl(state);
  };

  const renderListings = () => {
    const grid = document.getElementById("listing-grid");
    const countEl = document.getElementById("result-count");
    if (!grid) return;
    const items = LISTINGS.filter((item) => matchesListing(item, state)).slice(0, 12);
    grid.setAttribute("aria-busy", "false");
    if (countEl) countEl.textContent = `(${items.length})`;
    if (!items.length) {
      grid.innerHTML = `
        <div class="empty-state">
          <p>Bu kriterlere uygun ilan yok.</p>
          <a class="btn btn-machine" href="ilanlar.html">Tüm ilanları gör</a>
        </div>`;
      return;
    }
    grid.innerHTML = items.map((item) => listingCardHtml(item)).join("");
  };

  const renderRecent = () => {
    const section = document.getElementById("recent");
    const grid = document.getElementById("recent-grid");
    if (!section || !grid) return;
    const items = recent.map((id) => window.AP.getListing(id)).filter(Boolean);
    if (!items.length) {
      section.hidden = true;
      return;
    }
    section.hidden = false;
    grid.innerHTML = items.map((item) => listingCardHtml(item)).join("");
  };

  const renderCategories = () => {
    const grid = document.getElementById("category-grid");
    if (!grid) return;
    grid.innerHTML = CATEGORIES.map(
      (cat) => `
      <li>
        <a href="${escapeHtml(buildListUrl({ filter: "all", category: cat.name, city: "", query: "", sort: "yeni" }))}">
          ${iconSvg(cat.icon)}
          <span>${escapeHtml(cat.name)}</span>
          <span class="count">${new Intl.NumberFormat("tr-TR").format(cat.count)}</span>
        </a>
      </li>`
    ).join("");
  };

  const renderCities = () => {
    const grid = document.getElementById("city-grid");
    if (!grid) return;
    grid.innerHTML = POPULAR_CITIES.map(
      (city) => `
      <li>
        <a href="${escapeHtml(buildListUrl({ filter: "all", category: "", city: city.name, query: "", sort: "yeni" }))}">
          <span>${escapeHtml(city.name)}</span>
          <span class="count">${new Intl.NumberFormat("tr-TR").format(city.count)}</span>
        </a>
      </li>`
    ).join("");
  };

  const syncControls = () => {
    const q = document.getElementById("search-query");
    const cat = document.getElementById("filter-category");
    const city = document.getElementById("filter-city");
    if (q) q.value = state.query || "";
    if (cat) cat.value = state.category || "";
    if (city) city.value = state.city || "";
    document.querySelectorAll(".chip").forEach((el) => {
      el.setAttribute("aria-pressed", el.dataset.filter === state.filter ? "true" : "false");
    });
  };

  const catSelect = document.getElementById("filter-category");
  if (catSelect) {
    catSelect.innerHTML = "";
    fillCategorySelect(catSelect);
  }
  fillCitySelect(document.getElementById("filter-city"));
  renderCategories();
  renderCities();
  syncControls();
  renderListings();
  renderRecent();

  document.getElementById("search-form")?.addEventListener("submit", (e) => {
    e.preventDefault();
    state.query = document.getElementById("search-query")?.value || "";
    goToList();
  });

  document.getElementById("filter-form")?.addEventListener("submit", (e) => {
    e.preventDefault();
    state.category = document.getElementById("filter-category")?.value || "";
    state.city = document.getElementById("filter-city")?.value || "";
    goToList();
  });

  document.body.addEventListener("click", (e) => {
    const filterEl = e.target.closest(".chip[data-filter]");
    if (!filterEl) return;
    e.preventDefault();
    state.filter = filterEl.dataset.filter;
    syncControls();
    renderListings();
  });

  document.getElementById("seller-cta-btn")?.addEventListener("click", (e) => {
    e.preventDefault();
    showToast("İlan verme yakında açılacak (demo)");
  });

  const more = document.getElementById("see-all-listings");
  if (more) more.setAttribute("href", buildListUrl(state));
})();
