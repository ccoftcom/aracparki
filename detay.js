(() => {
  "use strict";

  const {
    escapeHtml,
    formatPrice,
    formatHours,
    formatTons,
    formatHp,
    badgeLabel,
    getListing,
    buildListUrl,
    STORAGE_FAV,
    STORAGE_RECENT,
    loadJson,
    saveJson,
  } = window.AP;

  const params = new URLSearchParams(window.location.search);
  const id = params.get("id");
  const listing = getListing(id);
  const root = document.getElementById("detail-root");

  let favorites = loadJson(STORAGE_FAV, []);
  let recent = loadJson(STORAGE_RECENT, []);

  const showToast = (msg) => {
    const toast = document.getElementById("toast");
    if (!toast) return;
    toast.hidden = false;
    toast.textContent = msg;
    clearTimeout(showToast._t);
    showToast._t = setTimeout(() => {
      toast.hidden = true;
    }, 2200);
  };

  const trackRecent = (listingId) => {
    const num = Number(listingId);
    recent = [num, ...recent.filter((x) => x !== num)].slice(0, 6);
    saveJson(STORAGE_RECENT, recent);
  };

  if (!listing) {
    root.innerHTML = `
      <p class="listing-empty">İlan bulunamadı.
        <a href="ilanlar.html">İlan listesine dön</a>
      </p>`;
    return;
  }

  trackRecent(listing.id);
  document.title = `${listing.title} | Araç Parkı`;

  const descMeta = document.querySelector('meta[name="description"]');
  if (descMeta) {
    descMeta.setAttribute(
      "content",
      `${listing.title} — ${listing.year}, ${listing.hours} saat, ${listing.city}. ${listing.priceUnit ? "Kiralık" : "Satılık"} iş makinesi ilanı.`
    );
  }

  const badge = badgeLabel(listing);
  const price = listing.priceUnit
    ? `${formatPrice(listing.price)} <small>/ ${escapeHtml(listing.priceUnit)}</small>`
    : formatPrice(listing.price);
  // Tek gerçek görsel; sahte thumb üretme (UX güveni)
  const images = [listing.image];
  const listBack = buildListUrl({
    filter: listing.intent === "kiralik" ? "kiralik" : listing.intent === "ikinci-el" ? "ikinci-el" : "satilik",
    category: listing.category,
    city: "",
    query: "",
    sort: "yeni",
  });
  const favOn = favorites.includes(listing.id);

  root.innerHTML = `
    <nav class="breadcrumb" aria-label="Sayfa yolu">
      <a href="index.html">Anasayfa</a>
      <span class="bc-sep">/</span>
      <a href="${listBack}">${escapeHtml(listing.category)}</a>
      <span class="bc-sep">/</span>
      <span>${escapeHtml(listing.title)}</span>
    </nav>

    <div class="detail-layout">
      <div class="detail-left">
        <div class="detail-gallery">
          <img id="gallery-main" class="gallery-main" src="${escapeHtml(images[0])}" alt="${escapeHtml(listing.title)}" width="900" height="600" />
        </div>

        <section class="detail-card">
          <h2 class="detail-section-title">İlan Bilgileri</h2>
          <table class="spec-table">
            <tbody>
              <tr><th>İlan No</th><td>${escapeHtml(listing.adNo)}</td></tr>
              <tr><th>İlan Tarihi</th><td>${escapeHtml(listing.listedAt)}</td></tr>
              <tr><th>Kategori</th><td>${escapeHtml(listing.category)}</td></tr>
              <tr><th>Tip</th><td><span class="${escapeHtml(badge.className)}">${escapeHtml(badge.text)}</span></td></tr>
              <tr><th>Model Yılı</th><td>${listing.year}</td></tr>
              <tr><th>Çalışma Saati</th><td>${formatHours(listing.hours)}</td></tr>
              <tr><th>Operasyon Ağırlığı</th><td>${formatTons(listing.tons)}</td></tr>
              <tr><th>Motor Gücü</th><td>${formatHp(listing.hp)}</td></tr>
              <tr><th>Konum</th><td>${escapeHtml(listing.city)} / ${escapeHtml(listing.district)}</td></tr>
            </tbody>
          </table>
        </section>

        <section class="detail-card">
          <h2 class="detail-section-title">Açıklama</h2>
          <p class="detail-desc">${escapeHtml(listing.description)}</p>
        </section>
      </div>

      <aside class="detail-right">
        <div class="detail-card detail-buybox sticky-box" id="buybox">
          <h1 class="detail-title">${escapeHtml(listing.title)}</h1>
          <div class="detail-price">${price}</div>
          <div class="detail-loc">${escapeHtml(listing.city)} / ${escapeHtml(listing.district)}</div>

          <div class="detail-actions">
            <button type="button" class="btn btn-machine btn-block" id="btn-offer">Teklif Al</button>
            <button type="button" class="btn btn-dark btn-block" id="btn-phone">Telefonu Göster</button>
            <a class="btn btn-ghost btn-block" id="btn-call" href="tel:${escapeHtml(listing.phone.replace(/\s/g, ""))}" hidden>Ara: ${escapeHtml(listing.phone)}</a>
            <button type="button" class="btn btn-ghost btn-block${favOn ? " is-fav" : ""}" id="btn-fav" aria-pressed="${favOn}">
              ${favOn ? "Favorilerde" : "Favorilere Ekle"}
            </button>
          </div>

          <div class="seller-box">
            <strong>${escapeHtml(listing.sellerName)}</strong>
            <span>${escapeHtml(listing.seller)}${listing.verified ? " · Doğrulanmış" : ""}</span>
          </div>

          <a class="detail-back" href="${listBack}">← Benzer ilanlara dön</a>
        </div>
      </aside>
    </div>

    <div class="mobile-cta" id="mobile-cta" aria-label="Hızlı işlemler">
      <div class="mobile-cta-price">${price}</div>
      <button type="button" class="btn btn-dark btn-sm" id="btn-phone-mobile">Telefon</button>
      <button type="button" class="btn btn-machine btn-sm" id="btn-offer-mobile">Teklif Al</button>
    </div>
  `;

  const revealPhone = () => {
    const call = document.getElementById("btn-call");
    const phoneBtn = document.getElementById("btn-phone");
    if (call) call.hidden = false;
    if (phoneBtn) phoneBtn.hidden = true;
    showToast(`Telefon: ${listing.phone}`);
  };

  document.getElementById("btn-offer")?.addEventListener("click", () => {
    showToast(`${listing.title} için teklif talebi alındı (demo)`);
  });
  document.getElementById("btn-offer-mobile")?.addEventListener("click", () => {
    showToast(`${listing.title} için teklif talebi alındı (demo)`);
  });
  document.getElementById("btn-phone")?.addEventListener("click", revealPhone);
  document.getElementById("btn-phone-mobile")?.addEventListener("click", revealPhone);

  document.getElementById("btn-fav")?.addEventListener("click", () => {
    const num = listing.id;
    const btn = document.getElementById("btn-fav");
    if (favorites.includes(num)) {
      favorites = favorites.filter((x) => x !== num);
      btn.textContent = "Favorilere Ekle";
      btn.classList.remove("is-fav");
      btn.setAttribute("aria-pressed", "false");
      showToast("Favorilerden çıkarıldı");
    } else {
      favorites = [num, ...favorites];
      btn.textContent = "Favorilerde";
      btn.classList.add("is-fav");
      btn.setAttribute("aria-pressed", "true");
      showToast("Favorilere eklendi");
    }
    saveJson(STORAGE_FAV, favorites);
  });
})();
