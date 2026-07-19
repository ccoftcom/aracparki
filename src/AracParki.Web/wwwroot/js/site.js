(() => {
  "use strict";

  const STORAGE_RECENT = "ap:recent";

  const toast = (message) => {
    const el = document.getElementById("toast");
    if (!el || !message) return;
    el.hidden = false;
    el.textContent = String(message);
    clearTimeout(toast._t);
    toast._t = setTimeout(() => {
      el.hidden = true;
      el.textContent = "";
    }, 2800);
  };

  const trackRecent = () => {
    const host = document.querySelector("[data-recent-id]");
    if (!host) return;
    const id = Number(host.getAttribute("data-recent-id"));
    if (!Number.isFinite(id)) return;
    let recent = [];
    try {
      recent = JSON.parse(localStorage.getItem(STORAGE_RECENT) || "[]");
    } catch {
      recent = [];
    }
    recent = [id, ...recent.filter((x) => x !== id)].slice(0, 6);
    localStorage.setItem(STORAGE_RECENT, JSON.stringify(recent));
  };

  document.addEventListener("click", (e) => {
    const toastBtn = e.target.closest("[data-toast]");
    if (toastBtn) {
      toast(toastBtn.getAttribute("data-toast"));
    }

  });

  document.addEventListener("click", async (e) => {
    const btn = e.target.closest("[data-reveal-phone]");
    if (!btn) return;
    e.preventDefault();
    const adNo = btn.getAttribute("data-reveal-phone");
    if (!adNo) return;
    btn.disabled = true;
    try {
      const res = await fetch(`/ilan/${encodeURIComponent(adNo)}/telefon`, {
        method: "POST",
        headers: { Accept: "application/json" },
      });
      if (res.status === 429) {
        toast("Çok fazla deneme. Bir dakika sonra tekrar deneyin.");
        return;
      }
      if (!res.ok) {
        toast("Telefon alınamadı.");
        return;
      }
      const data = await res.json();
      const phone = data?.phone;
      if (!phone) {
        toast("Telefon alınamadı.");
        return;
      }
      toast(`Telefon: ${phone}`);
      document.querySelectorAll("[data-phone-slot]").forEach((slot) => {
        slot.hidden = false;
        const link = slot.querySelector("a[data-phone-link]");
        if (link) {
          link.href = `tel:${String(phone).replace(/\s/g, "")}`;
          link.textContent = `Ara: ${phone}`;
        }
      });
      document.querySelectorAll("[data-reveal-phone]").forEach((el) => {
        el.hidden = true;
      });
    } catch {
      toast("Telefon alınamadı.");
    } finally {
      btn.disabled = false;
    }
  });

  const evaluatePasswordRules = (password) => ({
    len: password.length >= 8,
    letter: /[A-Za-zÀ-ÿĞÜŞİÖÇğüşıöç]/.test(password),
    digit: /\d/.test(password),
    noTriple: password.length > 0 && !/(.)\1\1/.test(password),
  });

  const setFeedbackVisible = (el, visible) => {
    if (!el) return;
    el.hidden = !visible;
  };

  const syncPasswordRules = (input, { showFail = false } = {}) => {
    const selector = input.getAttribute("data-password-rules");
    if (!selector) return;
    const form = input.closest("form");
    const list = (form && form.querySelector(selector)) || document.querySelector(selector);
    if (!list) return;

    const value = input.value || "";
    const hasValue = value.length > 0;
    setFeedbackVisible(list, hasValue);

    const hint = form?.querySelector("#password-hint") || document.getElementById("password-hint");
    if (hint) {
      hint.hidden = hasValue;
    }

    const checks = evaluatePasswordRules(value);
    list.querySelectorAll("[data-rule]").forEach((item) => {
      const key = item.getAttribute("data-rule");
      const ok = Boolean(checks[key]);
      item.classList.toggle("is-ok", ok);
      item.classList.toggle("is-fail", showFail && hasValue && !ok);
    });
  };

  const syncPasswordMatch = (form, { showFail = false } = {}) => {
    if (!(form instanceof HTMLFormElement)) return;
    const password = form.querySelector("input[data-password-rules]");
    const confirm = form.querySelector("input[data-password-match]");
    if (!(password instanceof HTMLInputElement) || !(confirm instanceof HTMLInputElement)) return;
    const selector = confirm.getAttribute("data-password-match");
    if (!selector) return;
    const list = form.querySelector(selector) || document.querySelector(selector);
    if (!list) return;

    const pwd = password.value || "";
    const conf = confirm.value || "";
    const hasConfirm = conf.length > 0;
    setFeedbackVisible(list, hasConfirm);

    const matched = hasConfirm && pwd === conf;
    list.querySelectorAll('[data-rule="match"]').forEach((item) => {
      item.classList.toggle("is-ok", matched);
      item.classList.toggle("is-fail", showFail && hasConfirm && !matched);
      const okText = item.getAttribute("data-ok-text");
      const failText = item.getAttribute("data-fail-text");
      if (okText && failText) {
        item.textContent = matched ? okText : failText;
      }
    });
  };

  document.addEventListener("input", (e) => {
    const input = e.target;
    if (!(input instanceof HTMLInputElement)) return;
    const form = input.closest("form");
    if (input.hasAttribute("data-password-rules")) {
      syncPasswordRules(input, { showFail: input.dataset.touched === "1" });
    }
    if (input.hasAttribute("data-password-rules") || input.hasAttribute("data-password-match")) {
      const confirm = form?.querySelector("input[data-password-match]");
      syncPasswordMatch(form, {
        showFail: confirm instanceof HTMLInputElement && confirm.dataset.touched === "1",
      });
    }
  });

  document.addEventListener("blur", (e) => {
    const input = e.target;
    if (!(input instanceof HTMLInputElement)) return;
    const form = input.closest("form");
    if (input.hasAttribute("data-password-rules")) {
      input.dataset.touched = "1";
      syncPasswordRules(input, { showFail: true });
      syncPasswordMatch(form, {
        showFail: form?.querySelector("input[data-password-match]")?.dataset.touched === "1",
      });
    }
    if (input.hasAttribute("data-password-match")) {
      input.dataset.touched = "1";
      syncPasswordMatch(form, { showFail: true });
    }
  }, true);

  document.addEventListener("alpine:init", () => {
    Alpine.data("siteChrome", () => ({
      navOpen: false,
      searchOpen: false,
      syncAria() {
        const nav = this.$refs.navToggle;
        if (nav) {
          nav.setAttribute("aria-expanded", String(this.navOpen));
          nav.setAttribute("aria-label", this.navOpen ? "Menüyü kapat" : "Menüyü aç");
        }
        const search = this.$refs.searchToggle;
        if (search) {
          search.setAttribute("aria-expanded", String(this.searchOpen));
          search.setAttribute("aria-label", this.searchOpen ? "Aramayı kapat" : "Aramayı aç");
        }
      },
      toggleNav() {
        this.navOpen = !this.navOpen;
        this.searchOpen = false;
        document.querySelector(".site-header")?.classList.remove("is-search-open");
        this.syncAria();
        if (this.navOpen) {
          this.$nextTick(() => document.querySelector("#nav-mobile a")?.focus());
        }
      },
      closeNav() {
        this.navOpen = false;
        this.syncAria();
      },
      toggleSearch() {
        this.searchOpen = !this.searchOpen;
        this.navOpen = false;
        document.querySelector(".site-header")?.classList.toggle("is-search-open", this.searchOpen);
        this.syncAria();
        if (this.searchOpen) {
          this.$nextTick(() => document.getElementById("search-query")?.focus());
        }
      },
      init() {
        window.addEventListener("keydown", (e) => {
          if (e.key !== "Escape") return;
          this.navOpen = false;
          this.searchOpen = false;
          document.querySelector(".site-header")?.classList.remove("is-search-open");
          this.syncAria();
        });
      },
    }));

    Alpine.data("authForm", () => ({
      showPassword: false,
      get passwordType() {
        return this.showPassword ? "text" : "password";
      },
      get passwordToggleLabel() {
        return this.showPassword ? "Şifreyi gizle" : "Şifreyi göster";
      },
      get showEye() {
        return !this.showPassword;
      },
      get showEyeOff() {
        return this.showPassword;
      },
      togglePassword() {
        this.showPassword = !this.showPassword;
      },
    }));

    const parseJsonAttr = (el, name, fallback) => {
      try {
        const raw = el.getAttribute(name);
        if (!raw) return fallback;
        return JSON.parse(raw);
      } catch {
        return fallback;
      }
    };

    Alpine.data("ilanVerCascader", () => ({
      groups: [],
      categories: [],
      brands: [],
      models: [],
      years: [],
      groupId: 0,
      groupName: "",
      categoryId: 0,
      categoryName: "",
      brandId: 0,
      brandName: "",
      modelId: 0,
      modelName: "",
      modelYear: 0,
      showCustomModel: false,
      brandsLoading: false,
      modelsLoading: false,

      get hasPath() {
        return this.pathText.length > 0;
      },
      get pathText() {
        const parts = [];
        if (this.groupName) parts.push(this.groupName);
        if (this.categoryName) parts.push(this.categoryName);
        if (this.brandName) parts.push(this.brandName);
        if (this.modelName) parts.push(this.modelName);
        if (this.modelYear) parts.push(String(this.modelYear));
        return parts.join(" › ");
      },
      get hasGroup() {
        return this.groupId > 0;
      },
      get hasCategory() {
        return this.categoryId > 0;
      },
      get hasBrand() {
        return this.brandId > 0;
      },
      get brandsEmpty() {
        return !this.brandsLoading && this.brands.length === 0;
      },
      get modelsReady() {
        return !this.modelsLoading;
      },
      get canPickYear() {
        return this.brandId > 0 && String(this.modelName || "").trim().length > 0;
      },
      get canContinue() {
        return (
          this.categoryId > 0 &&
          this.brandId > 0 &&
          String(this.modelName || "").trim().length > 0 &&
          this.modelYear >= 1950
        );
      },
      get cannotContinue() {
        return !this.canContinue;
      },
      get customModelClass() {
        return this.showCustomModel ? "is-selected" : "";
      },
      get groupsView() {
        const selected = this.groupId;
        return this.groups.map(function (g) {
          const on = Number(g.id) === selected;
          return {
            id: g.id,
            name: g.name,
            categories: g.categories || [],
            itemClass: on ? "is-selected" : "",
            ariaSelected: on ? "true" : "false",
          };
        });
      },
      get categoriesView() {
        const selected = this.categoryId;
        return this.categories.map(function (c) {
          const on = Number(c.id) === selected;
          return {
            id: c.id,
            name: c.name,
            itemClass: on ? "is-selected" : "",
            ariaSelected: on ? "true" : "false",
          };
        });
      },
      get brandsView() {
        const selected = this.brandId;
        return this.brands.map(function (b) {
          const on = Number(b.id) === selected;
          return {
            id: b.id,
            name: b.name,
            itemClass: on ? "is-selected" : "",
            ariaSelected: on ? "true" : "false",
          };
        });
      },
      get modelsView() {
        const selected = this.modelId;
        return this.models.map(function (m) {
          const on = Number(m.id) === selected;
          return {
            id: m.id,
            name: m.name,
            itemClass: on ? "is-selected" : "",
            ariaSelected: on ? "true" : "false",
          };
        });
      },
      get yearsView() {
        const selected = this.modelYear;
        return this.years.map(function (y) {
          const id = Number(y.id);
          const on = id === selected;
          return {
            id: id,
            label: String(id),
            itemClass: on ? "is-selected" : "",
            ariaSelected: on ? "true" : "false",
          };
        });
      },

      pickGroup(event) {
        const id = Number((event && event.currentTarget && event.currentTarget.getAttribute("data-id")) || 0);
        const g = this.groups.find(function (x) {
          return Number(x.id) === id;
        });
        if (g) this.selectGroup(g);
      },
      pickCategory(event) {
        const id = Number((event && event.currentTarget && event.currentTarget.getAttribute("data-id")) || 0);
        const c = this.categories.find(function (x) {
          return Number(x.id) === id;
        });
        if (c) this.selectCategory(c);
      },
      pickBrand(event) {
        const id = Number((event && event.currentTarget && event.currentTarget.getAttribute("data-id")) || 0);
        const b = this.brands.find(function (x) {
          return Number(x.id) === id;
        });
        if (b) this.selectBrand(b);
      },
      pickModel(event) {
        const id = Number((event && event.currentTarget && event.currentTarget.getAttribute("data-id")) || 0);
        const m = this.models.find(function (x) {
          return Number(x.id) === id;
        });
        if (m) this.selectModel(m);
      },
      pickYear(event) {
        this.modelYear = Number((event && event.currentTarget && event.currentTarget.getAttribute("data-id")) || 0);
      },

      init() {
        this.groups = parseJsonAttr(this.$el, "data-groups", []);
        const yMax = new Date().getFullYear() + 1;
        const list = [];
        for (let y = yMax; y >= 1950; y -= 1) {
          list.push({ id: y });
        }
        this.years = list;

        this.groupId = Number(this.$el.dataset.groupId || 0);
        this.groupName = this.$el.dataset.groupName || "";
        this.categoryId = Number(this.$el.dataset.categoryId || 0);
        this.categoryName = this.$el.dataset.categoryName || "";
        this.brandId = Number(this.$el.dataset.brandId || 0);
        this.brandName = this.$el.dataset.brandName || "";
        this.modelId = Number(this.$el.dataset.modelId || 0);
        this.modelName = this.$el.dataset.modelName || "";
        this.modelYear = Number(this.$el.dataset.modelYear || 0);

        if (this.groupId) {
          const g = this.groups.find((x) => Number(x.id) === this.groupId);
          this.categories = (g && g.categories) || [];
          if (g && !this.groupName) this.groupName = g.name;
        }
        if (this.categoryId) {
          this.loadBrands(true);
        }
      },
      reset() {
        this.groupId = 0;
        this.groupName = "";
        this.categoryId = 0;
        this.categoryName = "";
        this.brandId = 0;
        this.brandName = "";
        this.modelId = 0;
        this.modelName = "";
        this.modelYear = 0;
        this.categories = [];
        this.brands = [];
        this.models = [];
        this.showCustomModel = false;
      },
      selectGroup(g) {
        this.groupId = Number(g.id);
        this.groupName = g.name;
        this.categories = g.categories || [];
        this.categoryId = 0;
        this.categoryName = "";
        this.clearBrandDown();
        this.scrollBoard();
      },
      selectCategory(c) {
        this.categoryId = Number(c.id);
        this.categoryName = c.name;
        this.clearBrandDown();
        const self = this;
        this.loadBrands(false).then(function () {
          self.scrollBoard();
        });
      },
      async selectBrand(b) {
        this.brandId = Number(b.id);
        this.brandName = b.name;
        this.modelId = 0;
        this.modelName = "";
        this.modelYear = 0;
        this.showCustomModel = false;
        this.models = [];
        await this.loadModels(false);
        this.scrollBoard();
      },
      selectModel(m) {
        this.modelId = Number(m.id);
        this.modelName = m.name;
        this.showCustomModel = false;
        this.modelYear = 0;
        this.scrollBoard();
      },
      selectCustomModel() {
        this.modelId = 0;
        this.showCustomModel = true;
        this.modelYear = 0;
        this.$nextTick(function () {
          const el = document.getElementById("cascade-model-custom");
          if (el) el.focus();
        });
        this.scrollBoard();
      },
      onCustomModelInput() {
        this.modelId = 0;
        if (!String(this.modelName || "").trim()) this.modelYear = 0;
      },
      scrollBoard() {
        const self = this;
        this.$nextTick(function () {
          const board = self.$el.querySelector(".cascade-board");
          if (!board) return;
          board.scrollTo({ left: board.scrollWidth, behavior: "smooth" });
        });
      },
      clearBrandDown() {
        this.brandId = 0;
        this.brandName = "";
        this.modelId = 0;
        this.modelName = "";
        this.modelYear = 0;
        this.brands = [];
        this.models = [];
        this.showCustomModel = false;
      },
      async loadBrands(restore) {
        if (!this.categoryId) return;
        this.brandsLoading = true;
        try {
          const res = await fetch(`/api/catalog/categories/${this.categoryId}/brands`);
          if (!res.ok) {
            this.brands = [];
            return;
          }
          const data = await res.json();
          this.brands = (data || []).map(function (b) {
            return { id: b.id ?? b.Id, name: b.name ?? b.Name };
          });
          if (restore && this.brandId) {
            await this.loadModels(true);
          }
        } catch {
          this.brands = [];
        } finally {
          this.brandsLoading = false;
        }
      },
      async loadModels(restore) {
        if (!this.categoryId || !this.brandId) return;
        this.modelsLoading = true;
        try {
          const res = await fetch(
            `/api/catalog/categories/${this.categoryId}/brands/${this.brandId}/models`
          );
          if (!res.ok) {
            this.models = [];
            return;
          }
          const data = await res.json();
          this.models = (data || []).map(function (m) {
            return { id: m.id ?? m.Id, name: m.name ?? m.Name };
          });
          if (restore && this.modelId === 0 && this.modelName) {
            const name = this.modelName;
            const known = this.models.some(function (m) {
              return m.name === name;
            });
            this.showCustomModel = !known;
          }
        } catch {
          this.models = [];
        } finally {
          this.modelsLoading = false;
        }
      },
    }));

    Alpine.data("ilanVerMachine", () => ({
      categoryId: 0,
      brandId: 0,
      modelId: 0,
      modelName: "",
      models: [],
      init() {
        this.categoryId = Number(this.$el.dataset.categoryId || 0);
        this.brandId = Number(this.$el.dataset.brandId || 0);
        this.modelId = Number(this.$el.dataset.modelId || 0);
        this.modelName = this.$el.dataset.modelName || "";
        this.models = parseJsonAttr(this.$el, "data-models", []);
      },
      async onBrandChange() {
        this.modelId = 0;
        this.models = [];
        if (!this.categoryId || !this.brandId) return;
        try {
          const res = await fetch(
            `/api/catalog/categories/${this.categoryId}/brands/${this.brandId}/models`
          );
          if (!res.ok) return;
          const data = await res.json();
          this.models = (data || []).map((m) => ({
            id: m.id ?? m.Id,
            name: m.name ?? m.Name,
          }));
        } catch {
          this.models = [];
        }
      },
      onModelChange() {
        const found = this.models.find((m) => Number(m.id) === Number(this.modelId));
        if (found) this.modelName = found.name;
      },
    }));

    Alpine.data("ilanVerSale", () => ({
      cityId: 0,
      districtId: 0,
      primaryIntent: "satilik",
      rent: false,
      districts: [],
      init() {
        this.cityId = Number(this.$el.dataset.cityId || 0);
        this.districtId = Number(this.$el.dataset.districtId || 0);
        this.primaryIntent = this.$el.dataset.primaryIntent || "satilik";
        this.rent = this.$el.dataset.rent === "1";
        this.districts = parseJsonAttr(this.$el, "data-districts", []);
      },
      async onCityChange() {
        this.districtId = 0;
        this.districts = [];
        if (!this.cityId) return;
        try {
          const res = await fetch(`/api/locations/cities/${this.cityId}/districts`);
          if (!res.ok) return;
          const data = await res.json();
          this.districts = (data || []).map((d) => ({
            id: d.id ?? d.Id,
            name: d.name ?? d.Name,
          }));
        } catch {
          this.districts = [];
        }
      },
    }));

    Alpine.data("ilanVerImages", () => ({
      urls: [""],
      get canRemove() {
        return this.urls.length > 1;
      },
      get canAdd() {
        return this.urls.length < 8;
      },
      init() {
        const parsed = parseJsonAttr(this.$el, "data-urls", [""]);
        this.urls = Array.isArray(parsed) && parsed.length ? parsed : [""];
      },
      add() {
        if (this.urls.length < 8) this.urls.push("");
      },
      removeAt(index) {
        if (this.urls.length <= 1) return;
        this.urls.splice(index, 1);
      },
      labelFor(index) {
        return Number(index) === 0 ? "Kapak görseli (URL)" : "Görsel " + (Number(index) + 1);
      },
      inputId(index) {
        return "img-" + index;
      },
      hasPreview(index) {
        return !!String(this.urls[index] || "").trim();
      },
    }));
  });

  document.addEventListener("DOMContentLoaded", () => {
    trackRecent();
    const boot = document.getElementById("toast")?.getAttribute("data-boot-toast");
    if (boot) toast(boot);
    document.querySelectorAll("input[data-password-rules]").forEach((input) => {
      if (input instanceof HTMLInputElement) {
        syncPasswordRules(input, { showFail: input.dataset.touched === "1" });
        syncPasswordMatch(input.closest("form"), {
          showFail: input.closest("form")?.querySelector("input[data-password-match]")?.dataset.touched === "1",
        });
      }
    });
  });

  window.AP = { toast, STORAGE_RECENT };
})();
