(() => {
  "use strict";

  const STORAGE_RECENT = "ap:recent";

  const toast = (message, durationMs = 2800) => {
    const el = document.getElementById("toast");
    if (!el || !message) return;
    el.hidden = false;
    el.textContent = String(message);
    clearTimeout(toast._t);
    toast._t = setTimeout(() => {
      el.hidden = true;
      el.textContent = "";
    }, durationMs);
  };

  const VERIFY_COOLDOWN_SEC = 60;

  const setVerifyBtnState = (btn, { busy = false, disabled = false, label } = {}) => {
    if (!(btn instanceof HTMLButtonElement)) return;
    btn.disabled = disabled || busy;
    btn.setAttribute("aria-busy", busy ? "true" : "false");
    btn.classList.toggle("is-loading", busy);
    btn.classList.toggle("is-cooldown", disabled && !busy);
    if (typeof label === "string") btn.textContent = label;
  };

  const startVerifyCooldown = (btn, seconds = VERIFY_COOLDOWN_SEC) => {
    if (!(btn instanceof HTMLButtonElement)) return;
    const idle = btn.getAttribute("data-label-idle") || "Doğrulama e-postası gönder";
    const template = btn.getAttribute("data-label-cooldown") || "Tekrar gönder ({s}s)";
    let left = Math.max(0, Math.floor(seconds));
    clearInterval(btn._cooldownTimer);
    const tick = () => {
      if (left <= 0) {
        clearInterval(btn._cooldownTimer);
        setVerifyBtnState(btn, { busy: false, disabled: false, label: idle });
        return;
      }
      setVerifyBtnState(btn, {
        busy: false,
        disabled: true,
        label: template.replace("{s}", String(left)),
      });
      left -= 1;
    };
    tick();
    btn._cooldownTimer = setInterval(tick, 1000);
  };

  const initVerifyBanner = () => {
    const form = document.querySelector("[data-verify-resend-form]");
    const btn = document.querySelector("[data-verify-resend-btn]");
    if (!(form instanceof HTMLFormElement) || !(btn instanceof HTMLButtonElement)) return;

    form.addEventListener("submit", () => {
      if (btn.disabled && btn.getAttribute("aria-busy") !== "true") return;
      const loading = btn.getAttribute("data-label-loading") || "Gönderiliyor…";
      setVerifyBtnState(btn, { busy: true, disabled: true, label: loading });
    });

    if (document.querySelector("[data-verify-banner][data-verify-sent]")) {
      startVerifyCooldown(btn, VERIFY_COOLDOWN_SEC);
    }
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

    Alpine.data("accountMenu", () => ({
      open: false,
      menuId: "",
      triggerId: "",
      activeIndex: -1,
      panelTop: "0px",
      panelLeft: "0px",
      panelWidth: "264px",
      _onReposition: null,
      init() {
        const id = "am-" + Math.random().toString(36).slice(2, 9);
        this.menuId = id;
        this.triggerId = id + "-btn";
        this._onReposition = () => {
          if (!this.open) return;
          this.placePanel();
        };
        window.addEventListener("resize", this._onReposition);
        window.addEventListener("scroll", this._onReposition, true);
      },
      destroy() {
        window.removeEventListener("resize", this._onReposition);
        window.removeEventListener("scroll", this._onReposition, true);
      },
      get openAria() {
        return this.open ? "true" : "false";
      },
      get rootClass() {
        return this.open ? "is-open" : "";
      },
      get panelStyle() {
        return (
          "top:" +
          this.panelTop +
          ";left:" +
          this.panelLeft +
          ";width:" +
          this.panelWidth
        );
      },
      items() {
        return Array.from(this.$root.querySelectorAll('[role="menuitem"]'));
      },
      placePanel() {
        const trigger = this.$refs.trigger;
        if (!trigger) return;
        const rect = trigger.getBoundingClientRect();
        const width = Math.min(264, window.innerWidth - 24);
        let left = rect.right - width;
        if (left < 12) left = 12;
        if (left + width > window.innerWidth - 12) {
          left = Math.max(12, window.innerWidth - width - 12);
        }
        this.panelTop = Math.round(rect.bottom + 8) + "px";
        this.panelLeft = Math.round(left) + "px";
        this.panelWidth = Math.round(width) + "px";
      },
      toggle() {
        if (this.open) {
          this.close();
          return;
        }
        this.open = true;
        this.$nextTick(() => this.placePanel());
      },
      openMenu(index) {
        this.open = true;
        this.$nextTick(() => {
          this.placePanel();
          this.focusItem(index);
        });
      },
      close() {
        this.open = false;
        this.activeIndex = -1;
      },
      focusItem(index) {
        const list = this.items();
        if (!list.length) return;
        const i = ((index % list.length) + list.length) % list.length;
        this.activeIndex = i;
        list[i].focus();
      },
      onKey(event) {
        const key = event.key;
        if (key === "Escape") {
          if (!this.open) return;
          event.preventDefault();
          this.close();
          this.$refs.trigger && this.$refs.trigger.focus();
          return;
        }

        if (!this.open) {
          if (key === "ArrowDown" || key === "ArrowUp") {
            event.preventDefault();
            this.openMenu(key === "ArrowUp" ? -1 : 0);
          }
          return;
        }

        if (key === "ArrowDown") {
          event.preventDefault();
          this.focusItem(this.activeIndex + 1);
          return;
        }
        if (key === "ArrowUp") {
          event.preventDefault();
          this.focusItem(this.activeIndex - 1);
          return;
        }
        if (key === "Home") {
          event.preventDefault();
          this.focusItem(0);
          return;
        }
        if (key === "End") {
          event.preventDefault();
          this.focusItem(-1);
          return;
        }
        if (key === "Tab") {
          this.close();
        }
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

    function maskTrPhone(raw) {
      let digits = String(raw || "").replace(/\D/g, "");
      if (digits.length >= 12 && digits.startsWith("90")) digits = digits.slice(2);
      if (digits.length > 10) digits = digits.slice(-10);
      if (digits.length < 4) return "••••";
      if (digits.length === 10) {
        return digits[0] + "•• ••• •• " + digits.slice(8);
      }
      return "••••" + digits.slice(-4);
    }

    /** Format TR mobile as "532 123 45 67" while typing (CSP-safe input mask). */
    function formatTrPhoneInput(raw) {
      let digits = String(raw || "").replace(/\D/g, "");
      if (digits.startsWith("90") && digits.length > 10) digits = digits.slice(2);
      if (digits.startsWith("0")) digits = digits.slice(1);
      digits = digits.slice(0, 10);
      const parts = [];
      if (digits.length > 0) parts.push(digits.slice(0, 3));
      if (digits.length > 3) parts.push(digits.slice(3, 6));
      if (digits.length > 6) parts.push(digits.slice(6, 8));
      if (digits.length > 8) parts.push(digits.slice(8, 10));
      return parts.join(" ");
    }

    Alpine.data("phoneVerifyModal", () => ({
      step: "phone",
      phone: "",
      serverPhone: "",
      _maskedFromServer: "",
      otp: "",
      error: "",
      info: "",
      devCode: "",
      busy: false,
      cooldown: 0,
      sendUrl: "",
      verifyUrl: "",
      trapOpen: true,
      _timer: null,

      get canSubmit() {
        if (this.step === "phone") {
          return String(this.phone || "").replace(/\D/g, "").length >= 10;
        }
        return /^\d{6}$/.test(String(this.otp || "").trim());
      },

      get submitDisabled() {
        return this.busy || !this.canSubmit;
      },

      get resendDisabled() {
        return this.busy || this.cooldown > 0;
      },

      get showInfo() {
        return !!(this.info && !this.error);
      },

      get busyAria() {
        return this.busy ? "true" : "false";
      },

      get submitLabel() {
        return this.step === "phone" ? "WhatsApp’a kod gönder" : "Doğrula";
      },

      get ctaLabel() {
        if (this.busy) {
          return this.step === "phone" ? "Gönderiliyor…" : "Doğrulanıyor…";
        }
        return this.submitLabel;
      },

      get headerDesc() {
        if (this.step === "otp") {
          return (
            "WhatsApp’tan gelen 6 haneli kodu " +
            this.maskedPhone +
            " numarasına gönderildi."
          );
        }
        return "İlan yayımlamak için cep numaranı WhatsApp ile doğrula.";
      },

      get resendLabel() {
        return this.cooldown > 0
          ? "Tekrar gönder (" + this.cooldown + "s)"
          : "Tekrar gönder";
      },

      get maskedPhone() {
        if (this._maskedFromServer) return this._maskedFromServer;
        return maskTrPhone(this.serverPhone || this.phone);
      },

      init() {
        this.phone = formatTrPhoneInput(this.$el.dataset.phone || "");
        this.sendUrl = this.$el.dataset.sendUrl || "";
        this.verifyUrl = this.$el.dataset.verifyUrl || "";
        this.$nextTick(() => {
          document.getElementById("phone-verify-input")?.focus();
        });
      },

      onPhoneInput(event) {
        const el = event && event.target;
        if (!el) return;
        const formatted = formatTrPhoneInput(el.value);
        this.phone = formatted;
        if (el.value !== formatted) {
          el.value = formatted;
        }
      },

      onOtpInput(event) {
        const el = event && event.target;
        if (!el) return;
        const digits = String(el.value || "")
          .replace(/\D/g, "")
          .slice(0, 6);
        this.otp = digits;
        if (el.value !== digits) {
          el.value = digits;
        }
      },

      editPhone() {
        this.step = "phone";
        this.otp = "";
        this.error = "";
        this.info = "";
        this.devCode = "";
        this.serverPhone = "";
        this._maskedFromServer = "";
        this.$nextTick(() => {
          document.getElementById("phone-verify-input")?.focus();
        });
      },

      startCooldown(seconds) {
        this.cooldown = seconds;
        if (this._timer) clearInterval(this._timer);
        this._timer = setInterval(() => {
          if (this.cooldown <= 1) {
            this.cooldown = 0;
            clearInterval(this._timer);
            this._timer = null;
            return;
          }
          this.cooldown -= 1;
        }, 1000);
      },

      antiforgeryToken() {
        return (
          this.$el.querySelector('input[name="__RequestVerificationToken"]')?.value || ""
        );
      },

      async post(url, fields) {
        const token = this.antiforgeryToken();
        const fd = new FormData();
        fd.append("__RequestVerificationToken", token);
        Object.keys(fields).forEach((key) => {
          if (fields[key] != null) fd.append(key, fields[key]);
        });
        const res = await fetch(url, {
          method: "POST",
          body: fd,
          headers: {
            RequestVerificationToken: token,
            Accept: "application/json",
            "X-Requested-With": "XMLHttpRequest",
          },
          credentials: "same-origin",
        });
        let data = null;
        try {
          data = await res.json();
        } catch {
          data = null;
        }
        return { res, data };
      },

      async onSubmit() {
        if (this.busy || !this.canSubmit) return;
        if (this.step === "phone") {
          await this.sendCode();
          return;
        }
        await this.verifyCode();
      },

      async resend() {
        if (this.busy || this.cooldown > 0) return;
        await this.sendCode();
      },

      async sendCode() {
        this.busy = true;
        this.error = "";
        this.info = "";
        this.devCode = "";
        try {
          const { res, data } = await this.post(this.sendUrl, { phone: this.phone });
          if (res.status === 429 || res.status === 503) {
            this.error = "Çok fazla deneme. Biraz sonra tekrar dene.";
            return;
          }
          if (data?.alreadyVerified) {
            window.location.reload();
            return;
          }
          if (!res.ok || !data?.ok) {
            this.error = data?.error || "Kod gönderilemedi.";
            return;
          }
          this.serverPhone = this.phone;
          this._maskedFromServer = data.maskedPhone || maskTrPhone(this.phone);
          this.step = "otp";
          this.info = data.message || "Kod WhatsApp’a gönderildi.";
          this.devCode = data.devCode || "";
          this.startCooldown(60);
          this.$nextTick(() => {
            document.getElementById("phone-otp-input")?.focus();
          });
        } catch {
          this.error = "Bağlantı hatası. Tekrar dene.";
        } finally {
          this.busy = false;
        }
      },

      async verifyCode() {
        this.busy = true;
        this.error = "";
        this.info = "";
        try {
          const { res, data } = await this.post(this.verifyUrl, {
            phone: this.serverPhone || this.phone,
            otpCode: this.otp,
          });
          if (res.status === 429 || res.status === 503) {
            this.error = "Çok fazla deneme. Biraz sonra tekrar dene.";
            return;
          }
          if (data?.alreadyVerified || (data?.ok && data?.reload)) {
            window.location.reload();
            return;
          }
          if (!res.ok || !data?.ok) {
            this.error = data?.error || "Doğrulama başarısız.";
            return;
          }
          window.location.reload();
        } catch {
          this.error = "Bağlantı hatası. Tekrar dene.";
        } finally {
          this.busy = false;
        }
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

    Alpine.data("ilanVerSale", () => ({
      cityId: 0,
      districtId: 0,
      neighborhoodId: 0,
      primaryIntent: "satilik",
      rent: false,
      bothSaleRent: false,
      sellerType: "",
      districts: [],
      neighborhoods: [],
      init() {
        this.cityId = Number(this.$el.dataset.cityId || 0);
        this.districtId = Number(this.$el.dataset.districtId || 0);
        this.neighborhoodId = Number(this.$el.dataset.neighborhoodId || 0);
        this.primaryIntent = this.$el.dataset.primaryIntent || "satilik";
        this.rent = this.$el.dataset.rent === "1";
        this.bothSaleRent = this.$el.dataset.bothSaleRent === "1";
        this.sellerType = this.$el.dataset.sellerType || "";
        this.districts = parseJsonAttr(this.$el, "data-districts", []);
        this.neighborhoods = parseJsonAttr(this.$el, "data-neighborhoods", []);
      },
      onIntentsChange() {
        const form = this.$el;
        const boxes = form.querySelectorAll('input[name="intents"]');
        let hasSale = false;
        let hasRent = false;
        boxes.forEach((cb) => {
          if (cb.value === "satilik" && cb.checked) hasSale = true;
          if (cb.value === "kiralik" && cb.checked) hasRent = true;
        });
        this.rent = hasRent;
        this.bothSaleRent = hasSale && hasRent;
      },
      async onCityChange() {
        this.districtId = 0;
        this.neighborhoodId = 0;
        this.districts = [];
        this.neighborhoods = [];
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
      async onDistrictChange() {
        this.neighborhoodId = 0;
        this.neighborhoods = [];
        if (!this.districtId) return;
        try {
          const res = await fetch(`/api/locations/districts/${this.districtId}/neighborhoods`);
          if (!res.ok) return;
          const data = await res.json();
          this.neighborhoods = (data || []).map((n) => ({
            id: n.id ?? n.Id,
            name: n.displayName ?? n.DisplayName ?? n.name ?? n.Name,
          }));
        } catch {
          this.neighborhoods = [];
        }
      },
    }));

    // CSP-friendly shell for /ilanlar mobile filter drawer (no inline object/ternary expressions).
    Alpine.data("listShell", () => ({
      filtersOpen: false,
      init() {
        this.$watch("filtersOpen", (open) => {
          document.body.classList.toggle("no-scroll", !!open);
        });
      },
      destroy() {
        document.body.classList.remove("no-scroll");
      },
      get layoutClass() {
        return this.filtersOpen ? "filters-open" : "";
      },
      get filtersExpandedAria() {
        return this.filtersOpen ? "true" : "false";
      },
      openFilters() {
        this.filtersOpen = true;
      },
      closeFilters() {
        this.filtersOpen = false;
      },
    }));

    Alpine.data("listLocationFilter", () => ({
      open: null,
      citySearch: "",
      districtSearch: "",
      cities: [],
      districts: [],
      selectedCityIds: [],
      selectedDistrictIds: [],
      loadingDistricts: false,
      init() {
        this.cities = parseJsonAttr(this.$el, "data-cities", []);
        this.districts = parseJsonAttr(this.$el, "data-districts", []);
        this.selectedCityIds = (parseJsonAttr(this.$el, "data-selected-cities", []) || []).map(Number);
        this.selectedDistrictIds = (parseJsonAttr(this.$el, "data-selected-districts", []) || []).map(Number);
      },
      get cityOpen() {
        return this.open === "city";
      },
      get districtOpen() {
        return this.open === "district";
      },
      get cityOpenAria() {
        return this.cityOpen ? "true" : "false";
      },
      get districtOpenAria() {
        return this.districtOpen ? "true" : "false";
      },
      get hasCitySearch() {
        return this.citySearch.length > 0;
      },
      get hasDistrictSearch() {
        return this.districtSearch.length > 0;
      },
      get hasSelectedCities() {
        return this.selectedCityIds.length > 0;
      },
      get districtTriggerDisabled() {
        return this.loadingDistricts || !this.hasSelectedCities;
      },
      get citiesView() {
        const q = this.citySearch.trim().toLocaleLowerCase("tr");
        const selected = new Set(this.selectedCityIds.map(Number));
        const list = !q
          ? this.cities
          : this.cities.filter((c) => String(c.name || "").toLocaleLowerCase("tr").includes(q));
        return list.map((c) => {
          const id = Number(c.id);
          const on = selected.has(id);
          return {
            id: c.id,
            name: c.name,
            selected: on,
            itemClass: on ? "is-selected" : "",
          };
        });
      },
      get citiesEmpty() {
        return this.citiesView.length === 0;
      },
      get districtsView() {
        const q = this.districtSearch.trim().toLocaleLowerCase("tr");
        const selected = new Set(this.selectedDistrictIds.map(Number));
        const showCity = this.selectedCityIds.length > 1;
        const list = !q
          ? this.districts
          : this.districts.filter((d) => {
              const label = `${d.name || ""} ${d.cityName || ""}`;
              return label.toLocaleLowerCase("tr").includes(q);
            });
        return list.map((d) => {
          const id = Number(d.id);
          const on = selected.has(id);
          const cityName = d.cityName || "";
          return {
            id: d.id,
            name: d.name,
            selected: on,
            itemClass: on ? "is-selected" : "",
            showCity: showCity && !!cityName,
            citySuffix: cityName ? ` (${cityName})` : "",
          };
        });
      },
      get districtsEmpty() {
        return this.districtsView.length === 0;
      },
      get cityTriggerLabel() {
        const n = this.selectedCityIds.length;
        if (n === 0) return "Türkiye geneli";
        const first = this.cities.find((c) => Number(c.id) === Number(this.selectedCityIds[0]));
        const name = first?.name || "İl";
        return n === 1 ? name : `${name} +${n - 1}`;
      },
      get districtTriggerLabel() {
        const n = this.selectedDistrictIds.length;
        if (n === 0) return "Tüm ilçeler";
        const first = this.districts.find((d) => Number(d.id) === Number(this.selectedDistrictIds[0]));
        const name = first?.name || "İlçe";
        return n === 1 ? name : `${name} +${n - 1}`;
      },
      get districtButtonLabel() {
        if (this.selectedCityIds.length === 0) return "Önce il seçin";
        return this.loadingDistricts ? "Yükleniyor…" : this.districtTriggerLabel;
      },
      toggleCityOpen() {
        this.open = this.open === "city" ? null : "city";
        this.citySearch = "";
        if (this.open === "city") {
          this.$nextTick(() => this.$refs.citySearch?.focus());
        }
      },
      toggleDistrictOpen() {
        if (this.selectedCityIds.length === 0) return;
        this.open = this.open === "district" ? null : "district";
        this.districtSearch = "";
        if (this.open === "district") {
          this.$nextTick(() => this.$refs.districtSearch?.focus());
        }
      },
      closeCityPanel() {
        if (this.open === "city") this.open = null;
      },
      closeDistrictPanel() {
        if (this.open === "district") this.open = null;
      },
      escapeCityPanel() {
        this.closeCityPanel();
        const trigger = this.$refs.cityTrigger;
        if (trigger) trigger.focus();
      },
      escapeDistrictPanel() {
        this.closeDistrictPanel();
        const trigger = this.$refs.districtTrigger;
        if (trigger) trigger.focus();
      },
      isCitySelected(id) {
        return this.selectedCityIds.some((x) => Number(x) === Number(id));
      },
      isDistrictSelected(id) {
        return this.selectedDistrictIds.some((x) => Number(x) === Number(id));
      },
      async toggleCity(event) {
        const num = Number(
          (event && event.currentTarget && event.currentTarget.getAttribute("data-id")) || 0
        );
        if (!num) return;
        if (this.isCitySelected(num)) {
          this.selectedCityIds = this.selectedCityIds.filter((x) => Number(x) !== num);
        } else {
          this.selectedCityIds = [...this.selectedCityIds, num];
        }
        await this.reloadDistricts();
      },
      toggleDistrict(event) {
        const num = Number(
          (event && event.currentTarget && event.currentTarget.getAttribute("data-id")) || 0
        );
        if (!num) return;
        if (this.isDistrictSelected(num)) {
          this.selectedDistrictIds = this.selectedDistrictIds.filter((x) => Number(x) !== num);
        } else {
          this.selectedDistrictIds = [...this.selectedDistrictIds, num];
        }
      },
      async reloadDistricts() {
        const prev = new Set(this.selectedDistrictIds.map(Number));
        this.districts = [];
        this.selectedDistrictIds = [];
        if (this.selectedCityIds.length === 0) {
          if (this.open === "district") this.open = null;
          return;
        }
        this.loadingDistricts = true;
        try {
          const qs = this.selectedCityIds.join(",");
          const res = await fetch(`/api/locations/districts?cityIds=${encodeURIComponent(qs)}`);
          if (!res.ok) return;
          const data = await res.json();
          this.districts = (data || []).map((d) => ({
            id: d.id ?? d.Id,
            name: d.name ?? d.Name,
            cityId: d.cityId ?? d.CityId,
            cityName: d.cityName ?? d.CityName,
          }));
          this.selectedDistrictIds = this.districts
            .map((d) => Number(d.id))
            .filter((id) => prev.has(id));
        } catch {
          this.districts = [];
        } finally {
          this.loadingDistricts = false;
        }
      },
      clearCitySearch() {
        this.citySearch = "";
        this.$refs.citySearch?.focus();
      },
      clearDistrictSearch() {
        this.districtSearch = "";
        this.$refs.districtSearch?.focus();
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
      get hasAnyUrl() {
        return this.urls.some((u) => !!String(u || "").trim());
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

  // —— İlanlar (liste) sayfası geliştirmeleri ——
  const SAVED_SEARCHES = "ap:saved-searches";
  const RANGE_PAIRS = [
    ["fiyatMin", "fiyatMax"],
    ["yilMin", "yilMax"],
    ["saatMin", "saatMax"],
    ["hpMin", "hpMax"],
    ["kgMin", "kgMax"],
    ["tonMin", "tonMax"],
  ];

  const validateRanges = (form) => {
    let ok = true;
    RANGE_PAIRS.forEach(([minName, maxName]) => {
      const minEl = form.querySelector(`[name="${minName}"]`);
      const maxEl = form.querySelector(`[name="${maxName}"]`);
      if (!minEl || !maxEl) return;
      if (typeof maxEl.setCustomValidity === "function") maxEl.setCustomValidity("");
      maxEl.classList?.remove("is-invalid");
      const minV = String(minEl.value || "").trim();
      const maxV = String(maxEl.value || "").trim();
      if (minV !== "" && maxV !== "" && Number(minV) > Number(maxV)) {
        if (typeof maxEl.setCustomValidity === "function") {
          maxEl.setCustomValidity("Maksimum değer minimumdan küçük olamaz.");
        }
        maxEl.classList?.add("is-invalid");
        ok = false;
      }
    });
    return ok;
  };

  const announceResults = () => {
    const live = document.getElementById("list-live");
    const title = document.getElementById("list-title");
    if (!live || !title) return;
    const text = title.textContent.replace(/\s+/g, " ").trim();
    live.textContent = "";
    requestAnimationFrame(() => {
      live.textContent = text;
    });
  };

  const readSavedSearches = () => {
    try {
      const raw = JSON.parse(localStorage.getItem(SAVED_SEARCHES) || "[]");
      return Array.isArray(raw) ? raw : [];
    } catch {
      return [];
    }
  };

  const writeSavedSearches = (list) => {
    try {
      localStorage.setItem(SAVED_SEARCHES, JSON.stringify(list.slice(0, 50)));
    } catch {
      /* storage dolu / kapalı — sessizce geç */
    }
  };

  const initSaveSearch = () => {
    const btn = document.querySelector("[data-save-search]");
    if (!(btn instanceof HTMLElement) || btn.dataset.bound === "1") return;
    btn.dataset.bound = "1";
    const textEl = btn.querySelector("[data-save-search-text]");
    const url = btn.getAttribute("data-url") || location.pathname + location.search;
    const label = (btn.getAttribute("data-label") || "Arama").trim();

    const setState = (on) => {
      btn.classList.toggle("is-saved", on);
      btn.setAttribute("aria-pressed", on ? "true" : "false");
      if (textEl) textEl.textContent = on ? "Arama Kaydedildi" : "Aramayı Kaydet";
    };

    setState(readSavedSearches().some((s) => s && s.url === url));

    btn.addEventListener("click", () => {
      const list = readSavedSearches();
      const idx = list.findIndex((s) => s && s.url === url);
      if (idx >= 0) {
        list.splice(idx, 1);
        writeSavedSearches(list);
        setState(false);
        toast("Arama kayıtlardan çıkarıldı.");
      } else {
        list.unshift({ url, label, savedAt: Date.now() });
        writeSavedSearches(list);
        setState(true);
        toast("Arama kaydedildi. Favori aramalarından ulaşabilirsin.");
      }
    });
  };

  const initListEnhancements = () => {
    // Sonuç bölgesinin tamamı yeniden bağlanmasın diye body seviyesinde bir kez bağlanır.
    if (document.body.dataset.listBound === "1") {
      initSaveSearch();
      return;
    }
    document.body.dataset.listBound = "1";
    initSaveSearch();

    document.body.addEventListener("htmx:beforeRequest", (evt) => {
      const elt = evt.detail && evt.detail.elt;
      const form = elt
        ? elt.id === "list-filter-form"
          ? elt
          : elt.id === "sort-select"
            ? document.getElementById("list-filter-form")
            : null
        : null;
      if (form && !validateRanges(form)) {
        evt.preventDefault();
        const bad = form.querySelector(".is-invalid");
        if (bad) {
          bad.reportValidity?.();
          bad.focus();
        }
        return;
      }
      const bar = document.getElementById("route-progress");
      if (bar) bar.classList.add("is-active");
      if (evt.detail && evt.detail.target && evt.detail.target.id === "list-shell") {
        document.getElementById("list-results")?.setAttribute("aria-busy", "true");
      }
    });

    document.body.addEventListener("htmx:afterRequest", () => {
      const bar = document.getElementById("route-progress");
      if (bar) {
        clearTimeout(initListEnhancements._t);
        initListEnhancements._t = setTimeout(() => bar.classList.remove("is-active"), 150);
      }
      document.getElementById("list-results")?.setAttribute("aria-busy", "false");
    });

    document.body.addEventListener("htmx:afterSwap", (evt) => {
      if (!evt.detail || !evt.detail.target || evt.detail.target.id !== "list-shell") return;
      announceResults();
      initSaveSearch();
      const title = document.getElementById("list-title");
      if (title) {
        try {
          title.focus({ preventScroll: false });
        } catch {
          title.focus();
        }
      }
    });

    // Tablo satırının tamamını tıklanabilir yap (Fitts) — gerçek kontroller korunur.
    document.body.addEventListener("click", (evt) => {
      const t = evt.target;
      if (!(t instanceof Element)) return;
      const row = t.closest(".classified-table-row[data-href]");
      if (!row) return;
      if (t.closest("a, button, input, label, select, textarea")) return;
      if (window.getSelection && String(window.getSelection()).length > 0) return;
      const href = row.getAttribute("data-href");
      if (!href) return;
      if (evt.metaKey || evt.ctrlKey || evt.button === 1) {
        window.open(href, "_blank", "noopener");
      } else {
        window.location.href = href;
      }
    });
  };

  document.addEventListener("DOMContentLoaded", () => {
    trackRecent();
    initVerifyBanner();
    initListEnhancements();
    const boot = document.getElementById("toast")?.getAttribute("data-boot-toast");
    if (boot) {
      // Auth / e-posta bildirimleri daha uzun kalsın — kullanıcı okusun.
      const longer = /mail|e-posta|doğrulama|onay/i.test(boot);
      toast(boot, longer ? 6000 : 2800);
    }
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
