(() => {
  "use strict";

  const SEARCH_ICON = `
    <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round">
      <circle cx="11" cy="11" r="6.5" />
      <path d="M16.2 16.2L21 21" />
    </svg>`;

  const logoHtml = (className = "logo") => `
    <a class="${className}" href="index.html" aria-label="Araç Parkı ana sayfa">
      <span class="logo-arac">Araç</span><span class="logo-parki">Parkı</span>
    </a>`;

  const CAT_LINKS = [
    { href: "ilanlar.html?kategori=Paletli%20Ekskavat%C3%B6r", label: "Ekskavatör" },
    { href: "ilanlar.html?kategori=Beko%20Loder", label: "Beko Loder" },
    { href: "ilanlar.html?kategori=Lastikli%20Y%C3%BCkleyici", label: "Loader" },
    { href: "ilanlar.html?kategori=Forklift", label: "Forklift" },
    { href: "ilanlar.html?kategori=Vin%C3%A7", label: "Vinç" },
    { href: "ilanlar.html?tip=kiralik", label: "Kiralık" },
    { href: "ilanlar.html?tip=satilik", label: "Satılık" },
    { href: "ilanlar.html", label: "Tüm İlanlar" },
  ];

  const pageFromBody = () => document.body?.dataset?.page || "home";

  const anchor = (page, homeHash) => (page === "home" ? homeHash : `index.html${homeHash}`);

  const renderHeader = (page) => {
    const loginHref = anchor(page, "#giris");
    const ctaHref = anchor(page, "#ilan-ver");
    const searchAction =
      page === "detail"
        ? `action="ilanlar.html" method="get"`
        : "";
    const showCat = page !== "detail";

    const catBar = showCat
      ? `
      <nav class="cat-bar" aria-label="Hızlı kategoriler">
        <div class="container cat-bar-row">
          <a class="logo cat-bar-brand" href="index.html" aria-label="Araç Parkı ana sayfa">
            <span class="logo-arac">Araç</span><span class="logo-parki">Parkı</span>
          </a>
          <div class="cat-bar-clip">
            <div class="cat-bar-inner">
              ${CAT_LINKS.map((l) => `<a href="${l.href}">${l.label}</a>`).join("")}
            </div>
          </div>
          <div class="cat-bar-actions">
            <a class="link-quiet" href="${loginHref}">Giriş Yap</a>
            <a class="btn btn-machine header-cta" href="${ctaHref}">Ücretsiz İlan Ver</a>
          </div>
        </div>
      </nav>`
      : "";

    return `
    <header class="site-header" id="top">
      <div class="header-top">
        <div class="container header-top-inner">
          <button
            class="nav-toggle"
            type="button"
            aria-expanded="false"
            aria-controls="nav-mobile"
            aria-label="Menüyü aç"
          >
            <span aria-hidden="true"></span>
          </button>

          ${logoHtml()}

          <form class="header-search" id="search-form" role="search" ${searchAction}>
            <label class="sr-only" for="search-query">Kelime ara</label>
            <input
              id="search-query"
              name="q"
              type="search"
              placeholder="Marka, model veya ilan no"
              autocomplete="off"
              enterkeyhint="search"
            />
            <button class="btn btn-machine" type="submit">Ara</button>
          </form>

          <div class="header-actions">
            <a class="link-quiet" href="${loginHref}">Giriş Yap</a>
            <button
              class="search-toggle"
              type="button"
              aria-expanded="false"
              aria-controls="search-form"
              aria-label="Aramayı aç"
            >
              ${SEARCH_ICON}
            </button>
            <a class="btn btn-machine header-cta" href="${ctaHref}">Ücretsiz İlan Ver</a>
          </div>
        </div>
      </div>

      ${catBar}

      <div class="nav-mobile" id="nav-mobile" hidden>
        <a href="ilanlar.html?tip=satilik">Satılık</a>
        <a href="ilanlar.html?tip=kiralik">Kiralık</a>
        <a href="ilanlar.html?tip=ikinci-el">İkinci El</a>
        <a href="${page === "home" ? "#kategoriler" : "index.html#kategoriler"}">Kategoriler</a>
        <a href="${page === "home" ? "#sehirler" : "index.html#sehirler"}">Şehirler</a>
        <a href="${ctaHref}">Ücretsiz İlan Ver</a>
      </div>
    </header>`;
  };

  const renderFooter = (page) => {
    const compact = page !== "home";
    if (compact) {
      return `
    <footer class="site-footer" id="giris">
      <div class="container">
        <div class="footer-bottom">
          <span>© 2026 Araç Parkı · aracparki.com</span>
          <nav aria-label="Alt bağlantılar">
            <a href="index.html">Anasayfa</a>
            <a href="ilanlar.html">Tüm ilanlar</a>
            <a href="index.html#ilan-ver">İlan ver</a>
          </nav>
        </div>
      </div>
    </footer>`;
    }

    return `
    <footer class="site-footer" id="giris">
      <div class="container">
        <div class="footer-grid">
          <div class="footer-brand">
            <a class="logo" href="#top" aria-label="Araç Parkı">
              <span class="logo-arac">Araç</span><span class="logo-parki">Parkı</span>
            </a>
            <p>Türkiye genelinde satılık, kiralık ve ikinci el iş makineleri pazar yeri.</p>
          </div>
          <div class="footer-col">
            <h3>Keşfet</h3>
            <ul>
              <li><a href="ilanlar.html?tip=satilik">Satılık</a></li>
              <li><a href="ilanlar.html?tip=kiralik">Kiralık</a></li>
              <li><a href="ilanlar.html?tip=ikinci-el">İkinci El</a></li>
              <li><a href="ilanlar.html">Tüm ilanlar</a></li>
            </ul>
          </div>
          <div class="footer-col">
            <h3>Şehirler</h3>
            <ul>
              <li><a href="ilanlar.html?il=%C4%B0stanbul">İstanbul</a></li>
              <li><a href="ilanlar.html?il=Ankara">Ankara</a></li>
              <li><a href="ilanlar.html?il=%C4%B0zmir">İzmir</a></li>
              <li><a href="#sehirler">Popüler iller</a></li>
            </ul>
          </div>
          <div class="footer-col">
            <h3>Kurumsal</h3>
            <ul>
              <li><a href="#guven">Güven</a></li>
              <li><a href="#ilan-ver">İlan ver</a></li>
              <li><a href="#giris">Giriş</a></li>
            </ul>
          </div>
        </div>
        <div class="footer-bottom">
          <span>© 2026 Araç Parkı · aracparki.com</span>
          <span>Satılık · Kiralık · İkinci El</span>
        </div>
      </div>
    </footer>`;
  };

  const mount = () => {
    const page = pageFromBody();
    const headerHost = document.getElementById("site-header");
    const footerHost = document.getElementById("site-footer");
    if (headerHost) headerHost.outerHTML = renderHeader(page);
    if (footerHost) footerHost.outerHTML = renderFooter(page);
  };

  mount();

  window.AP = window.AP || {};
  window.AP.chrome = { mount, renderHeader, renderFooter, CAT_LINKS };
})();
