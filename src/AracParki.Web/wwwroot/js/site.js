(() => {
  "use strict";

  const STORAGE_RECENT = "ap:recent";

  /** Parents that can host the lazy-load spinner (must be position:relative capable). */
  const IMG_SHELL_SEL = [
    ".img-shell",
    ".listing-media",
    ".classified-thumb",
    ".gallery-main-wrap",
    ".gallery-thumbs button",
    ".admin-listing-thumb",
    ".account-result-media",
    ".wizard-gallery-item",
  ].join(",");

  /**
   * Bind a content <img> to show .img-shell.is-loading spinner until load/error.
   * Skips LCP heroes (fetchpriority=high), crop UI, and upload placeholders.
   * Safe to call again after src/srcset changes (re-shows spinner).
   */
  const bindImageSpinner = (img) => {
    if (!(img instanceof HTMLImageElement)) return;
    if (img.hasAttribute("data-no-spinner")) return;
    if (img.getAttribute("fetchpriority") === "high") return;
    if (img.closest(".wizard-gallery-item.is-uploading")) return;
    if (img.hasAttribute("x-ref") && img.getAttribute("x-ref") === "cropImage") return;

    const hasSrc = Boolean(img.getAttribute("src") || img.getAttribute("srcset") || img.currentSrc);
    if (!hasSrc) return;

    const shell = img.closest(IMG_SHELL_SEL);
    if (!(shell instanceof HTMLElement)) return;
    shell.classList.add("img-shell");

    const setLoading = (on) => {
      shell.classList.toggle("is-loading", on);
      if (on) img.setAttribute("aria-busy", "true");
      else img.removeAttribute("aria-busy");
    };

    const settle = () => {
      if (img.complete && img.naturalWidth > 0) {
        setLoading(false);
        return true;
      }
      if (img.complete && img.naturalWidth === 0 && img.currentSrc) {
        // Broken image (error already fired or cached failure)
        setLoading(false);
        return true;
      }
      return false;
    };

    const start = () => {
      if (settle()) return;
      setLoading(true);
    };

    if (img.dataset.spinnerBound !== "1") {
      img.addEventListener("load", () => setLoading(false));
      img.addEventListener("error", () => setLoading(false));
      img.dataset.spinnerBound = "1";
    }

    start();
  };

  const initLazyImageSpinners = (root = document) => {
    if (!(root instanceof Document || root instanceof Element)) return;
    root.querySelectorAll("img[loading], img.gallery-main").forEach((el) => bindImageSpinner(el));
  };

  const toast = (message, durationMs = 2800) => {
    const text = String(message || "").trim();
    if (!text) return;

    if (typeof window.Toastify === "function") {
      window.Toastify({
        text,
        duration: durationMs,
        gravity: "top",
        position: "right",
        stopOnFocus: true,
        close: false,
        style: {
          background: "var(--ink, #0c0c0c)",
          color: "#fff",
          borderRadius: "8px",
          fontFamily: "var(--font-sans, Manrope, system-ui, sans-serif)",
          fontSize: "13px",
          fontWeight: "700",
          boxShadow: "0 12px 32px rgba(0, 0, 0, 0.28)",
          padding: "12px 16px",
        },
      }).showToast();
      return;
    }

    const el = document.getElementById("toast");
    if (!el) return;
    el.hidden = false;
    el.textContent = text;
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
      const token = document.querySelector('meta[name="request-verification-token"]')?.getAttribute("content") || "";
      const res = await fetch(`/ilan/${encodeURIComponent(adNo)}/telefon`, {
        method: "POST",
        headers: {
          Accept: "application/json",
          RequestVerificationToken: token,
        },
      });
      if (res.status === 400) {
        toast("İstek doğrulanamadı. Sayfayı yenileyip tekrar deneyin.");
        return;
      }
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

    // Simple open/close popover (listing actions, reject reason, etc.).
    Alpine.data("accountPopover", () => ({
      open: false,
      get openAria() {
        return this.open ? "true" : "false";
      },
      toggle() {
        this.open = !this.open;
      },
      close() {
        this.open = false;
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
      intents: [
        { id: "satilik", name: "Satılık" },
        { id: "kiralik", name: "Kiralık" },
      ],
      conditions: [
        { id: "used", name: "İkinci el" },
        { id: "new", name: "Sıfır" },
      ],
      intent: "",
      condition: "",
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
        if (this.intentLabel) parts.push(this.intentLabel);
        if (this.conditionLabel) parts.push(this.conditionLabel);
        if (this.groupName) parts.push(this.groupName);
        if (this.categoryName) parts.push(this.categoryName);
        if (this.brandName) parts.push(this.brandName);
        if (this.modelName) parts.push(this.modelName);
        if (this.modelYear) parts.push(String(this.modelYear));
        return parts.join(" › ");
      },
      get intentLabel() {
        const id = String(this.intent || "");
        const hit = this.intents.find(function (t) {
          return t.id === id;
        });
        return hit ? hit.name : "";
      },
      get conditionLabel() {
        const id = String(this.condition || "");
        const hit = this.conditions.find(function (c) {
          return c.id === id;
        });
        return hit ? hit.name : "";
      },
      get hasIntent() {
        return this.intent === "satilik" || this.intent === "kiralik";
      },
      get hasCondition() {
        return this.condition === "used" || this.condition === "new";
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
      get modelsEmpty() {
        return !this.modelsLoading && this.models.length === 0;
      },
      get brandsReady() {
        return !this.brandsLoading;
      },
      get modelsReady() {
        return !this.modelsLoading;
      },
      get brandsColClass() {
        return this.brandsLoading ? "is-loading" : "";
      },
      get modelsColClass() {
        return this.modelsLoading ? "is-loading" : "";
      },
      get brandsBusy() {
        return this.brandsLoading ? "true" : "false";
      },
      get modelsBusy() {
        return this.modelsLoading ? "true" : "false";
      },
      get canPickYear() {
        return (
          this.brandId > 0 &&
          !this.modelsLoading &&
          String(this.modelName || "").trim().length > 0
        );
      },
      get canContinue() {
        return (
          this.hasIntent &&
          this.hasCondition &&
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
      get customModelNeedsName() {
        return this.showCustomModel && String(this.modelName || "").trim().length === 0;
      },
      get customModelHasName() {
        return this.showCustomModel && String(this.modelName || "").trim().length > 0;
      },
      get intentsView() {
        const selected = String(this.intent || "");
        return this.intents.map(function (t) {
          const on = t.id === selected;
          return {
            id: t.id,
            name: t.name,
            itemClass: on ? "is-selected" : "",
            ariaSelected: on ? "true" : "false",
          };
        });
      },
      get conditionsView() {
        const selected = String(this.condition || "");
        return this.conditions.map(function (c) {
          const on = c.id === selected;
          return {
            id: c.id,
            name: c.name,
            itemClass: on ? "is-selected" : "",
            ariaSelected: on ? "true" : "false",
          };
        });
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

      pickIntent(event) {
        const id = String((event && event.currentTarget && event.currentTarget.getAttribute("data-id")) || "");
        if (id !== "satilik" && id !== "kiralik") return;
        this.intent = id;
        this.scrollBoardOnClick();
      },
      pickCondition(event) {
        const id = String((event && event.currentTarget && event.currentTarget.getAttribute("data-id")) || "");
        if (id !== "used" && id !== "new") return;
        this.condition = id;
        this.scrollBoardOnClick();
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
        this.scrollBoardOnClick();
      },

      init() {
        this.groups = parseJsonAttr(this.$el, "data-groups", []);
        const yMax = new Date().getFullYear() + 1;
        const list = [];
        for (let y = yMax; y >= 1950; y -= 1) {
          list.push({ id: y });
        }
        this.years = list;

        const intentRaw = String(this.$el.dataset.intent || "").trim();
        this.intent = intentRaw === "satilik" || intentRaw === "kiralik" ? intentRaw : "";
        const conditionRaw = String(this.$el.dataset.condition || "").trim();
        this.condition = conditionRaw === "used" || conditionRaw === "new" ? conditionRaw : "";
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
        this.intent = "";
        this.condition = "";
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
        this.brandsLoading = false;
        this.modelsLoading = false;
        const board = this.cascadeBoard();
        if (board) board.scrollLeft = 0;
      },
      selectGroup(g) {
        this.groupId = Number(g.id);
        this.groupName = g.name;
        this.categories = g.categories || [];
        this.categoryId = 0;
        this.categoryName = "";
        this.clearBrandDown();
        this.scrollBoardOnClick();
      },
      selectCategory(c) {
        this.categoryId = Number(c.id);
        this.categoryName = c.name;
        this.clearBrandDown();
        this.brandsLoading = true;
        this.scrollBoardOnClick();
        const self = this;
        this.loadBrands(false).then(function () {
          self.scrollBoardOnClick();
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
        this.modelsLoading = true;
        this.scrollBoardOnClick();
        await this.loadModels(false);
        this.scrollBoardOnClick();
      },
      selectModel(m) {
        this.modelId = Number(m.id);
        this.modelName = m.name;
        this.showCustomModel = false;
        this.modelYear = 0;
        this.scrollBoardOnClick();
      },
      selectCustomModel() {
        this.modelId = 0;
        this.modelName = "";
        this.showCustomModel = true;
        this.modelYear = 0;
        this.scrollBoardOnClick();
        this.$nextTick(function () {
          const el = document.getElementById("cascade-model-custom");
          if (!el) return;
          // preventScroll: input lives outside the board; don't yank the page/board.
          try {
            el.focus({ preventScroll: true });
          } catch {
            el.focus();
          }
        });
      },
      onCustomModelInput(event) {
        // Input is bound one-way (:value + @input); this is the single writer.
        if (event && event.target) {
          this.modelName = event.target.value;
        }
        this.modelId = 0;
        const name = String(this.modelName || "").trim();
        if (!name) {
          this.modelYear = 0;
          return;
        }
        this.scrollBoardOnClick();
      },
      cascadeBoard() {
        return window.document.getElementsByClassName("cascade-board")[0];
      },
      preferReducedMotion() {
        return (
          typeof window.matchMedia === "function" &&
          window.matchMedia("(prefers-reduced-motion: reduce)").matches
        );
      },
      scrollBoardOnClick() {
        const board = this.cascadeBoard();
        setTimeout(() => {
          if (board) {
            board.scrollTo({ left: board.scrollWidth, behavior: "smooth" });
          }
        }, 1000);
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
        this.brandsLoading = false;
        this.modelsLoading = false;
      },
      async loadBrands(restore) {
        if (!this.categoryId) return;
        this.brandsLoading = true;
        this.brands = [];
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
        this.models = [];
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
      districtsLoading: false,
      neighborhoodsLoading: false,
      locationStatus: "",
      priceRaw: 0,
      priceHint: "",
      accountPhone: "",
      corporatePhones: {},
      selectedCorporateId: "",
      contactPhoneSource: "account",
      _districtAbort: null,
      _neighborhoodAbort: null,
      get hasLocationStatus() {
        return !!this.locationStatus;
      },
      get hasPriceHint() {
        return !!this.priceHint;
      },
      get isCorporateSeller() {
        return !!this.selectedCorporateId;
      },
      get corporatePhone() {
        const row = this.corporatePhones[this.selectedCorporateId];
        return row && row.phone ? String(row.phone) : "";
      },
      get corporateName() {
        const row = this.corporatePhones[this.selectedCorporateId];
        return row && row.name ? String(row.name) : "Bayi";
      },
      get showPhoneChoices() {
        return this.isCorporateSeller && !!this.accountPhone && !!this.corporatePhone;
      },
      get showSinglePhone() {
        return !!this.activePhone && !this.showPhoneChoices;
      },
      get showPhoneFallback() {
        return !this.accountPhone && !this.corporatePhone;
      },
      get isAccountPhoneSelected() {
        return this.contactPhoneSource === "account";
      },
      get isCorporatePhoneSelected() {
        return this.contactPhoneSource === "corporate";
      },
      get accountPhoneLabel() {
        return this.accountPhone || "";
      },
      get corporatePhoneLabel() {
        if (!this.corporatePhone) return "";
        return this.corporateName
          ? this.corporateName + " · " + this.corporatePhone
          : this.corporatePhone;
      },
      get activePhone() {
        if (this.contactPhoneSource === "corporate" && this.corporatePhone) {
          return this.corporatePhone;
        }
        return this.accountPhone || this.corporatePhone || "";
      },
      get activePhoneLabel() {
        return this.activePhone || "—";
      },
      init() {
        const cityId = Number(this.$el.dataset.cityId || 0) || 0;
        const districtId = Number(this.$el.dataset.districtId || 0) || 0;
        const neighborhoodId = Number(this.$el.dataset.neighborhoodId || 0) || 0;
        const districts = parseJsonAttr(this.$el, "data-districts", []);
        const neighborhoods = parseJsonAttr(this.$el, "data-neighborhoods", []);
        const initialPrice = Number(this.$el.dataset.price || 0) || 0;
        const initialCurrency = String(this.$el.dataset.currency || "TRY");
        this.accountPhone = String(this.$el.dataset.accountPhone || "").trim();
        this.selectedCorporateId = String(this.$el.dataset.corporateId || "").trim();
        this.contactPhoneSource = String(this.$el.dataset.contactPhoneSource || "account").trim() || "account";

        const phoneRows = parseJsonAttr(this.$el, "data-corporate-phones", []);
        const map = {};
        if (Array.isArray(phoneRows)) {
          for (const row of phoneRows) {
            if (!row || row.id == null) continue;
            map[String(row.id)] = {
              phone: String(row.phone || "").trim(),
              name: String(row.name || "").trim(),
            };
          }
        }
        this.corporatePhones = map;
        this.syncContactPhoneSource();

        // disabled select alanları POST'a girmez; gönderimden hemen önce aç.
        this.$el.addEventListener("submit", () => {
          if (this.$refs.district) this.$refs.district.disabled = false;
          if (this.$refs.neighborhood) this.$refs.neighborhood.disabled = false;
          this.syncContactPhoneSource();
          this.syncPriceHidden();
        });

        this.$nextTick(() => {
          if (this.$refs.currency) {
            this.$refs.currency.value = initialCurrency;
          }
          this.setPriceRaw(initialPrice);

          if (this.$refs.city) {
            this.$refs.city.value = cityId > 0 ? String(cityId) : "";
            this.$refs.city.addEventListener("change", (e) => this.onCityChange(e));
          }
          if (this.$refs.district) {
            this.$refs.district.addEventListener("change", (e) => this.onDistrictChange(e));
          }

          this.fillSelect(
            this.$refs.district,
            districts,
            districtId,
            cityId > 0 ? "İlçe seç" : "Önce il seç",
            cityId <= 0
          );
          this.fillSelect(
            this.$refs.neighborhood,
            neighborhoods,
            neighborhoodId,
            districtId > 0 ? "Mahalle seç" : "Önce ilçe seç",
            districtId <= 0
          );
        });
      },
      syncContactPhoneSource() {
        if (!this.showPhoneChoices) {
          this.contactPhoneSource = "account";
        } else if (this.contactPhoneSource !== "corporate") {
          this.contactPhoneSource = "account";
        }
        if (this.$refs.contactPhoneSource) {
          this.$refs.contactPhoneSource.value = this.contactPhoneSource;
        }
      },
      onSellerChange(event) {
        this.selectedCorporateId = String(event && event.target ? event.target.value : "").trim();
        if (this.showPhoneChoices) {
          this.contactPhoneSource = "corporate";
        } else {
          this.contactPhoneSource = "account";
        }
        this.syncContactPhoneSource();
      },
      onContactPhoneSourceChange(event) {
        this.contactPhoneSource = String(event && event.target ? event.target.value : "account");
        this.syncContactPhoneSource();
      },
      formatN0(value) {
        const n = Math.floor(Number(value) || 0);
        if (n <= 0) return "";
        return n.toLocaleString("tr-TR", { maximumFractionDigits: 0 });
      },
      setPriceRaw(value) {
        const n = Math.floor(Number(value) || 0);
        this.priceRaw = n > 0 ? n : 0;
        if (this.$refs.priceDisplay) {
          this.$refs.priceDisplay.value = this.formatN0(this.priceRaw);
        }
        this.syncPriceHidden();
        this.updatePriceHint();
      },
      syncPriceHidden() {
        if (this.$refs.priceRaw) {
          this.$refs.priceRaw.value = this.priceRaw > 0 ? String(this.priceRaw) : "";
        }
      },
      updatePriceHint() {
        if (this.priceRaw <= 0) {
          this.priceHint = "";
          return;
        }
        const label =
          this.$refs.currency && this.$refs.currency.value
            ? this.$refs.currency.options[this.$refs.currency.selectedIndex].text
            : "TL";
        this.priceHint = this.formatN0(this.priceRaw) + " " + label;
      },
      onPriceInput(event) {
        const el = event && event.target;
        if (!el) return;
        const digits = String(el.value || "").replace(/\D/g, "");
        const n = digits ? Number(digits) : 0;
        this.priceRaw = Number.isFinite(n) && n > 0 ? Math.floor(n) : 0;
        el.value = this.formatN0(this.priceRaw);
        this.syncPriceHidden();
        this.updatePriceHint();
      },
      onPriceBlur() {
        if (this.$refs.priceDisplay) {
          this.$refs.priceDisplay.value = this.formatN0(this.priceRaw);
        }
        this.syncPriceHidden();
        this.updatePriceHint();
      },
      onCurrencyChange() {
        this.updatePriceHint();
      },
      setBusy(select, busy) {
        if (!select) return;
        select.setAttribute("aria-busy", busy ? "true" : "false");
        select.classList.toggle("is-loading", !!busy);
      },
      fillSelect(select, items, selectedId, placeholder, disabled) {
        if (!select) return;
        const list = Array.isArray(items) ? items : [];
        const selected = Number(selectedId) || 0;
        const wasDisabled = select.disabled;
        select.innerHTML = "";

        const empty = document.createElement("option");
        empty.value = "";
        empty.textContent = placeholder;
        select.appendChild(empty);

        for (const item of list) {
          const id = Number(item.id ?? item.Id);
          if (!id) continue;
          const opt = document.createElement("option");
          opt.value = String(id);
          opt.textContent = String(
            item.name ?? item.Name ?? item.displayName ?? item.DisplayName ?? id
          );
          if (id === selected) opt.selected = true;
          select.appendChild(opt);
        }

        select.disabled = !!disabled;
        this.setBusy(select, String(placeholder).includes("Yükleniyor"));

        if (wasDisabled && !disabled && list.length > 0) {
          this.$nextTick(() => select.focus({ preventScroll: true }));
        }
      },
      async onCityChange(event) {
        const cityId = Number(event?.target?.value || 0) || 0;
        this._districtAbort?.abort();
        this._neighborhoodAbort?.abort();
        this.locationStatus = "";
        this.fillSelect(this.$refs.district, [], 0, cityId ? "Yükleniyor…" : "Önce il seç", true);
        this.fillSelect(this.$refs.neighborhood, [], 0, "Önce ilçe seç", true);
        if (!cityId) return;

        this.districtsLoading = true;
        this.locationStatus = "İlçeler yükleniyor…";
        this._districtAbort = new AbortController();
        try {
          const res = await fetch(`/api/locations/cities/${cityId}/districts`, {
            headers: { Accept: "application/json" },
            signal: this._districtAbort.signal,
          });
          if (!res.ok) throw new Error("districts");
          const data = await res.json();
          const items = (Array.isArray(data) ? data : []).map((d) => ({
            id: Number(d.id ?? d.Id),
            name: d.name ?? d.Name,
          }));
          this.fillSelect(
            this.$refs.district,
            items,
            0,
            items.length ? "İlçe seç" : "İlçe bulunamadı",
            items.length === 0
          );
          this.locationStatus = items.length ? "" : "Bu il için ilçe bulunamadı.";
        } catch (err) {
          if (err?.name === "AbortError") return;
          this.fillSelect(this.$refs.district, [], 0, "İlçeler yüklenemedi", true);
          this.locationStatus = "İlçeler yüklenemedi. Tekrar dene.";
        } finally {
          this.districtsLoading = false;
          this.setBusy(this.$refs.district, false);
        }
      },
      async onDistrictChange(event) {
        const districtId = Number(event?.target?.value || 0) || 0;
        this._neighborhoodAbort?.abort();
        this.locationStatus = "";
        this.fillSelect(
          this.$refs.neighborhood,
          [],
          0,
          districtId ? "Yükleniyor…" : "Önce ilçe seç",
          true
        );
        if (!districtId) return;

        this.neighborhoodsLoading = true;
        this.locationStatus = "Mahalleler yükleniyor…";
        this._neighborhoodAbort = new AbortController();
        try {
          const res = await fetch(`/api/locations/districts/${districtId}/neighborhoods`, {
            headers: { Accept: "application/json" },
            signal: this._neighborhoodAbort.signal,
          });
          if (!res.ok) throw new Error("neighborhoods");
          const data = await res.json();
          const items = (Array.isArray(data) ? data : []).map((n) => ({
            id: Number(n.id ?? n.Id),
            name: n.displayName ?? n.DisplayName ?? n.name ?? n.Name,
          }));
          this.fillSelect(
            this.$refs.neighborhood,
            items,
            0,
            items.length ? "Mahalle seç" : "Mahalle bulunamadı",
            false
          );
          this.locationStatus = "";
        } catch (err) {
          if (err?.name === "AbortError") return;
          this.fillSelect(this.$refs.neighborhood, [], 0, "Mahalleler yüklenemedi", false);
          this.locationStatus = "Mahalleler yüklenemedi; istersen boş bırakabilirsin.";
        } finally {
          this.neighborhoodsLoading = false;
          this.setBusy(this.$refs.neighborhood, false);
        }
      },
    }));

    // CSP-friendly shell for /ilanlar mobile filter drawer (no inline object/ternary expressions).
    Alpine.data("listShell", () => ({
      filtersOpen: false,
      viewType: "list",
      init() {
        try {
          const stored = localStorage.getItem("ap-list-view");
          if (stored === "grid" || stored === "list") {
            this.viewType = stored;
          }
        } catch {
          /* ignore */
        }
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
      get resultsViewClass() {
        return this.viewType === "grid" ? "is-view-grid" : "is-view-list";
      },
      get listViewBtnClass() {
        return this.viewType === "list" ? "is-active" : "";
      },
      get gridViewBtnClass() {
        return this.viewType === "grid" ? "is-active" : "";
      },
      get listViewPressed() {
        return this.viewType === "list" ? "true" : "false";
      },
      get gridViewPressed() {
        return this.viewType === "grid" ? "true" : "false";
      },
      setListView() {
        this.viewType = "list";
        try {
          localStorage.setItem("ap-list-view", "list");
        } catch {
          /* ignore */
        }
      },
      setGridView() {
        this.viewType = "grid";
        try {
          localStorage.setItem("ap-list-view", "grid");
        } catch {
          /* ignore */
        }
      },
      openFilters() {
        this.filtersOpen = true;
      },
      closeFilters() {
        this.filtersOpen = false;
      },
    }));

    // Account / admin shell — mobile off-canvas nav (desktop sticky sidebar).
    Alpine.data("accountShell", () => ({
      navOpen: false,
      init() {
        this.$watch("navOpen", (open) => {
          document.body.classList.toggle("no-scroll", !!open);
        });
        const mq = window.matchMedia("(min-width: 980px)");
        const onChange = () => {
          if (mq.matches) this.closeNav();
        };
        if (typeof mq.addEventListener === "function") {
          mq.addEventListener("change", onChange);
        } else {
          mq.addListener(onChange);
        }
      },
      destroy() {
        document.body.classList.remove("no-scroll");
      },
      get layoutClass() {
        return this.navOpen ? "is-nav-open" : "";
      },
      get navExpandedAria() {
        return this.navOpen ? "true" : "false";
      },
      get asideHiddenAria() {
        if (typeof window !== "undefined" && window.matchMedia("(min-width: 980px)").matches) {
          return "false";
        }
        return this.navOpen ? "false" : "true";
      },
      get asideInert() {
        if (typeof window !== "undefined" && window.matchMedia("(min-width: 980px)").matches) {
          return false;
        }
        return !this.navOpen;
      },
      openNav() {
        this.navOpen = true;
        this.$nextTick(() => {
          document.querySelector("#account-nav-drawer .account-nav-link.is-current")
            ?.focus?.();
        });
      },
      closeNav() {
        this.navOpen = false;
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
      onCitySearchInput(event) {
        this.citySearch = String((event && event.target && event.target.value) || "");
      },
      onDistrictSearchInput(event) {
        this.districtSearch = String((event && event.target && event.target.value) || "");
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

    // CSP Alpine: kurumsal hesap — şirket türüne göre alanlar + il/ilçe.
    Alpine.data("corpForm", () => ({
      companyType: "sahis",
      init() {
        const initial = String(this.$el.dataset.companyType || "sahis");
        this.companyType = initial;
      },
      get isCapital() {
        return this.companyType === "limited" || this.companyType === "anonim";
      },
      get isSahis() {
        return this.companyType === "sahis";
      },
      get isDiger() {
        return this.companyType === "diger";
      },
      get showMersis() {
        return this.isCapital || this.isDiger;
      },
      get mersisRequired() {
        return this.isCapital;
      },
      get showTradeRegistry() {
        return this.isCapital || this.isDiger;
      },
      get capitalDocsRequired() {
        return this.isCapital;
      },
      get taxLabel() {
        return this.isSahis ? "TC kimlik no" : "Vergi kimlik no (VKN)";
      },
      get taxHint() {
        if (this.isSahis) return "Şahıs şirketinde 11 haneli TCKN gir.";
        if (this.isCapital) return "Vergi levhasındaki 10 haneli VKN.";
        return "VKN (10 hane) veya TCKN (11 hane).";
      },
      get taxMaxLength() {
        return this.isSahis ? 11 : 11;
      },
      get taxPlaceholder() {
        return this.isSahis ? "11 haneli TCKN" : "10 haneli VKN";
      },
      get companyHint() {
        if (this.isCapital) {
          return "Limited/Anonim şirketlerde MERSİS, ticaret sicil gazetesi ve faaliyet belgesi zorunludur.";
        }
        if (this.isSahis) {
          return "Şahıs şirketinde vergi levhası ve imza beyannamesi zorunludur. Ticaret sicil ve faaliyet belgesi isteğe bağlı yüklenebilir.";
        }
        return "Kooperatif / diğer tüzel kişilerde vergi levhası ve imza sirküleri zorunludur; MERSİS, ticaret sicil ve faaliyet belgesi isteğe bağlıdır.";
      },
      onCompanyTypeChange(event) {
        this.companyType = String(event?.target?.value || "sahis");
        if (this.isSahis) {
          const mersis = this.$el.querySelector("#mersisNo");
          const registry = this.$el.querySelector("#tradeRegistryNo");
          if (mersis) mersis.value = "";
          if (registry) registry.value = "";
        }
      },
      async onCityChange(event) {
        const cityId = Number(event?.target?.value || 0) || 0;
        const district = this.$refs.district;
        if (!district) return;

        this._districtAbort?.abort();
        district.innerHTML = "";
        district.appendChild(new Option(cityId ? "Yükleniyor…" : "Önce il seç", ""));
        district.disabled = true;
        if (!cityId) return;

        this._districtAbort = new AbortController();
        try {
          const res = await fetch(`/api/locations/cities/${cityId}/districts`, {
            headers: { Accept: "application/json" },
            signal: this._districtAbort.signal,
          });
          if (!res.ok) throw new Error("districts");
          const data = await res.json();
          const items = (Array.isArray(data) ? data : []).map((d) => ({
            id: Number(d.id ?? d.Id),
            name: d.name ?? d.Name,
          }));
          district.innerHTML = "";
          district.appendChild(new Option(items.length ? "İlçe seç" : "İlçe bulunamadı", ""));
          for (const item of items) {
            district.appendChild(new Option(item.name, String(item.id)));
          }
          district.disabled = items.length === 0;
        } catch (err) {
          if (err?.name === "AbortError") return;
          district.innerHTML = "";
          district.appendChild(new Option("İlçeler yüklenemedi", ""));
          district.disabled = true;
        }
      },
    }));

    Alpine.data("ilanVerMachine", () => ({
      hoursUnknown: false,
      get hoursRequired() {
        return !this.hoursUnknown;
      },
      toggleHoursUnknown(event) {
        this.hoursUnknown = !!(event && event.target && event.target.checked);
      },
      init() {
        this.hoursUnknown = String(this.$el.dataset.hoursUnknown || "") === "true";
        const invalid = this.$el.querySelector(".is-invalid, [aria-invalid='true']");
        if (invalid && typeof invalid.scrollIntoView === "function") {
          invalid.scrollIntoView({ behavior: "smooth", block: "center" });
        }
      },
    }));

    // CSP Alpine: description character counter for step 2 (Quill or textarea).
    Alpine.data("ilanVerCharCount", () => ({
      count: 0,
      max: 8000,
      init() {
        this.max = Number(this.$el.dataset.max || 8000);
        this.sync();
        this.$el.addEventListener("quill-change", () => this.sync());
      },
      onInput() {
        this.sync();
      },
      sync() {
        const fromQuill = this.$el.dataset.textLength;
        if (fromQuill != null && fromQuill !== "") {
          this.count = Number(fromQuill) || 0;
          return;
        }
        const el = this.$refs.input;
        const value = el ? String(el.value || "") : "";
        this.count = value.length;
      },
    }));

    // CSP Alpine: draft resume modal (explicit continue vs new).
    Alpine.data("draftResumeModal", () => ({
      // Trap is always open while the modal is rendered.
    }));

    // CSP Alpine: multi-file upload with square crop (Cropper.js) → UploadJson.
    Alpine.data("ilanVerUploader", () => ({
      dragging: false,
      queue: [],
      cropQueue: [],
      cropOpen: false,
      cropBusy: false,
      cropError: "",
      cropFileName: "",
      get cropSubmitLabel() {
        return this.cropBusy ? "Hazırlanıyor…" : "Kırp ve yükle";
      },
      currentCount: 0,
      maxCount: 30,
      maxBytes: 10 * 1024 * 1024,
      uploadUrl: "",
      _nextId: 1,
      _token: "",
      _cropper: null,
      _cropObjectUrl: "",
      _cropOutputPx: 1600,
      get dropzoneClass() {
        return this.dragging ? "is-dragging" : "";
      },
      get busy() {
        return (
          this.cropOpen ||
          this.cropQueue.length > 0 ||
          this.queue.some((item) => item.state === "pending" || item.state === "uploading")
        );
      },
      get busyAria() {
        return this.busy ? "true" : "false";
      },
      init() {
        this.uploadUrl = String(this.$el.dataset.uploadUrl || "");
        this.maxCount = Number(this.$el.dataset.maxCount || 30);
        this.maxBytes = Number(this.$el.dataset.maxBytes || 10485760);
        this.currentCount = Number(this.$el.dataset.currentCount || 0);
        const tokenInput = this.$el.querySelector('input[name="__RequestVerificationToken"]');
        this._token = tokenInput ? tokenInput.value : "";
        this.$watch("cropOpen", (open) => this.setCropBodyLock(open));
      },
      onDragEnter() {
        this.dragging = true;
      },
      onDragOver() {
        this.dragging = true;
      },
      onDragLeave(event) {
        const related = event && event.relatedTarget;
        if (related && this.$el.contains(related)) return;
        this.dragging = false;
      },
      onDrop(event) {
        this.dragging = false;
        const files = event && event.dataTransfer && event.dataTransfer.files;
        if (files && files.length) this.enqueueFiles(files);
      },
      onFileChange() {
        const input = this.$refs.file;
        if (input && input.files && input.files.length) {
          this.enqueueFiles(input.files);
          input.value = "";
        }
      },
      enqueueFiles(fileList) {
        const inFlight =
          this.queue.filter((q) => q.state !== "done").length +
          this.cropQueue.length +
          (this.cropOpen ? 1 : 0);
        const remaining = this.maxCount - this.currentCount - inFlight;
        if (remaining <= 0) return;
        const files = Array.from(fileList).slice(0, remaining);
        for (const file of files) {
          this.cropQueue.push(file);
        }
        this.processCropQueue();
      },
      async processCropQueue() {
        if (this.cropOpen || this._cropProcessing) return;
        this._cropProcessing = true;
        try {
          while (this.cropQueue.length > 0) {
            if (this.currentCount >= this.maxCount) {
              this.cropQueue = [];
              break;
            }
            const file = this.cropQueue.shift();
            await this.openCropper(file);
          }
        } finally {
          this._cropProcessing = false;
        }
      },
      openCropper(file) {
        const parent = this;
        return new Promise((resolve) => {
          parent._cropResolve = resolve;
          parent.cropError = "";
          parent.cropBusy = false;
          parent.cropFileName = file.name || "görsel";
          parent._pendingCropFile = file;

          const type = String(file.type || "").toLowerCase();
          const allowed = ["image/jpeg", "image/png", "image/webp", "image/heic", "image/heif"];
          if (file.size > parent.maxBytes) {
            parent.cropError = "Dosya en fazla 10 MB olabilir.";
            parent.cropOpen = true;
            parent.$nextTick(() => parent.focusCropPanel());
            return;
          }
          if (type && allowed.indexOf(type) < 0) {
            parent.cropError = "Desteklenmeyen format.";
            parent.cropOpen = true;
            parent.$nextTick(() => parent.focusCropPanel());
            return;
          }
          if (typeof window.Cropper !== "function") {
            // Library not loaded yet / failed — upload without interactive crop.
            parent.finishCropWithFile(file);
            return;
          }

          parent.destroyCropper();
          parent._cropObjectUrl = URL.createObjectURL(file);
          parent.cropOpen = true;

          // x-show toggles display:none → Cropper must init only after the stage
          // has a real layout box (nextTick alone is not enough on first open).
          parent.$nextTick(() => {
            requestAnimationFrame(() => {
              requestAnimationFrame(() => parent.mountCropper(file));
            });
          });
        });
      },
      mountCropper(file) {
        const parent = this;
        const img = parent.$refs.cropImage;
        if (!(img instanceof HTMLImageElement)) {
          parent.finishCropWithFile(file);
          return;
        }

        let started = false;
        const start = () => {
          if (started) return;
          started = true;
          parent.destroyCropperInstanceOnly();

          const stage = img.parentElement;
          if (!(stage instanceof HTMLElement) || stage.clientWidth < 32 || stage.clientHeight < 32) {
            // Layout still not ready — one more frame, then fall back to raw upload.
            requestAnimationFrame(() => {
              if (stage instanceof HTMLElement && stage.clientWidth >= 32 && stage.clientHeight >= 32) {
                started = false;
                start();
                return;
              }
              parent.finishCropWithFile(file);
            });
            return;
          }

          parent._cropper = new window.Cropper(img, {
            aspectRatio: 1,
            viewMode: 1,
            dragMode: "move",
            autoCropArea: 1,
            responsive: true,
            background: false,
            guides: true,
            center: true,
            highlight: false,
            cropBoxMovable: true,
            cropBoxResizable: true,
            toggleDragModeOnDblclick: false,
            checkOrientation: true,
            ready() {
              try {
                parent._cropper?.resize();
              } catch {
                /* ignore */
              }
              parent.focusCropPanel();
            },
          });
        };

        img.onload = start;
        img.onerror = () => {
          parent.cropError =
            "Bu görsel tarayıcıda açılamadı. Kırpmadan yüklemek için “Kırpmadan yükle”ye bas.";
          parent.focusCropPanel();
        };
        img.src = parent._cropObjectUrl;
        // Cached images may already be complete (onload may not fire again).
        if (img.complete && img.naturalWidth > 0) {
          start();
        }
      },
      focusCropPanel() {
        const panel = this.$refs.cropPanel;
        if (panel instanceof HTMLElement) panel.focus({ preventScroll: true });
      },
      setCropBodyLock(locked) {
        document.documentElement.classList.toggle("wizard-crop-open", !!locked);
      },
      destroyCropperInstanceOnly() {
        if (this._cropper) {
          try {
            this._cropper.destroy();
          } catch {
            /* ignore */
          }
          this._cropper = null;
        }
      },
      destroyCropper() {
        this.destroyCropperInstanceOnly();
        if (this._cropObjectUrl) {
          URL.revokeObjectURL(this._cropObjectUrl);
          this._cropObjectUrl = "";
        }
        const img = this.$refs.cropImage;
        if (img instanceof HTMLImageElement) {
          img.onload = null;
          img.onerror = null;
          img.removeAttribute("src");
        }
      },
      rotateCropLeft() {
        if (this._cropper) this._cropper.rotate(-90);
      },
      rotateCropRight() {
        if (this._cropper) this._cropper.rotate(90);
      },
      resetCrop() {
        if (this._cropper) {
          this._cropper.reset();
          try {
            this._cropper.resize();
          } catch {
            /* ignore */
          }
        }
      },
      cancelCrop() {
        // Skip this file entirely.
        this.closeCropModal();
        if (typeof this._cropResolve === "function") {
          this._cropResolve();
          this._cropResolve = null;
        }
      },
      uploadWithoutCrop() {
        const file = this._pendingCropFile;
        if (!file) {
          this.cancelCrop();
          return;
        }
        this.finishCropWithFile(file);
      },
      finishCropWithFile(file) {
        this.closeCropModal();
        this.queue.push(this.createQueueItem(file));
        this.processQueue();
        if (typeof this._cropResolve === "function") {
          this._cropResolve();
          this._cropResolve = null;
        }
      },
      closeCropModal() {
        this.destroyCropper();
        this.cropOpen = false;
        this.cropBusy = false;
        this.cropError = "";
        this._pendingCropFile = null;
        this.setCropBodyLock(false);
      },
      confirmCrop() {
        const parent = this;
        if (parent.cropBusy) return;
        if (parent.cropError && !parent._cropper) {
          // Allow fallback button path via uploadWithoutCrop.
          return;
        }
        if (!parent._cropper) {
          parent.cropError = "Kırpıcı hazır değil. “Kırpmadan yükle” veya başka bir dosya dene.";
          return;
        }

        parent.cropBusy = true;
        parent.cropError = "";

        const canvas = parent._cropper.getCroppedCanvas({
          width: parent._cropOutputPx,
          height: parent._cropOutputPx,
          imageSmoothingEnabled: true,
          imageSmoothingQuality: "high",
          fillColor: "#fff",
        });

        if (!canvas) {
          parent.cropBusy = false;
          parent.cropError = "Kırpma başarısız. Tekrar dene.";
          return;
        }

        const baseName = String(parent._pendingCropFile && parent._pendingCropFile.name
          ? parent._pendingCropFile.name
          : "gorsel")
          .replace(/\.[^.]+$/, "");
        const outName = baseName + "-kare.jpg";

        canvas.toBlob(
          (blob) => {
            parent.cropBusy = false;
            if (!blob) {
              parent.cropError = "Görsel oluşturulamadı.";
              return;
            }
            if (blob.size > parent.maxBytes) {
              parent.cropError = "Kırpılmış dosya 10 MB sınırını aşıyor. Daha küçük alan seç.";
              return;
            }
            const file = new File([blob], outName, {
              type: "image/jpeg",
              lastModified: Date.now(),
            });
            parent.finishCropWithFile(file);
          },
          "image/jpeg",
          0.92
        );
      },
      createQueueItem(file) {
        const parent = this;
        const id = this._nextId++;
        const previewUrl = URL.createObjectURL(file);
        return {
          id,
          file,
          name: file.name || "görsel",
          previewUrl,
          state: "pending",
          progress: 0,
          error: "",
          get isVisible() {
            return this.state !== "done";
          },
          get showProgress() {
            return this.state === "pending" || this.state === "uploading";
          },
          get showError() {
            return this.state === "error";
          },
          get progressLabel() {
            if (this.state === "pending") return "Hazırlanıyor…";
            if (this.state === "error") return "Hata";
            if (this.progress <= 0) return "Yükleniyor…";
            return this.progress + "%";
          },
          get barStyle() {
            return "width:" + Math.max(0, Math.min(100, this.progress)) + "%";
          },
          retry() {
            this.state = "pending";
            this.progress = 0;
            this.error = "";
            parent.processQueue();
          },
          dismiss() {
            parent.dismissItem(this);
          },
        };
      },
      dismissItem(item) {
        if (item.previewUrl) URL.revokeObjectURL(item.previewUrl);
        this.queue = this.queue.filter((q) => q.id !== item.id);
      },
      async processQueue() {
        if (this._processing) return;
        this._processing = true;
        try {
          while (true) {
            const next = this.queue.find((i) => i.state === "pending");
            if (!next) break;
            if (this.currentCount >= this.maxCount) {
              next.state = "error";
              next.error = "En fazla " + this.maxCount + " görsel ekleyebilirsin.";
              continue;
            }
            await this.uploadOne(next);
          }
        } finally {
          this._processing = false;
        }
      },
      uploadOne(item) {
        const parent = this;
        return new Promise((resolve) => {
          if (item.file.size > parent.maxBytes) {
            item.state = "error";
            item.error = "Dosya en fazla 10 MB olabilir.";
            resolve();
            return;
          }
          const type = String(item.file.type || "").toLowerCase();
          const allowed = ["image/jpeg", "image/png", "image/webp", "image/heic", "image/heif"];
          if (type && allowed.indexOf(type) < 0) {
            item.state = "error";
            item.error = "Desteklenmeyen format.";
            resolve();
            return;
          }

          item.state = "uploading";
          item.progress = 0;
          const form = new FormData();
          form.append("file", item.file, item.file.name);

          const xhr = new XMLHttpRequest();
          xhr.open("POST", parent.uploadUrl);
          xhr.setRequestHeader("RequestVerificationToken", parent._token);
          xhr.setRequestHeader("X-Requested-With", "XMLHttpRequest");

          xhr.upload.onprogress = (event) => {
            if (!event.lengthComputable) return;
            item.progress = Math.round((event.loaded / event.total) * 100);
          };

          xhr.onload = () => {
            let payload = null;
            try {
              payload = JSON.parse(xhr.responseText || "{}");
            } catch {
              payload = null;
            }
            if (xhr.status >= 200 && xhr.status < 300 && payload && payload.ok) {
              item.progress = 100;
              parent.currentCount = Number(payload.count || parent.currentCount + 1);
              parent.appendGalleryItem(
                payload.deliveryUrl,
                parent.currentCount === 1,
                item.previewUrl
              );
              parent.appendHiddenUrl(payload.deliveryUrl);
              item.state = "done";
              parent.queue = parent.queue.filter((q) => q.id !== item.id);
            } else {
              item.state = "error";
              item.error =
                (payload && payload.error) ||
                (xhr.status === 429 ? "Çok fazla istek. Biraz sonra dene." : "Yükleme başarısız.");
            }
            resolve();
          };

          xhr.onerror = () => {
            item.state = "error";
            item.error = "Ağ hatası. Tekrar dene.";
            resolve();
          };

          xhr.send(form);
        });
      },
      appendHiddenUrl(url) {
        const wrap = document.getElementById("wizard-hidden-urls");
        if (!wrap || !url) return;
        const input = document.createElement("input");
        input.type = "hidden";
        input.name = "imageUrls";
        input.value = url;
        wrap.appendChild(input);
        const continueBtn = document.getElementById("wizard-images-continue");
        if (continueBtn) continueBtn.disabled = false;
      },
      appendGalleryItem(url, isCover, previewUrl) {
        const gallery = this.$refs.gallery;
        if (!gallery || !url) return;
        const index = gallery.querySelectorAll(".wizard-gallery-item:not(.is-uploading)").length;
        const figure = document.createElement("figure");
        figure.className = "wizard-gallery-item" + (index === 0 || isCover ? " is-cover" : "");
        figure.setAttribute("data-url", url);

        const img = document.createElement("img");
        img.alt = "";
        img.width = 480;
        img.height = 480;
        const localPreview = typeof previewUrl === "string" && previewUrl ? previewUrl : "";
        if (localPreview) {
          img.src = localPreview;
          const remote = new Image();
          remote.decoding = "async";
          remote.onload = () => {
            img.src = url;
            URL.revokeObjectURL(localPreview);
            bindImageSpinner(img);
          };
          remote.onerror = () => {
            img.src = url;
            URL.revokeObjectURL(localPreview);
            bindImageSpinner(img);
          };
          remote.src = url;
        } else {
          img.src = url;
          img.loading = "lazy";
          img.decoding = "async";
        }

        const cap = document.createElement("figcaption");
        cap.textContent = index === 0 ? "Kapak" : String(index + 1);

        const actions = document.createElement("div");
        actions.className = "wizard-gallery-actions";

        if (index > 0) {
          const coverForm = document.createElement("form");
          coverForm.method = "post";
          coverForm.action = "?handler=SetCover";
          coverForm.innerHTML =
            '<input type="hidden" name="__RequestVerificationToken" />' +
            '<input type="hidden" name="url" />' +
            '<button type="submit" class="wizard-gallery-action">Kapak yap</button>';
          coverForm.querySelector('input[name="__RequestVerificationToken"]').value = this._token;
          coverForm.querySelector('input[name="url"]').value = url;
          actions.appendChild(coverForm);
        }

        const removeForm = document.createElement("form");
        removeForm.method = "post";
        removeForm.action = "?handler=RemoveImage";
        removeForm.innerHTML =
          '<input type="hidden" name="__RequestVerificationToken" />' +
          '<input type="hidden" name="url" />' +
          '<button type="submit" class="wizard-gallery-action wizard-gallery-action--danger">Kaldır</button>';
        removeForm.querySelector('input[name="__RequestVerificationToken"]').value = this._token;
        removeForm.querySelector('input[name="url"]').value = url;
        actions.appendChild(removeForm);

        figure.appendChild(img);
        figure.appendChild(cap);
        figure.appendChild(actions);
        bindImageSpinner(img);

        const firstUploading = gallery.querySelector(".wizard-gallery-item.is-uploading");
        if (firstUploading) {
          gallery.insertBefore(figure, firstUploading);
        } else {
          gallery.appendChild(figure);
        }
      },
    }));

    // Legacy single-file dropzone (unused by step 4; kept for safety).
    Alpine.data("ilanVerDropzone", () => ({
      dragging: false,
      get dropzoneClass() {
        return this.dragging ? "is-dragging" : "";
      },
      onDragEnter() {
        this.dragging = true;
      },
      onDragOver() {
        this.dragging = true;
      },
      onDragLeave(event) {
        const related = event && event.relatedTarget;
        if (related && this.$el.contains(related)) return;
        this.dragging = false;
      },
      onDrop(event) {
        this.dragging = false;
      },
      onFileChange() {},
    }));

    // CSP Alpine: no method(args) / bracket access in markup — use item objects + getters.
    Alpine.data("ilanVerImages", () => ({
      items: [],
      _nextId: 1,
      get canRemove() {
        return this.items.length > 1;
      },
      get canAdd() {
        return this.items.length < 8;
      },
      get hasAnyUrl() {
        return this.items.some((item) => !!String(item.value || "").trim());
      },
      createItem(value) {
        const parent = this;
        const id = this._nextId++;
        return {
          id,
          value: String(value || ""),
          get label() {
            const index = parent.items.indexOf(this);
            return index === 0 ? "Kapak görseli (URL)" : "Görsel " + (index + 1);
          },
          get inputId() {
            return "img-" + this.id;
          },
          get hasPreview() {
            return !!String(this.value || "").trim();
          },
          remove() {
            parent.removeItem(this);
          },
        };
      },
      init() {
        const parsed = parseJsonAttr(this.$el, "data-urls", [""]);
        const urls = Array.isArray(parsed) && parsed.length ? parsed : [""];
        this.items = urls.map((url) => this.createItem(url));
      },
      add() {
        if (this.items.length < 8) this.items.push(this.createItem(""));
      },
      removeItem(item) {
        if (this.items.length <= 1) return;
        const index = this.items.indexOf(item);
        if (index >= 0) this.items.splice(index, 1);
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
      initLazyImageSpinners(evt.detail.target);
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

  const initQuillDescriptions = () => {
    if (typeof Quill === "undefined") return;

    document.querySelectorAll("[data-quill-description]").forEach((host) => {
      if (!(host instanceof HTMLElement) || host.dataset.quillReady === "1") return;

      const input = host.querySelector('input[name="description"], textarea[name="description"]');
      const editorEl = host.querySelector("[data-quill-editor]");
      if (!(input instanceof HTMLInputElement || input instanceof HTMLTextAreaElement)
          || !(editorEl instanceof HTMLElement)) {
        return;
      }

      const max = Number(host.dataset.max || 8000);
      const quill = new Quill(editorEl, {
        theme: "snow",
        placeholder: editorEl.getAttribute("data-placeholder") || "",
        modules: {
          toolbar: [
            ["bold", "italic", "underline"],
            [{ list: "ordered" }, { list: "bullet" }],
            ["link"],
            ["clean"],
          ],
        },
      });

      const initial = String(input.value || "").trim();
      if (initial) {
        if (initial.startsWith("<")) {
          quill.setContents(quill.clipboard.convert({ html: initial }), "silent");
        } else {
          quill.setText(initial, "silent");
        }
      }

      let applyingLimit = false;
      const plainLength = () => {
        const text = quill.getText();
        return text.endsWith("\n") ? text.length - 1 : text.length;
      };

      const sync = () => {
        const length = plainLength();
        const html = quill.getSemanticHTML();
        const empty = length === 0 || !String(html).replace(/<[^>]+>/g, "").replace(/&nbsp;/gi, " ").trim();
        input.value = empty ? "" : html;
        host.dataset.textLength = String(length);
        host.dispatchEvent(new CustomEvent("quill-change"));
      };

      quill.on("text-change", (_delta, _old, source) => {
        if (applyingLimit) return;
        if (source === "user" && plainLength() > max) {
          applyingLimit = true;
          quill.history.undo();
          applyingLimit = false;
        }
        sync();
      });

      const form = host.closest("form");
      if (form instanceof HTMLFormElement) {
        form.addEventListener("submit", () => {
          sync();
          if (!input.value) {
            input.setCustomValidity("Açıklama zorunlu.");
          } else {
            input.setCustomValidity("");
          }
        });
      }

      host.dataset.quillReady = "1";
      sync();
    });
  };

  const galleryFullUrl = (src) => {
    const raw = String(src || "").trim();
    if (!raw) return raw;
    try {
      const url = new URL(raw, window.location.origin);
      if (url.searchParams.has("v")) {
        url.searchParams.set("v", "lg");
      }
      return url.toString();
    } catch {
      return raw.replace(/([?&]v=)[^&]*/i, "$1lg");
    }
  };

  const initDetailGallery = () => {
    document.querySelectorAll("[data-detail-gallery]").forEach((host) => {
      if (!(host instanceof HTMLElement) || host.dataset.galleryReady === "1") return;
      const main = host.querySelector("[data-gallery-main]");
      const thumbs = [...host.querySelectorAll("[data-gallery-thumb]")];
      if (!(main instanceof HTMLImageElement) || thumbs.length === 0) return;

      const counter = host.querySelector("[data-gallery-counter]");
      const lgTrigger = host.querySelector("[data-gallery-lg-trigger]");
      const total = thumbs.length;
      let index = Math.max(
        0,
        thumbs.findIndex((t) => t.classList.contains("is-active"))
      );
      if (index < 0) index = 0;

      const items = thumbs.map((btn, i) => {
        const src = String(btn.getAttribute("data-src") || "").trim();
        const srcset = String(btn.getAttribute("data-srcset") || "").trim();
        const full = galleryFullUrl(src);
        return {
          src: full,
          thumb: src || full,
          srcset,
          size: "1600-1600",
          alt: main.alt || `Görsel ${i + 1}`,
          subHtml: `<div class="lg-sub-html-inner">${i + 1} / ${total}</div>`,
        };
      });

      const syncThumbs = (activeIndex) => {
        thumbs.forEach((btn, i) => {
          const on = i === activeIndex;
          btn.classList.toggle("is-active", on);
          btn.setAttribute("aria-current", on ? "true" : "false");
        });
        if (counter) counter.textContent = `${activeIndex + 1} / ${total} Fotoğraf`;
      };

      const setIndex = (next) => {
        if (total <= 0) return;
        index = ((next % total) + total) % total;
        const item = items[index];
        const src = item?.thumb || "";
        if (src) {
          // Browsers prefer srcset over src — clear/update or the first image sticks.
          if (item.srcset) {
            main.srcset = item.srcset;
          } else {
            main.removeAttribute("srcset");
          }
          main.src = src;
          main.dataset.gallerySrc = src;
          bindImageSpinner(main);
        }
        syncThumbs(index);
      };

      const step = (delta) => setIndex(index + delta);

      let lgInstance = null;
      const ensureLightGallery = () => {
        if (lgInstance) return lgInstance;
        if (typeof window.lightGallery !== "function") return null;
        const trigger = lgTrigger instanceof HTMLElement ? lgTrigger : host;
        const plugins = [];
        if (typeof window.lgThumbnail !== "undefined") plugins.push(window.lgThumbnail);
        if (typeof window.lgZoom !== "undefined") plugins.push(window.lgZoom);

        lgInstance = window.lightGallery(trigger, {
          dynamic: true,
          dynamicEl: items,
          plugins,
          speed: 400,
          download: false,
          counter: true,
          closable: true,
          escKey: true,
          keyPress: true,
          controls: true,
          mousewheel: true,
          getCaptionFromTitleOrAlt: true,
          mobileSettings: {
            controls: true,
            showCloseIcon: true,
            download: false,
          },
          // GPLv3 / open-source build — commercial production requires a paid key.
          // See https://www.lightgalleryjs.com/
        });

        trigger.addEventListener("lgAfterSlide", (event) => {
          const detail = event && event.detail;
          const nextIndex = detail && typeof detail.index === "number" ? detail.index : -1;
          if (nextIndex >= 0) setIndex(nextIndex);
        });

        return lgInstance;
      };

      const openLightboxAt = (at) => {
        const lg = ensureLightGallery();
        if (!lg || typeof lg.openGallery !== "function") return;
        const start = ((at % total) + total) % total;
        setIndex(start);
        lg.openGallery(start);
      };

      host.addEventListener("click", (e) => {
        const t = e.target;
        if (!(t instanceof Element)) return;
        if (t.closest("[data-gallery-prev]")) {
          step(-1);
          return;
        }
        if (t.closest("[data-gallery-next]")) {
          step(1);
          return;
        }
        if (t.closest("[data-gallery-open]")) {
          openLightboxAt(index);
          return;
        }
        const thumb = t.closest("[data-gallery-thumb]");
        if (thumb) {
          const i = Number(thumb.getAttribute("data-index"));
          if (Number.isFinite(i)) setIndex(i);
          return;
        }
        if (t.closest(".gallery-main-wrap") && !t.closest(".gallery-hit, .gallery-expand, [data-gallery-lg-trigger]")) {
          openLightboxAt(index);
        }
      });

      host.addEventListener("keydown", (e) => {
        if (!(e instanceof KeyboardEvent)) return;
        if (e.key !== "ArrowLeft" && e.key !== "ArrowRight") return;
        if (document.activeElement !== host && !host.contains(document.activeElement)) return;
        // Don't steal keys while lightGallery overlay is open.
        if (document.querySelector(".lg-container.lg-show")) return;
        step(e.key === "ArrowRight" ? 1 : -1);
        e.preventDefault();
      });

      setIndex(index);
      // Warm lightGallery after paint so first open is snappy.
      requestAnimationFrame(() => ensureLightGallery());
      host.dataset.galleryReady = "1";
    });
  };

  const initDetailTabs = () => {
    document.querySelectorAll("[data-detail-tabs]").forEach((root) => {
      if (!(root instanceof HTMLElement) || root.dataset.tabsReady === "1") return;
      const tabs = [...root.querySelectorAll("[data-detail-tab]")];
      const panels = [...root.querySelectorAll("[data-detail-panel]")];
      if (tabs.length === 0) return;

      const activate = (name) => {
        tabs.forEach((tab) => {
          const on = tab.getAttribute("data-detail-tab") === name;
          tab.classList.toggle("is-active", on);
          tab.setAttribute("aria-selected", on ? "true" : "false");
          tab.tabIndex = on ? 0 : -1;
        });
        panels.forEach((panel) => {
          const on = panel.getAttribute("data-detail-panel") === name;
          panel.classList.toggle("is-active", on);
          panel.toggleAttribute("hidden", !on);
        });
      };

      root.addEventListener("click", (e) => {
        const t = e.target;
        if (!(t instanceof Element)) return;
        const tab = t.closest("[data-detail-tab]");
        if (!(tab instanceof HTMLElement)) return;
        const name = tab.getAttribute("data-detail-tab");
        if (name) activate(name);
      });

      root.addEventListener("keydown", (e) => {
        if (!(e instanceof KeyboardEvent)) return;
        if (e.key !== "ArrowLeft" && e.key !== "ArrowRight") return;
        const current = tabs.findIndex((t) => t.classList.contains("is-active"));
        if (current < 0) return;
        const next = e.key === "ArrowRight"
          ? (current + 1) % tabs.length
          : (current - 1 + tabs.length) % tabs.length;
        const name = tabs[next].getAttribute("data-detail-tab");
        if (name) {
          activate(name);
          tabs[next].focus();
        }
        e.preventDefault();
      });

      root.dataset.tabsReady = "1";
    });
  };

  const initShareListing = () => {
    document.querySelectorAll("[data-share-listing]").forEach((btn) => {
      if (!(btn instanceof HTMLButtonElement) || btn.dataset.shareReady === "1") return;
      btn.addEventListener("click", async () => {
        const url = btn.getAttribute("data-share-url") || window.location.href;
        const title = btn.getAttribute("data-share-title") || document.title;
        const label = btn.querySelector("[data-share-label]");
        try {
          if (navigator.share) {
            await navigator.share({ title, url });
            return;
          }
        } catch {
          /* fall through to clipboard */
        }
        try {
          await navigator.clipboard.writeText(url);
          toast("Bağlantı kopyalandı");
          if (label) {
            const prev = label.textContent;
            label.textContent = "Kopyalandı";
            setTimeout(() => { label.textContent = prev || "Paylaş"; }, 2000);
          }
        } catch {
          toast("Bağlantı kopyalanamadı");
        }
      });
      btn.dataset.shareReady = "1";
    });
  };

  const initPrintListing = () => {
    document.querySelectorAll("[data-print-listing]").forEach((btn) => {
      if (!(btn instanceof HTMLButtonElement) || btn.dataset.printReady === "1") return;
      btn.addEventListener("click", () => window.print());
      btn.dataset.printReady = "1";
    });
  };

  document.addEventListener("DOMContentLoaded", () => {
    trackRecent();
    initVerifyBanner();
    initListEnhancements();
    initQuillDescriptions();
    initLazyImageSpinners();
    initDetailGallery();
    initDetailTabs();
    initShareListing();
    initPrintListing();
    const toastEl = document.getElementById("toast");
    const draftToast = toastEl?.getAttribute("data-draft-toast");
    if (draftToast) {
      toast(draftToast, 4200);
    }
    const boot = toastEl?.getAttribute("data-boot-toast");
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

  window.AP = { toast, STORAGE_RECENT, bindImageSpinner, initLazyImageSpinners };
})();
