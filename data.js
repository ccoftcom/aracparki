(() => {
  "use strict";

  const ILLER = [
    "Adana",
    "Adıyaman",
    "Afyonkarahisar",
    "Ağrı",
    "Aksaray",
    "Amasya",
    "Ankara",
    "Antalya",
    "Ardahan",
    "Artvin",
    "Aydın",
    "Balıkesir",
    "Bartın",
    "Batman",
    "Bayburt",
    "Bilecik",
    "Bingöl",
    "Bitlis",
    "Bolu",
    "Burdur",
    "Bursa",
    "Çanakkale",
    "Çankırı",
    "Çorum",
    "Denizli",
    "Diyarbakır",
    "Düzce",
    "Edirne",
    "Elazığ",
    "Erzincan",
    "Erzurum",
    "Eskişehir",
    "Gaziantep",
    "Giresun",
    "Gümüşhane",
    "Hakkari",
    "Hatay",
    "Iğdır",
    "Isparta",
    "İstanbul",
    "İzmir",
    "Kahramanmaraş",
    "Karabük",
    "Karaman",
    "Kars",
    "Kastamonu",
    "Kayseri",
    "Kırıkkale",
    "Kırklareli",
    "Kırşehir",
    "Kilis",
    "Kocaeli",
    "Konya",
    "Kütahya",
    "Malatya",
    "Manisa",
    "Mardin",
    "Mersin",
    "Muğla",
    "Muş",
    "Nevşehir",
    "Niğde",
    "Ordu",
    "Osmaniye",
    "Rize",
    "Sakarya",
    "Samsun",
    "Siirt",
    "Sinop",
    "Sivas",
    "Şanlıurfa",
    "Şırnak",
    "Tekirdağ",
    "Tokat",
    "Trabzon",
    "Tunceli",
    "Uşak",
    "Van",
    "Yalova",
    "Yozgat",
    "Zonguldak"
  ];

  const POPULAR_CITIES = [
    { name: "İstanbul", count: 1840 },
    { name: "Ankara", count: 1120 },
    { name: "İzmir", count: 960 },
    { name: "Bursa", count: 640 },
    { name: "Antalya", count: 520 },
    { name: "Adana", count: 410 },
    { name: "Konya", count: 380 },
    { name: "Gaziantep", count: 350 },
    { name: "Kocaeli", count: 310 },
    { name: "Mersin", count: 280 },
    { name: "Kayseri", count: 240 },
    { name: "Diyarbakır", count: 210 },
  ];

  const CATEGORIES = [
    { name: "Paletli Ekskavatör", count: 2840, icon: "excavator" },
    { name: "Beko Loder", count: 1920, icon: "backhoe" },
    { name: "Lastikli Yükleyici", count: 1480, icon: "loader" },
    { name: "Forklift", count: 1360, icon: "forklift" },
    { name: "Vinç", count: 920, icon: "crane" },
    { name: "Dozer", count: 640, icon: "dozer" },
    { name: "Greyder", count: 410, icon: "grader" },
    { name: "Silindir", count: 380, icon: "roller" },
    { name: "Mini Ekskavatör", count: 870, icon: "mini" },
    { name: "Telehandler", count: 520, icon: "lift" },
    { name: "Beton", count: 290, icon: "concrete" },
    { name: "Kırıcı", count: 240, icon: "crusher" },
  ];

  const LISTINGS = [
  {
    id: 1,
    title: "Caterpillar 320D",
    category: "Paletli Ekskavatör",
    intent: "ikinci-el",
    intents: ["satilik", "ikinci-el"],
    year: 2019,
    hours: 7200,
    tons: 22,
    hp: 162,
    city: "İstanbul",
    district: "Tuzla",
    price: 4850000,
    priceUnit: null,
    seller: "Bayi",
    verified: true,
    phone: "02165550101",
    image: "https://images.unsplash.com/photo-1504307651254-35680f356dfd?auto=format&fit=crop&w=900&q=80",
    images: ["https://images.unsplash.com/photo-1504307651254-35680f356dfd?auto=format&fit=crop&w=900&q=80", "https://images.unsplash.com/photo-1504307651254-35680f356dfd?auto=format&fit=crop&w=900&q=80&sig=2", "https://images.unsplash.com/photo-1504307651254-35680f356dfd?auto=format&fit=crop&w=900&q=80&sig=3"],
    sellerName: "Bayi Makine 1",
    listedAt: "2026-07-11",
    adNo: "AP-100001",
    description: "2019 model Caterpillar 320D. Paletli Ekskavatör kategorisinde, 7200 saat, 22 ton, 162 HP. Düzenli bakımlı, sahada çalışmaya hazır. Konum: İstanbul / Tuzla. Detay ve keşif için iletişime geçiniz."
  },
  {
    id: 2,
    title: "Hidromek HMK 102B",
    category: "Beko Loder",
    intent: "satilik",
    intents: ["satilik", "ikinci-el"],
    year: 2021,
    hours: 4100,
    tons: 8.5,
    hp: 100,
    city: "Ankara",
    district: "Sincan",
    price: 2750000,
    priceUnit: null,
    seller: "Sahibi",
    verified: true,
    phone: "03125550102",
    image: "https://images.unsplash.com/photo-1581092160562-40aa08e78837?auto=format&fit=crop&w=900&q=80",
    images: ["https://images.unsplash.com/photo-1581092160562-40aa08e78837?auto=format&fit=crop&w=900&q=80", "https://images.unsplash.com/photo-1581092160562-40aa08e78837?auto=format&fit=crop&w=900&q=80&sig=2", "https://images.unsplash.com/photo-1581092160562-40aa08e78837?auto=format&fit=crop&w=900&q=80&sig=3"],
    sellerName: "Sahibi Makine 2",
    listedAt: "2026-07-12",
    adNo: "AP-100002",
    description: "2021 model Hidromek HMK 102B. Beko Loder kategorisinde, 4100 saat, 8.5 ton, 100 HP. Düzenli bakımlı, sahada çalışmaya hazır. Konum: Ankara / Sincan. Detay ve keşif için iletişime geçiniz."
  },
  {
    id: 3,
    title: "Komatsu PC210",
    category: "Paletli Ekskavatör",
    intent: "kiralik",
    intents: ["kiralik"],
    year: 2020,
    hours: 5800,
    tons: 21,
    hp: 158,
    city: "İzmir",
    district: "Aliağa",
    price: 18500,
    priceUnit: "gün",
    seller: "Bayi",
    verified: true,
    phone: "02325550103",
    image: "https://images.unsplash.com/photo-1541888946425-d81bb19240f5?auto=format&fit=crop&w=900&q=80",
    images: ["https://images.unsplash.com/photo-1541888946425-d81bb19240f5?auto=format&fit=crop&w=900&q=80", "https://images.unsplash.com/photo-1541888946425-d81bb19240f5?auto=format&fit=crop&w=900&q=80&sig=2", "https://images.unsplash.com/photo-1541888946425-d81bb19240f5?auto=format&fit=crop&w=900&q=80&sig=3"],
    sellerName: "Bayi Makine 3",
    listedAt: "2026-07-13",
    adNo: "AP-100003",
    description: "2020 model Komatsu PC210. Paletli Ekskavatör kategorisinde, 5800 saat, 21 ton, 158 HP. Düzenli bakımlı, sahada çalışmaya hazır. Konum: İzmir / Aliağa. Detay ve keşif için iletişime geçiniz."
  },
  {
    id: 4,
    title: "Volvo L120H",
    category: "Lastikli Yükleyici",
    intent: "satilik",
    intents: ["satilik", "ikinci-el"],
    year: 2018,
    hours: 9100,
    tons: 20,
    hp: 276,
    city: "Bursa",
    district: "Nilüfer",
    price: 6200000,
    priceUnit: null,
    seller: "Bayi",
    verified: false,
    phone: "02245550104",
    image: "https://images.unsplash.com/photo-1504917595217-d4dc5ebe6122?auto=format&fit=crop&w=900&q=80",
    images: ["https://images.unsplash.com/photo-1504917595217-d4dc5ebe6122?auto=format&fit=crop&w=900&q=80", "https://images.unsplash.com/photo-1504917595217-d4dc5ebe6122?auto=format&fit=crop&w=900&q=80&sig=2", "https://images.unsplash.com/photo-1504917595217-d4dc5ebe6122?auto=format&fit=crop&w=900&q=80&sig=3"],
    sellerName: "Bayi Makine 4",
    listedAt: "2026-07-14",
    adNo: "AP-100004",
    description: "2018 model Volvo L120H. Lastikli Yükleyici kategorisinde, 9100 saat, 20 ton, 276 HP. Düzenli bakımlı, sahada çalışmaya hazır. Konum: Bursa / Nilüfer. Detay ve keşif için iletişime geçiniz."
  },
  {
    id: 5,
    title: "JCB 3CX",
    category: "Beko Loder",
    intent: "kiralik",
    intents: ["kiralik"],
    year: 2022,
    hours: 2100,
    tons: 8,
    hp: 92,
    city: "Antalya",
    district: "Kepez",
    price: 9500,
    priceUnit: "gün",
    seller: "Sahibi",
    verified: true,
    phone: "02425550105",
    image: "https://images.unsplash.com/photo-1590644365607-1c3d4c1b0f0b?auto=format&fit=crop&w=900&q=80",
    images: ["https://images.unsplash.com/photo-1590644365607-1c3d4c1b0f0b?auto=format&fit=crop&w=900&q=80", "https://images.unsplash.com/photo-1590644365607-1c3d4c1b0f0b?auto=format&fit=crop&w=900&q=80&sig=2", "https://images.unsplash.com/photo-1590644365607-1c3d4c1b0f0b?auto=format&fit=crop&w=900&q=80&sig=3"],
    sellerName: "Sahibi Makine 5",
    listedAt: "2026-07-15",
    adNo: "AP-100005",
    description: "2022 model JCB 3CX. Beko Loder kategorisinde, 2100 saat, 8 ton, 92 HP. Düzenli bakımlı, sahada çalışmaya hazır. Konum: Antalya / Kepez. Detay ve keşif için iletişime geçiniz."
  },
  {
    id: 6,
    title: "Toyota 8FD30",
    category: "Forklift",
    intent: "ikinci-el",
    intents: ["satilik", "ikinci-el"],
    year: 2017,
    hours: 6400,
    tons: 3,
    hp: 54,
    city: "Kocaeli",
    district: "Gebze",
    price: 890000,
    priceUnit: null,
    seller: "Bayi",
    verified: true,
    phone: "02625550106",
    image: "https://images.unsplash.com/photo-1566576721346-d4a3b4eaeb55?auto=format&fit=crop&w=900&q=80",
    images: ["https://images.unsplash.com/photo-1566576721346-d4a3b4eaeb55?auto=format&fit=crop&w=900&q=80", "https://images.unsplash.com/photo-1566576721346-d4a3b4eaeb55?auto=format&fit=crop&w=900&q=80&sig=2", "https://images.unsplash.com/photo-1566576721346-d4a3b4eaeb55?auto=format&fit=crop&w=900&q=80&sig=3"],
    sellerName: "Bayi Makine 6",
    listedAt: "2026-07-16",
    adNo: "AP-100006",
    description: "2017 model Toyota 8FD30. Forklift kategorisinde, 6400 saat, 3 ton, 54 HP. Düzenli bakımlı, sahada çalışmaya hazır. Konum: Kocaeli / Gebze. Detay ve keşif için iletişime geçiniz."
  },
  {
    id: 7,
    title: "Liebherr LTM 1100",
    category: "Vinç",
    intent: "kiralik",
    intents: ["kiralik"],
    year: 2016,
    hours: 4200,
    tons: 100,
    hp: 367,
    city: "Gaziantep",
    district: "Şehitkamil",
    price: 45000,
    priceUnit: "gün",
    seller: "Bayi",
    verified: true,
    phone: "03425550107",
    image: "https://images.unsplash.com/photo-1503387762-592deb58ef4e?auto=format&fit=crop&w=900&q=80",
    images: ["https://images.unsplash.com/photo-1503387762-592deb58ef4e?auto=format&fit=crop&w=900&q=80", "https://images.unsplash.com/photo-1503387762-592deb58ef4e?auto=format&fit=crop&w=900&q=80&sig=2", "https://images.unsplash.com/photo-1503387762-592deb58ef4e?auto=format&fit=crop&w=900&q=80&sig=3"],
    sellerName: "Bayi Makine 7",
    listedAt: "2026-07-17",
    adNo: "AP-100007",
    description: "2016 model Liebherr LTM 1100. Vinç kategorisinde, 4200 saat, 100 ton, 367 HP. Düzenli bakımlı, sahada çalışmaya hazır. Konum: Gaziantep / Şehitkamil. Detay ve keşif için iletişime geçiniz."
  },
  {
    id: 8,
    title: "Caterpillar D6T",
    category: "Dozer",
    intent: "satilik",
    intents: ["satilik", "ikinci-el"],
    year: 2015,
    hours: 11200,
    tons: 23,
    hp: 215,
    city: "Konya",
    district: "Selçuklu",
    price: 7100000,
    priceUnit: null,
    seller: "Sahibi",
    verified: false,
    phone: "03325550108",
    image: "https://images.unsplash.com/photo-1590496793929-36417d311cba?auto=format&fit=crop&w=900&q=80",
    images: ["https://images.unsplash.com/photo-1590496793929-36417d311cba?auto=format&fit=crop&w=900&q=80", "https://images.unsplash.com/photo-1590496793929-36417d311cba?auto=format&fit=crop&w=900&q=80&sig=2", "https://images.unsplash.com/photo-1590496793929-36417d311cba?auto=format&fit=crop&w=900&q=80&sig=3"],
    sellerName: "Sahibi Makine 8",
    listedAt: "2026-07-18",
    adNo: "AP-100008",
    description: "2015 model Caterpillar D6T. Dozer kategorisinde, 11200 saat, 23 ton, 215 HP. Düzenli bakımlı, sahada çalışmaya hazır. Konum: Konya / Selçuklu. Detay ve keşif için iletişime geçiniz."
  },
  {
    id: 9,
    title: "Case 580ST",
    category: "Beko Loder",
    intent: "ikinci-el",
    intents: ["satilik", "ikinci-el"],
    year: 2020,
    hours: 3500,
    tons: 8.2,
    hp: 97,
    city: "Adana",
    district: "Seyhan",
    price: 3180000,
    priceUnit: null,
    seller: "Bayi",
    verified: true,
    phone: "03225550109",
    image: "https://images.unsplash.com/photo-1621905252507-b35492cc74b4?auto=format&fit=crop&w=900&q=80",
    images: ["https://images.unsplash.com/photo-1621905252507-b35492cc74b4?auto=format&fit=crop&w=900&q=80", "https://images.unsplash.com/photo-1621905252507-b35492cc74b4?auto=format&fit=crop&w=900&q=80&sig=2", "https://images.unsplash.com/photo-1621905252507-b35492cc74b4?auto=format&fit=crop&w=900&q=80&sig=3"],
    sellerName: "Bayi Makine 9",
    listedAt: "2026-07-10",
    adNo: "AP-100009",
    description: "2020 model Case 580ST. Beko Loder kategorisinde, 3500 saat, 8.2 ton, 97 HP. Düzenli bakımlı, sahada çalışmaya hazır. Konum: Adana / Seyhan. Detay ve keşif için iletişime geçiniz."
  },
  {
    id: 10,
    title: "Bobcat E50",
    category: "Mini Ekskavatör",
    intent: "kiralik",
    intents: ["kiralik"],
    year: 2023,
    hours: 980,
    tons: 5.2,
    hp: 55,
    city: "Mersin",
    district: "Yenişehir",
    price: 6200,
    priceUnit: "gün",
    seller: "Bayi",
    verified: true,
    phone: "03245550110",
    image: "https://images.unsplash.com/photo-1581092918056-0c4c3acd3789?auto=format&fit=crop&w=900&q=80",
    images: ["https://images.unsplash.com/photo-1581092918056-0c4c3acd3789?auto=format&fit=crop&w=900&q=80", "https://images.unsplash.com/photo-1581092918056-0c4c3acd3789?auto=format&fit=crop&w=900&q=80&sig=2", "https://images.unsplash.com/photo-1581092918056-0c4c3acd3789?auto=format&fit=crop&w=900&q=80&sig=3"],
    sellerName: "Bayi Makine 10",
    listedAt: "2026-07-11",
    adNo: "AP-100010",
    description: "2023 model Bobcat E50. Mini Ekskavatör kategorisinde, 980 saat, 5.2 ton, 55 HP. Düzenli bakımlı, sahada çalışmaya hazır. Konum: Mersin / Yenişehir. Detay ve keşif için iletişime geçiniz."
  },
  {
    id: 11,
    title: "Manitou MT 1840",
    category: "Telehandler",
    intent: "satilik",
    intents: ["satilik", "ikinci-el"],
    year: 2019,
    hours: 4700,
    tons: 12,
    hp: 101,
    city: "Kayseri",
    district: "Melikgazi",
    price: 2450000,
    priceUnit: null,
    seller: "Sahibi",
    verified: true,
    phone: "03525550111",
    image: "https://images.unsplash.com/photo-1581092162384-8987c1d64718?auto=format&fit=crop&w=900&q=80",
    images: ["https://images.unsplash.com/photo-1581092162384-8987c1d64718?auto=format&fit=crop&w=900&q=80", "https://images.unsplash.com/photo-1581092162384-8987c1d64718?auto=format&fit=crop&w=900&q=80&sig=2", "https://images.unsplash.com/photo-1581092162384-8987c1d64718?auto=format&fit=crop&w=900&q=80&sig=3"],
    sellerName: "Sahibi Makine 11",
    listedAt: "2026-07-12",
    adNo: "AP-100011",
    description: "2019 model Manitou MT 1840. Telehandler kategorisinde, 4700 saat, 12 ton, 101 HP. Düzenli bakımlı, sahada çalışmaya hazır. Konum: Kayseri / Melikgazi. Detay ve keşif için iletişime geçiniz."
  },
  {
    id: 12,
    title: "Doosan DX225",
    category: "Paletli Ekskavatör",
    intent: "satilik",
    intents: ["satilik", "ikinci-el"],
    year: 2021,
    hours: 3900,
    tons: 23.5,
    hp: 173,
    city: "Diyarbakır",
    district: "Bağlar",
    price: 5120000,
    priceUnit: null,
    seller: "Bayi",
    verified: true,
    phone: "04125550112",
    image: "https://images.unsplash.com/photo-1504307651254-35680f356dfd?auto=format&fit=crop&w=900&q=70",
    images: ["https://images.unsplash.com/photo-1504307651254-35680f356dfd?auto=format&fit=crop&w=900&q=70", "https://images.unsplash.com/photo-1504307651254-35680f356dfd?auto=format&fit=crop&w=900&q=70&sig=2", "https://images.unsplash.com/photo-1504307651254-35680f356dfd?auto=format&fit=crop&w=900&q=70&sig=3"],
    sellerName: "Bayi Makine 12",
    listedAt: "2026-07-13",
    adNo: "AP-100012",
    description: "2021 model Doosan DX225. Paletli Ekskavatör kategorisinde, 3900 saat, 23.5 ton, 173 HP. Düzenli bakımlı, sahada çalışmaya hazır. Konum: Diyarbakır / Bağlar. Detay ve keşif için iletişime geçiniz."
  }
  ];

  const escapeHtml = (value) =>
    String(value)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

  const formatPrice = (n) =>
    new Intl.NumberFormat("tr-TR", {
      style: "currency",
      currency: "TRY",
      maximumFractionDigits: 0,
    }).format(n);

  const formatHours = (n) => new Intl.NumberFormat("tr-TR").format(n) + " saat";
  const formatTons = (n) => String(n).replace(".", ",") + " ton";
  const formatHp = (n) => n + " HP";

  const badgeLabel = (listing) => {
    if (listing.intent === "kiralik") return { text: "Kiralık", className: "badge badge-rent" };
    if (listing.intent === "ikinci-el") return { text: "Satılık · İkinci El", className: "badge badge-used" };
    return { text: "Satılık", className: "badge" };
  };

  const intentLabel = (filter) => {
    const map = { all: "Tümü", satilik: "Satılık", kiralik: "Kiralık", "ikinci-el": "İkinci El" };
    return map[filter] || filter;
  };

  const getListing = (id) => LISTINGS.find((item) => item.id === Number(id));

  const matchesListing = (listing, state) => {
    const intentOk =
      state.filter === "all" ||
      listing.intents.includes(state.filter) ||
      listing.intent === state.filter;
    const categoryOk = !state.category || listing.category === state.category;
    const cityOk = !state.city || listing.city === state.city;
    const q = (state.query || "").trim().toLowerCase();
    const queryOk =
      !q ||
      listing.title.toLowerCase().includes(q) ||
      listing.category.toLowerCase().includes(q) ||
      listing.city.toLowerCase().includes(q) ||
      String(listing.id) === q ||
      (listing.adNo && listing.adNo.toLowerCase().includes(q));
    return intentOk && categoryOk && cityOk && queryOk;
  };

  const readUrlState = () => {
    const params = new URLSearchParams(window.location.search);
    const tip = params.get("tip") || "all";
    return {
      filter: ["all", "satilik", "kiralik", "ikinci-el"].includes(tip) ? tip : "all",
      category: params.get("kategori") || "",
      city: params.get("il") || "",
      query: params.get("q") || "",
      sort: params.get("siralama") || "yeni",
    };
  };

  const buildListUrl = (state) => {
    const params = new URLSearchParams();
    if (state.filter && state.filter !== "all") params.set("tip", state.filter);
    if (state.category) params.set("kategori", state.category);
    if (state.city) params.set("il", state.city);
    if ((state.query || "").trim()) params.set("q", state.query.trim());
    if (state.sort && state.sort !== "yeni") params.set("siralama", state.sort);
    const qs = params.toString();
    return "ilanlar.html" + (qs ? `?${qs}` : "");
  };

  const detailUrl = (id) => `ilan.html?id=${id}`;

  const sortListings = (items, sort) => {
    const list = items.slice();
    if (sort === "fiyat-artan") list.sort((a, b) => a.price - b.price);
    else if (sort === "fiyat-azalan") list.sort((a, b) => b.price - a.price);
    else if (sort === "saat") list.sort((a, b) => a.hours - b.hours);
    else list.sort((a, b) => (b.listedAt || "").localeCompare(a.listedAt || ""));
    return list;
  };

  const STORAGE_FAV = "ap_favorites";
  const STORAGE_RECENT = "ap_recent";

  const loadJson = (key, fallback) => {
    try {
      const raw = localStorage.getItem(key);
      if (!raw) return fallback;
      const parsed = JSON.parse(raw);
      return Array.isArray(parsed) ? parsed : fallback;
    } catch {
      return fallback;
    }
  };

  const saveJson = (key, value) => {
    try {
      localStorage.setItem(key, JSON.stringify(value));
    } catch { /* ignore */ }
  };

  /**
   * Kategori ikonları — tek stil sistemi (stroke only).
   * Optik zemin Y≈17.5 · stroke 1.75 · 24×24 grid.
   * Tabler (MIT) path’leri: backhoe, forklift, crane — geri kalan özel stroke.
   */
  const iconSvg = (type) => {
    const attrs =
      'fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round"';

    // Ortak: palet (track) parçası
    const track = (x, w = 10) =>
      `<rect x="${x}" y="16" width="${w}" height="3.5" rx="1.2"/>`;

    const paths = {
      // Paletli ekskavatör — kabin + bom + kova + palet
      excavator: [
        track(3, 11),
        `<path d="M5 16V11h6v5"/>`,
        `<path d="M11 12l5-6 3 1.2-4.2 6.2"/>`,
        `<path d="M19 7.2l1.5-2.2"/>`,
        `<path d="M16.8 13.5l2.4 1.2-1.6 1.8"/>`,
      ].join(""),

      // Mini — aynı aile, sıkışık
      mini: [
        track(5, 9),
        `<path d="M6.5 16V12h5v4"/>`,
        `<path d="M11.5 12.5l3.2-4 2.2.9-2.8 4"/>`,
        `<path d="M16.9 9.4l1-1.6"/>`,
        `<path d="M14.8 13.8l1.8.9-1.2 1.4"/>`,
      ].join(""),

      // Beko loder — Tabler backhoe (MIT)
      backhoe: `<path d="M2 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0m9 0a2 2 0 1 0 4 0a2 2 0 1 0-4 0m2 2H4m0-4h9"/><path d="M8 12V7h2a3 3 0 0 1 3 3v5"/><path d="M5 15v-2a1 1 0 0 1 1-1h7m8.12-2.12L18 5l-5 5m8.12-.12A3 3 0 0 1 19 15a3 3 0 0 1-2.12-.88z"/>`,

      // Lastikli yükleyici — yükselen ön kova
      loader: [
        `<circle cx="7" cy="17.5" r="2.1"/>`,
        `<circle cx="16" cy="17.5" r="2.1"/>`,
        `<path d="M5 15.5h12V11H9.5z"/>`,
        `<path d="M9.5 11V8.5h3.5"/>`,
        `<path d="M14.5 12.5L19 8.5l2.2 1.5-3.2 5"/>`,
        `<path d="M21.2 10l.8-1.5"/>`,
      ].join(""),

      // Forklift — Tabler (MIT)
      forklift: `<path d="M3 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0m9 0a2 2 0 1 0 4 0a2 2 0 1 0-4 0m-5 0h5"/><path d="M3 17v-6h13v6M5 11V7h4m0 4V5h4l3 6m6 4h-3V5m-3 8h3"/>`,

      // Vinç — Tabler kule vinç (MIT)
      crane: `<path d="M6 21h6m-3 0V3L3 9h18M9 3l10 6"/><path d="M17 9v4a2 2 0 1 1-2 2"/>`,

      // Dozer — palet + kalın bıçak
      dozer: [
        track(4, 11),
        `<path d="M5.5 16V10.5h8V16"/>`,
        `<path d="M9 10.5V8h3.5"/>`,
        `<path d="M17 9v9"/>`,
        `<path d="M17 11h2.5v5H17"/>`,
      ].join(""),

      // Greyder — uzun şasi + ortada kalın bıçak
      grader: [
        `<circle cx="5.5" cy="17.5" r="2"/>`,
        `<circle cx="18.5" cy="17.5" r="2"/>`,
        `<path d="M4 15.5h16V11H10l-2 2.5H4z"/>`,
        `<path d="M11 11V7.5h3.5"/>`,
        `<path d="M3 18.8h18"/>`,
        `<path d="M8 18.8V16.2"/>`,
      ].join(""),

      // Silindir — çift tambur (optik küçültülmüş)
      roller: [
        `<circle cx="7.5" cy="15.5" r="3.4"/>`,
        `<circle cx="16.5" cy="15.8" r="2.8"/>`,
        `<path d="M10.8 14h2.4"/>`,
        `<path d="M16.5 13V9.5h2"/>`,
        `<path d="M6 12.2V9.8h3"/>`,
      ].join(""),

      // Telehandler — teleskoplu bom + uçta çatal
      lift: [
        `<circle cx="6.5" cy="17.5" r="2"/>`,
        `<circle cx="13.5" cy="17.5" r="2"/>`,
        `<path d="M4.5 15.5h11V11H8z"/>`,
        `<path d="M10 11L18 5.5"/>`,
        `<path d="M14 8.2L18 5.5"/>`,
        `<path d="M18 5.5h3"/>`,
        `<path d="M21 5.5V8.5"/>`,
        `<path d="M21 7h1.5"/>`,
      ].join(""),

      // Beton — mikser tamburu
      concrete: [
        `<circle cx="6.5" cy="17.5" r="2"/>`,
        `<circle cx="16.5" cy="17.5" r="2"/>`,
        `<path d="M4.5 15.5h15V12l-2.5-4H9.5L7.5 11H4.5z"/>`,
        `<ellipse cx="13.5" cy="9.5" rx="3.2" ry="2.6"/>`,
        `<path d="M13.5 7.2v4.6"/>`,
      ].join(""),

      // Kırıcı — hidrolik breaker uç (sivri)
      crusher: [
        `<circle cx="7" cy="17.5" r="2"/>`,
        `<circle cx="14" cy="17.5" r="2"/>`,
        `<path d="M5 15.5h11V11H9z"/>`,
        `<path d="M12 11L17 5"/>`,
        `<path d="M17 5l2.2 1.2-3.5 6"/>`,
        `<path d="M19.2 6.2L21 3.5"/>`,
      ].join(""),
    };

    const body = paths[type] || paths.excavator;
    return `<svg class="cat-ico" viewBox="0 0 24 24" aria-hidden="true" ${attrs}>${body}</svg>`;
  };

  window.AP = {
    ILLER,
    POPULAR_CITIES,
    CATEGORIES,
    LISTINGS,
    escapeHtml,
    formatPrice,
    formatHours,
    formatTons,
    formatHp,
    badgeLabel,
    intentLabel,
    getListing,
    matchesListing,
    readUrlState,
    buildListUrl,
    detailUrl,
    sortListings,
    iconSvg,
    STORAGE_FAV,
    STORAGE_RECENT,
    loadJson,
    saveJson,
  };
})();
