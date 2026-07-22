using System.Security.Claims;
using System.Text.Json;
using AracParki.Application.Accounts;
using AracParki.Application.Accounts.Services;
using AracParki.Application.Catalog.Dtos;
using AracParki.Application.Catalog.Services;
using AracParki.Application.Common;
using AracParki.Application.Corporate.Dtos;
using AracParki.Application.Corporate.Services;
using AracParki.Application.Listings;
using AracParki.Application.Listings.Commands;
using AracParki.Application.Listings.Dtos;
using AracParki.Application.Listings.Services;
using AracParki.Domain.Listings;
using AracParki.Web.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace AracParki.Web.Pages.IlanVer;

[Authorize]
public sealed class IndexModel(
    CatalogService catalog,
    AccountService accounts,
    CorporateAccountService corporate,
    ListingCommandService listingCommands,
    ListingService listings,
    IListingImageStorage imageStorage,
    ListingImageUrlPolicy imageUrlPolicy,
    IWizardDraftStore wizardDrafts,
    IPhoneOtpService phoneOtp,
    ILogger<IndexModel> logger) : PageModel
{
    private static readonly string[] StepTitles =
    [
        "Kategori seçimi",
        "Araç bilgileri",
        "Fiyat ve konum",
        "Fotoğraflar",
        "Yayına hazır"
    ];

    private static readonly JsonSerializerOptions AttrJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public WizardDraft Draft { get; private set; } = new();
    public int Step { get; private set; } = 1;
    public string StepTitle => StepTitles[Math.Clamp(Step, 1, 5) - 1];
    public string? FormError { get; private set; }
    public IReadOnlyList<string> FormErrors { get; private set; } = [];
    /// <summary>Field key → message for inline validation (WCAG 3.3.1).</summary>
    public IReadOnlyDictionary<string, string> FieldErrors { get; private set; }
        = new Dictionary<string, string>(StringComparer.Ordinal);

    public bool HasFieldError(string key) => FieldErrors.ContainsKey(key);

    public string? FieldError(string key)
        => FieldErrors.TryGetValue(key, out var msg) ? msg : null;

    public string FieldInvalidClass(string key)
        => HasFieldError(key) ? "is-invalid" : "";

    public string? AriaInvalid(string key)
        => HasFieldError(key) ? "true" : null;
    public bool RequirePhoneVerification { get; private set; }
    public bool ShowPhoneGate => RequirePhoneVerification;
    public bool ShowDraftResumeModal { get; private set; }
    public DraftResumeSummary? DraftResume { get; private set; }
    public string? AccountPhone { get; private set; }
    public bool AccountPhoneConfirmed { get; private set; }
    public string? PublishToken { get; private set; }

    public sealed class DraftResumeSummary
    {
        public int Step { get; init; }
        public string StepLabel { get; init; } = "";
        public string? Title { get; init; }
        public string? CategoryName { get; init; }
        public int ImageCount { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
    }

    public IReadOnlyList<CategoryGroupDto> CategoryGroups { get; private set; } = [];
    public IReadOnlyList<BrandOptionDto> Brands { get; private set; } = [];
    public IReadOnlyList<EquipmentModelOptionDto> Models { get; private set; } = [];
    public IReadOnlyList<CategoryAttributeDto> Attributes { get; private set; } = [];
    public IReadOnlyList<AttachmentOptionDto> Attachments { get; private set; } = [];
    public IReadOnlyList<CityOptionDto> Cities { get; private set; } = [];
    public IReadOnlyList<DistrictOptionDto> Districts { get; private set; } = [];
    public IReadOnlyList<NeighborhoodOptionDto> Neighborhoods { get; private set; } = [];
    public IReadOnlyList<CorporateOptionDto> ApprovedCorporateAccounts { get; private set; } = [];
    public string OwnerDisplayName { get; private set; } = "";

    public string AttrJson(object value) =>
        JsonSerializer.Serialize(value, AttrJsonOptions);

    private async Task<WizardDraft> LoadDraftAsync(CancellationToken cancellationToken)
    {
        var draft = await WizardDraftStore.LoadAsync(
            HttpContext.Session, wizardDrafts, GetAccountId(), cancellationToken);
        Draft = draft;
        SyncPhoneFromAccount();
        return draft;
    }

    public async Task<IActionResult> OnGetAsync(
        int? adim,
        int? yeni,
        int? devam,
        string? duzenle,
        CancellationToken cancellationToken)
    {
        await LoadAccountPhoneAsync(cancellationToken);
        var accountId = GetAccountId();

        if (!string.IsNullOrWhiteSpace(duzenle) && accountId is not null)
        {
            var loaded = await TryLoadEditDraftAsync(duzenle, accountId.Value, cancellationToken);
            if (!loaded)
            {
                FormError = "Düzenlenecek ilan bulunamadı veya durum uygun değil.";
                TempData["AuthNotice"] = FormError;
                return RedirectToPage("/Ilanlarim/Index");
            }

            return RedirectToPage(new { adim = 2 });
        }

        if (yeni == 1 && accountId is not null)
        {
            await WizardDraftStore.DiscardHardAsync(
                HttpContext.Session, wizardDrafts, imageStorage, accountId, logger, cancellationToken);
            WizardDraftStore.SetChoice(HttpContext.Session, WizardDraftStore.ChoiceNew);
            return RedirectToPage(new { adim = 1 });
        }

        if (devam == 1 && accountId is not null)
        {
            var draft = await WizardDraftStore.HydrateFromDbAsync(
                HttpContext.Session, wizardDrafts, accountId.Value, cancellationToken);
            var step = Math.Clamp(draft.Step is >= 1 and <= 5 ? draft.Step : 1, 1, 5);
            return RedirectToPage(new { adim = step });
        }

        await ResolveDraftGateAsync(adim, cancellationToken);

        if (ShowDraftResumeModal)
        {
            Draft = new WizardDraft();
            Step = 1;
            await LoadLookupsAsync(cancellationToken);
            PublishToken = EnsurePublishToken();
            SetMeta();
            return Page();
        }

        await LoadDraftAsync(cancellationToken);
        Step = ResolveStep(adim, Draft, RequirePhoneVerification);
        await LoadLookupsAsync(cancellationToken);
        PublishToken = EnsurePublishToken();
        SetMeta();
        return Page();
    }

    public async Task<IActionResult> OnPostContinueDraftAsync(CancellationToken cancellationToken)
    {
        var accountId = GetAccountId();
        if (accountId is null)
        {
            return Challenge();
        }

        await LoadAccountPhoneAsync(cancellationToken);
        var draft = await WizardDraftStore.HydrateFromDbAsync(
            HttpContext.Session, wizardDrafts, accountId.Value, cancellationToken);
        Draft = draft;
        SyncPhoneFromAccount();
        var step = Math.Clamp(draft.Step is >= 1 and <= 5 ? draft.Step : 1, 1, 5);
        return RedirectToPage(new { adim = step });
    }

    public async Task<IActionResult> OnPostStartNewDraftAsync(CancellationToken cancellationToken)
    {
        var accountId = GetAccountId();
        if (accountId is null)
        {
            return Challenge();
        }

        await WizardDraftStore.DiscardHardAsync(
            HttpContext.Session, wizardDrafts, imageStorage, accountId, logger, cancellationToken);
        WizardDraftStore.SetChoice(HttpContext.Session, WizardDraftStore.ChoiceNew);
        return RedirectToPage(new { adim = 1 });
    }

    [EnableRateLimiting("phone-otp")]
    public async Task<IActionResult> OnPostSendPhoneOtpAsync(
        string? phone,
        CancellationToken cancellationToken)
    {
        var accountId = GetAccountId();
        if (accountId is null)
        {
            return Challenge();
        }

        await LoadAccountPhoneAsync(cancellationToken);
        if (!RequirePhoneVerification)
        {
            return new JsonResult(new { ok = true, alreadyVerified = true });
        }

        var normalized = AccountService.NormalizePhone(phone)
                         ?? AccountService.NormalizePhone(AccountPhone);
        if (normalized is null)
        {
            return new JsonResult(new { ok = false, error = "Geçerli bir telefon numarası gir (10–15 rakam)." })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var (ok, error, devCode) = await phoneOtp.SendAsync(accountId.Value, normalized, cancellationToken);
        if (!ok)
        {
            return new JsonResult(new { ok = false, error = error ?? "Doğrulama kodu gönderilemedi." })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        await LoadDraftAsync(cancellationToken);
        Draft.Phone = normalized;
        await PersistDraftAsync(cancellationToken);

        return new JsonResult(new
        {
            ok = true,
            message = "Kod WhatsApp’a gönderildi.",
            maskedPhone = AccountService.MaskPhone(normalized),
            devCode
        });
    }

    [EnableRateLimiting("phone-otp-verify")]
    public async Task<IActionResult> OnPostVerifyPhoneOtpAsync(
        string? phone,
        string? otpCode,
        CancellationToken cancellationToken)
    {
        var accountId = GetAccountId();
        if (accountId is null)
        {
            return Challenge();
        }

        await LoadAccountPhoneAsync(cancellationToken);
        if (!RequirePhoneVerification)
        {
            return new JsonResult(new { ok = true, alreadyVerified = true, reload = true });
        }

        var normalized = AccountService.NormalizePhone(phone)
                         ?? AccountService.NormalizePhone(AccountPhone);
        if (normalized is null)
        {
            await LoadDraftAsync(cancellationToken);
            normalized = AccountService.NormalizePhone(Draft.Phone);
        }

        if (normalized is null)
        {
            return new JsonResult(new { ok = false, error = "Geçerli bir telefon numarası gir." })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        if (string.IsNullOrWhiteSpace(otpCode))
        {
            return new JsonResult(new { ok = false, error = "Doğrulama kodunu gir." })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var (ok, error) = await phoneOtp.VerifyAsync(accountId.Value, normalized, otpCode, cancellationToken);
        if (!ok)
        {
            return new JsonResult(new { ok = false, error = error ?? "Doğrulama başarısız." })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        await LoadDraftAsync(cancellationToken);
        Draft.Phone = normalized;
        Draft.PhoneVerified = true;
        await PersistDraftAsync(cancellationToken);
        await RefreshAuthCookieAsync(accountId.Value, cancellationToken);

        return new JsonResult(new { ok = true, reload = true });
    }

    public async Task<IActionResult> OnPostCascadeAsync(
        string? intent,
        string? condition,
        int categoryId,
        int brandId,
        int? modelId,
        string? modelName,
        int modelYear,
        int? groupId,
        string? groupName,
        CancellationToken cancellationToken)
    {
        if (await RejectIfPhoneGateAsync(cancellationToken) is { } gated)
        {
            return gated;
        }

        await LoadDraftAsync(cancellationToken);

        if (intent is not (ListingIntent.Satilik or ListingIntent.Kiralik))
        {
            return await FailAsync(1, "İlan tipi seç (Satılık veya Kiralık).", cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(condition) || !EquipmentCondition.Known.Contains(condition))
        {
            return await FailAsync(1, "Durum seç (İkinci el / Sıfır).", cancellationToken);
        }

        var categories = await catalog.GetAllCategoriesAsync(cancellationToken);
        var category = categories.FirstOrDefault(c => c.Id == categoryId);
        if (category is null)
        {
            return await FailAsync(1, "Kategori seçimi geçersiz.", cancellationToken);
        }

        var brands = await catalog.GetBrandsByCategoryAsync(categoryId, cancellationToken);
        var brand = brands.FirstOrDefault(b => b.Id == brandId);
        if (brand is null)
        {
            return await FailAsync(1, "Marka seçimi geçersiz — cascader’da markayı seç.", cancellationToken);
        }

        string resolvedModelName = modelName?.Trim() ?? "";
        int? resolvedModelId = modelId is > 0 ? modelId : null;
        EquipmentModelOptionDto? catalogModel = null;
        if (resolvedModelId is not null)
        {
            var models = await catalog.GetModelsByBrandCategoryAsync(brandId, categoryId, cancellationToken);
            catalogModel = models.FirstOrDefault(m => m.Id == resolvedModelId);
            if (catalogModel is null)
            {
                return await FailAsync(1, "Model seçimi geçersiz.", cancellationToken);
            }

            resolvedModelName = catalogModel.Name;
        }

        if (string.IsNullOrWhiteSpace(resolvedModelName))
        {
            return await FailAsync(1, "Model seçimi zorunlu.", cancellationToken);
        }

        if (modelYear is < 1950 or > 2100)
        {
            return await FailAsync(1, "Model yılı geçersiz.", cancellationToken);
        }

        var categoryChanged = Draft.CategoryId != categoryId;
        if (categoryChanged)
        {
            Draft.Specs = new Dictionary<string, string>(StringComparer.Ordinal);
            Draft.SpecsJson = "{}";
            Draft.AttachmentIds = [];
            Draft.CapacityKg = null;
            Draft.Title = "";
            Draft.Description = "";
        }

        var intentChanged = !string.Equals(Draft.PrimaryIntent, intent, StringComparison.Ordinal);
        Draft.PrimaryIntent = intent;
        Draft.Intents = [intent];
        Draft.Condition = condition;
        if (intentChanged)
        {
            Draft.RentPrice = null;
            Draft.PriceUnit = null;
            Draft.IncludesOperator = false;
            if (Draft.Step > 3)
            {
                Draft.Step = 3;
            }
        }

        Draft.GroupId = groupId is > 0 ? groupId.Value : category.GroupId ?? 0;
        Draft.GroupName = string.IsNullOrWhiteSpace(groupName) ? null : groupName.Trim();
        Draft.CategoryId = category.Id;
        Draft.CategoryName = category.Name;
        Draft.CapacityMetric = category.CapacityMetric;
        Draft.BrandId = brand.Id;
        Draft.BrandName = brand.Name;
        Draft.ModelId = resolvedModelId;
        Draft.ModelName = resolvedModelName;
        Draft.ModelYear = modelYear;

        var attrs = await catalog.GetCategoryAttributesAsync(category.Id, cancellationToken);
        if (catalogModel is not null)
        {
            CatalogModelDefaults.Apply(Draft, catalogModel, category.CapacityMetric, attrs);
        }
        else
        {
            CatalogModelDefaults.ClearCatalogLocks(Draft);
            CatalogModelDefaults.PruneSpecs(Draft, attrs);
        }

        Draft.Step = 2;
        await PersistDraftAsync(
            cancellationToken,
            notice: "Kategori seçimi kaydedildi · Sırada araç bilgileri");
        return RedirectToPage(new { adim = 2 });
    }

    public async Task<IActionResult> OnPostMachineAsync(
        string? condition,
        int? hours,
        bool hoursUnknown,
        decimal tons,
        int? capacityKg,
        int? horsepower,
        bool horsepowerUnknown,
        string? serialNo,
        string? title,
        string? description,
        [FromForm] Dictionary<string, string>? specs,
        int[]? attachmentIds,
        CancellationToken cancellationToken)
    {
        if (await RejectIfPhoneGateAsync(cancellationToken) is { } gated)
        {
            return gated;
        }

        await LoadDraftAsync(cancellationToken);
        if (!Draft.HasCategory || !Draft.HasIntent)
        {
            return RedirectToPage(new { adim = 1 });
        }

        if (string.IsNullOrWhiteSpace(condition) || !EquipmentCondition.Known.Contains(condition))
        {
            return await FailAsync(2, "Durum seçimi geçersiz.", cancellationToken);
        }

        var categoryAttrs = await catalog.GetCategoryAttributesAsync(Draft.CategoryId, cancellationToken);
        // Catalog-sourced keys stay authoritative; form may only fill gaps / non-catalog attrs.
        var catalogLockedKeys = CatalogModelDefaults.LockedSpecKeys(Draft);
        var formSpecs = specs is null
            ? null
            : specs
                .Where(kv => !catalogLockedKeys.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        var mergedForBuild = new Dictionary<string, string>(Draft.Specs, StringComparer.Ordinal);
        if (formSpecs is not null)
        {
            foreach (var (k, v) in formSpecs)
            {
                if (!string.IsNullOrWhiteSpace(v))
                {
                    mergedForBuild[k] = v;
                }
            }
        }

        // Dedicated capacityKg field (or catalog value) must satisfy required kg-capacity specs
        // such as platform_capacity_kg before SpecsJsonBuilder runs.
        if (string.Equals(Draft.CapacityMetric, "capacity_kg", StringComparison.Ordinal))
        {
            var resolvedKg = Draft.CapacityKgFromCatalog && Draft.CapacityKg is > 0
                ? Draft.CapacityKg
                : capacityKg is > 0
                    ? capacityKg
                    : CatalogModelDefaults.TryReadCapacityKgFromSpecs(mergedForBuild);

            if (resolvedKg is > 0)
            {
                CatalogModelDefaults.ApplyCapacityKgToSpecs(
                    Draft, resolvedKg.Value, mergedForBuild, categoryAttrs);
            }
        }

        var (specsOk, specsError, specsJson, storedRaw) = SpecsJsonBuilder.TryBuild(mergedForBuild, categoryAttrs);
        if (!specsOk)
        {
            var specGaps = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var attr in categoryAttrs.Where(a => a.IsRequired))
            {
                if (!mergedForBuild.TryGetValue(attr.Key, out var raw) || string.IsNullOrWhiteSpace(raw))
                {
                    // Point kg-capacity attribute gaps at the dedicated #field-capacityKg when shown.
                    if (string.Equals(Draft.CapacityMetric, "capacity_kg", StringComparison.Ordinal)
                        && CatalogModelDefaults.CapacityKgSpecKeys.Contains(attr.Key, StringComparer.Ordinal))
                    {
                        specGaps["capacityKg"] = "Kapasite (kg) gir.";
                    }
                    else
                    {
                        specGaps["spec:" + attr.Key] = $"{attr.Label} zorunlu.";
                    }
                }
            }

            if (specGaps.Count == 0 && !string.IsNullOrWhiteSpace(specsError))
            {
                specGaps["specs"] = specsError!;
            }

            return await FailAsync(
                2,
                specsError ?? "Özellikler geçersiz.",
                cancellationToken,
                specGaps);
        }

        var allowedAttachments = await catalog.GetAttachmentsByCategoryAsync(Draft.CategoryId, cancellationToken);
        var allowedIds = allowedAttachments.Select(a => a.Id).ToHashSet();
        var selectedAttachments = (attachmentIds ?? [])
            .Where(id => allowedIds.Contains(id))
            .Distinct()
            .Take(20)
            .ToList();

        Draft.Condition = condition;
        Draft.HoursUnknown = hoursUnknown;
        Draft.Hours = hoursUnknown ? null : hours;

        if (!Draft.TonsFromCatalog)
        {
            Draft.Tons = tons;
        }

        if (string.Equals(Draft.CapacityMetric, "capacity_kg", StringComparison.Ordinal))
        {
            if (!Draft.CapacityKgFromCatalog)
            {
                Draft.CapacityKg = capacityKg is > 0
                    ? capacityKg
                    : CatalogModelDefaults.TryReadCapacityKgFromSpecs(storedRaw);
            }

            // listings.tons CHECK (tons > 0): derive when catalog has no operating weight.
            if (Draft.Tons <= 0 && Draft.CapacityKg is > 0)
            {
                Draft.Tons = CatalogModelDefaults.DeriveTonsFromCapacityKg(Draft.CapacityKg.Value);
            }
        }
        else
        {
            Draft.CapacityKg = Draft.CapacityKgFromCatalog ? Draft.CapacityKg : null;
        }

        if (!Draft.HorsepowerFromCatalog)
        {
            Draft.HorsepowerUnknown = true;
            Draft.Horsepower = null;
        }
        else
        {
            Draft.HorsepowerUnknown = false;
        }

        Draft.SerialNo = string.IsNullOrWhiteSpace(serialNo) ? null : serialNo.Trim();
        Draft.Title = string.IsNullOrWhiteSpace(title) ? Draft.SuggestedTitle() : title.Trim();
        Draft.Description = ListingDescriptionHtml.Sanitize(description);
        Draft.Specs = storedRaw;
        Draft.SpecsJson = specsJson;
        Draft.AttachmentIds = selectedAttachments;

        if (!Draft.HasMachine)
        {
            var gaps = CollectMachineFieldErrors(Draft);
            return await FailAsync(
                2,
                gaps.Count > 0
                    ? "Zorunlu alanları tamamla — eksikler aşağıda işaretlendi."
                    : "Makine bilgilerini kontrol et — zorunlu alanlar eksik.",
                cancellationToken,
                gaps);
        }

        Draft.Step = 3;
        await PersistDraftAsync(
            cancellationToken,
            notice: "Araç bilgileri kaydedildi · Sırada fiyat ve konum");
        return RedirectToPage(new { adim = 3 });
    }

    public async Task<IActionResult> OnPostSaleAsync(
        decimal price,
        string? currency,
        string? priceUnit,
        bool includesOperator,
        long? corporateAccountId,
        string? contactPhoneSource,
        int cityId,
        int districtId,
        int? neighborhoodId,
        CancellationToken cancellationToken)
    {
        if (await RejectIfPhoneGateAsync(cancellationToken) is { } gated)
        {
            return gated;
        }

        await LoadDraftAsync(cancellationToken);
        if (!Draft.HasMachine || !Draft.HasIntent)
        {
            return RedirectToPage(new { adim = Draft.HasCategory && Draft.HasIntent ? 2 : 1 });
        }

        var accountId = GetAccountId();
        if (accountId is null)
        {
            return Challenge();
        }

        await LoadAccountPhoneAsync(cancellationToken);

        string sellerType;
        string? corporateName = null;
        long? resolvedCorporateId = null;
        string? corporatePhone = null;

        if (corporateAccountId is > 0)
        {
            var option = await corporate.GetApprovedOptionAsync(
                corporateAccountId.Value,
                accountId.Value,
                cancellationToken);
            if (option is null)
            {
                return await FailAsync(
                    3,
                    "Seçilen kurumsal hesap onaylı değil veya sana ait değil.",
                    cancellationToken,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["sellerType"] = "Onaylı kurumsal hesap seç veya Sahibinden ile devam et."
                    });
            }

            sellerType = SellerType.Dealer;
            resolvedCorporateId = option.Id;
            corporateName = option.DisplayName;
            corporatePhone = AccountService.NormalizePhone(option.Phone);
        }
        else
        {
            sellerType = SellerType.Owner;
        }

        var preferCorporatePhone = resolvedCorporateId is > 0
                                   && string.Equals(contactPhoneSource, "corporate", StringComparison.OrdinalIgnoreCase)
                                   && corporatePhone is not null;
        var listingPhone = preferCorporatePhone
            ? corporatePhone
            : AccountService.NormalizePhone(AccountPhone) ?? AccountService.NormalizePhone(Draft.Phone);

        if (listingPhone is null && !RequirePhoneVerification)
        {
            // Account phone missing but gate already passed somehow — keep draft phone if any.
            listingPhone = AccountService.NormalizePhone(Draft.Phone);
        }

        var cities = await catalog.GetAllCitiesAsync(cancellationToken);
        var city = cities.FirstOrDefault(c => c.Id == cityId);
        DistrictOptionDto? district = null;
        NeighborhoodOptionDto? neighborhood = null;
        if (city is not null)
        {
            var districts = await catalog.GetDistrictsByCityAsync(cityId, cancellationToken);
            district = districts.FirstOrDefault(d => d.Id == districtId);
            if (district is not null && neighborhoodId is > 0)
            {
                var neighborhoods = await catalog.GetNeighborhoodsByDistrictAsync(districtId, cancellationToken);
                neighborhood = neighborhoods.FirstOrDefault(n => n.Id == neighborhoodId);
            }
        }

        var isRent = Draft.PrimaryIntent == ListingIntent.Kiralik;
        Draft.Intents = [Draft.PrimaryIntent];
        Draft.Price = price;
        Draft.RentPrice = null;
        Draft.Currency = Currency.Normalize(currency);
        Draft.PriceUnit = isRent && !string.IsNullOrWhiteSpace(priceUnit) ? priceUnit.Trim() : null;
        Draft.IncludesOperator = includesOperator && isRent;
        Draft.SellerType = sellerType;
        Draft.CorporateAccountId = resolvedCorporateId;
        Draft.CorporateName = corporateName;
        Draft.ContactPhoneSource = preferCorporatePhone ? "corporate" : "account";
        Draft.CityId = city?.Id ?? 0;
        Draft.CityName = city?.Name;
        Draft.DistrictId = district?.Id ?? 0;
        Draft.DistrictName = district?.Name;
        Draft.NeighborhoodId = neighborhood?.Id;
        Draft.NeighborhoodName = neighborhood?.DisplayName ?? neighborhood?.Name;
        if (listingPhone is not null)
        {
            Draft.Phone = listingPhone;
            Draft.PhoneVerified = true;
        }
        else
        {
            Draft.Phone = AccountService.NormalizePhone(AccountPhone) ?? Draft.Phone;
            Draft.PhoneVerified = AccountPhoneConfirmed || Draft.PhoneVerified;
        }

        if (city is null || district is null)
        {
            return await FailAsync(3, "İl ve ilçe seçimi zorunlu.", cancellationToken);
        }

        if (!Draft.HasSaleInfo(RequirePhoneVerification))
        {
            var gaps = CollectSaleFieldErrors(Draft, RequirePhoneVerification);
            return await FailAsync(
                3,
                gaps.Count > 0
                    ? "Zorunlu alanları tamamla — eksikler aşağıda işaretlendi."
                    : "Satış bilgilerini kontrol et.",
                cancellationToken,
                gaps);
        }

        Draft.Step = 4;
        await PersistDraftAsync(
            cancellationToken,
            notice: "Fiyat ve konum kaydedildi · Sırada fotoğraflar");
        return RedirectToPage(new { adim = 4 });
    }

    public async Task<IActionResult> OnPostImagesAsync(string[]? imageUrls, CancellationToken cancellationToken)
    {
        if (await RejectIfPhoneGateAsync(cancellationToken) is { } gated)
        {
            return gated;
        }

        await LoadDraftAsync(cancellationToken);
        if (!Draft.HasSaleInfo(RequirePhoneVerification))
        {
            return RedirectToPage(new { adim = 3 });
        }

        var submitted = (imageUrls ?? [])
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .ToList();

        if (submitted.Count > ListingImageUrl.MaxCount)
        {
            return await FailAsync(4, $"En fazla {ListingImageUrl.MaxCount} görsel ekleyebilirsin.", cancellationToken);
        }

        Draft.ImageUrls = submitted
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(ListingImageUrl.MaxCount)
            .ToList();

        Draft.ImageAssets = Draft.ImageAssets
            .Where(a => Draft.ImageUrls.Contains(a.DeliveryUrl, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (Draft.ImageUrls.Count == 0)
        {
            return await FailAsync(4, "En az 1 görsel yükle.", cancellationToken);
        }

        if (Draft.ImageUrls.Any(u => !imageUrlPolicy.IsAllowed(u)))
        {
            return await FailAsync(4, "Geçersiz görsel. Yalnızca yüklenen fotoğraflar kullanılabilir.", cancellationToken);
        }

        Draft.Step = 5;
        await PersistDraftAsync(
            cancellationToken,
            notice: "Fotoğraflar kaydedildi · Yayına hazırlığa geçildi");
        return RedirectToPage(new { adim = 5 });
    }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (await RejectIfPhoneGateAsync(cancellationToken) is { } gated)
        {
            return gated;
        }

        await LoadDraftAsync(cancellationToken);
        var accountId = GetAccountId();
        if (accountId is null)
        {
            return Challenge();
        }

        if (!Draft.HasSaleInfo(RequirePhoneVerification))
        {
            return RedirectToPage(new { adim = 3 });
        }

        if (file is null || file.Length == 0)
        {
            return await FailAsync(4, "Dosya seçilmedi.", cancellationToken);
        }

        var error = await TrySaveUploadAsync(accountId.Value, file, cancellationToken);
        if (error is not null)
        {
            return await FailAsync(4, error, cancellationToken);
        }

        Draft.Step = 4;
        await PersistDraftAsync(cancellationToken);
        return RedirectToPage(new { adim = 4 });
    }

    [EnableRateLimiting("listing-wizard-upload")]
    public async Task<IActionResult> OnPostUploadJsonAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        await LoadAccountPhoneAsync(cancellationToken);
        if (RequirePhoneVerification)
        {
            return new JsonResult(new { ok = false, error = "İlan vermek için önce telefonunu doğrula." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        var accountId = GetAccountId();
        if (accountId is null)
        {
            return Challenge();
        }

        await LoadDraftAsync(cancellationToken);
        if (!Draft.HasSaleInfo(RequirePhoneVerification))
        {
            return new JsonResult(new { ok = false, error = "Önce fiyat ve konum adımını tamamla." })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        if (file is null || file.Length == 0)
        {
            return new JsonResult(new { ok = false, error = "Dosya seçilmedi." })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var error = await TrySaveUploadAsync(accountId.Value, file, cancellationToken);
        if (error is not null)
        {
            return new JsonResult(new { ok = false, error })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        Draft.Step = 4;
        await PersistDraftAsync(cancellationToken);

        var saved = Draft.ImageAssets.LastOrDefault();
        return new JsonResult(new
        {
            ok = true,
            deliveryUrl = Draft.ImageUrls[^1],
            asset = saved is null
                ? null
                : new
                {
                    imageId = saved.ImageId,
                    storageKey = saved.StorageKey,
                    width = saved.Width,
                    height = saved.Height,
                    byteSize = saved.ByteSize,
                    mimeType = saved.MimeType,
                    checksumSha256 = saved.ChecksumSha256,
                    originalFilename = saved.OriginalFilename
                },
            count = Draft.ImageUrls.Count,
            maxCount = ListingImageUrl.MaxCount
        });
    }

    public async Task<IActionResult> OnPostRemoveImageAsync(string? url, CancellationToken cancellationToken)
    {
        if (await RejectIfPhoneGateAsync(cancellationToken) is { } gated)
        {
            return gated;
        }

        await LoadDraftAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(url))
        {
            return RedirectToPage(new { adim = 4 });
        }

        var target = url.Trim();
        var asset = Draft.ImageAssets.FirstOrDefault(a =>
            string.Equals(a.DeliveryUrl, target, StringComparison.OrdinalIgnoreCase));

        if (ListingImageUrl.TryResolveStorageKey(asset, target, out var storageKey))
        {
            try
            {
                await imageStorage.DeleteAsync(storageKey, cancellationToken);
            }
            catch (Exception ex)
            {
                // Draft still drops the reference; orphan cleanup can retry later.
                logger.LogWarning(ex, "Hard-delete failed for {StorageKey} ({Url})", storageKey, target);
            }
        }

        Draft.ImageUrls = Draft.ImageUrls
            .Where(u => !string.Equals(u, target, StringComparison.OrdinalIgnoreCase))
            .ToList();
        Draft.ImageAssets = Draft.ImageAssets
            .Where(a => !string.Equals(a.DeliveryUrl, target, StringComparison.OrdinalIgnoreCase))
            .ToList();
        Draft.Step = 4;
        await PersistDraftAsync(cancellationToken);
        return RedirectToPage(new { adim = 4 });
    }

    public async Task<IActionResult> OnPostSetCoverAsync(string? url, CancellationToken cancellationToken)
    {
        if (await RejectIfPhoneGateAsync(cancellationToken) is { } gated)
        {
            return gated;
        }

        await LoadDraftAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(url))
        {
            return RedirectToPage(new { adim = 4 });
        }

        var target = url.Trim();
        var idx = Draft.ImageUrls.FindIndex(u => string.Equals(u, target, StringComparison.OrdinalIgnoreCase));
        if (idx <= 0)
        {
            return RedirectToPage(new { adim = 4 });
        }

        Draft.ImageUrls.RemoveAt(idx);
        Draft.ImageUrls.Insert(0, target);

        var asset = Draft.ImageAssets.FirstOrDefault(a =>
            string.Equals(a.DeliveryUrl, target, StringComparison.OrdinalIgnoreCase));
        if (asset is not null)
        {
            Draft.ImageAssets.Remove(asset);
            Draft.ImageAssets.Insert(0, asset);
        }

        Draft.Step = 4;
        await PersistDraftAsync(cancellationToken);
        return RedirectToPage(new { adim = 4 });
    }

    private async Task<string?> TrySaveUploadAsync(
        long accountId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length > ListingImageUrl.MaxUploadBytes)
        {
            return "Görsel en fazla 10 MB olabilir.";
        }

        if (Draft.ImageUrls.Count >= ListingImageUrl.MaxCount)
        {
            return $"En fazla {ListingImageUrl.MaxCount} görsel ekleyebilirsin.";
        }

        try
        {
            string detectedType;
            await using (var probe = file.OpenReadStream())
            {
                var (sigOk, type, sigError) =
                    await FileSignatures.DetectImageAsync(probe, cancellationToken);
                if (!sigOk || string.IsNullOrWhiteSpace(type))
                {
                    return sigError ?? "Dosya geçerli bir görsel değil.";
                }

                detectedType = type;
            }

            await using var stream = file.OpenReadStream();
            var saved = await imageStorage.SaveAsync(
                accountId,
                stream,
                detectedType,
                file.FileName,
                cancellationToken);
            Draft.ImageUrls.Add(saved.DeliveryUrl);
            Draft.ImageAssets.Add(ListingImageAsset.FromSaveResult(saved));
            if (WizardDraftStore.GetChoice(HttpContext.Session) is null)
            {
                WizardDraftStore.SetChoice(HttpContext.Session, WizardDraftStore.ChoiceContinue);
            }

            return null;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    [EnableRateLimiting("listing-publish")]
    public async Task<IActionResult> OnPostPublishAsync(string? publishToken, CancellationToken cancellationToken)
    {
        if (await RejectIfPhoneGateAsync(cancellationToken) is { } gated)
        {
            return gated;
        }

        await LoadDraftAsync(cancellationToken);
        Step = 5;

        var expected = HttpContext.Session.GetString("ilan-ver-publish-token");
        if (string.IsNullOrWhiteSpace(publishToken)
            || string.IsNullOrWhiteSpace(expected)
            || !CryptographicOperationsEquals(expected, publishToken))
        {
            PublishToken = EnsurePublishToken();
            return await FailAsync(5, "Yayın isteği geçersiz veya tekrarlandı. Tekrar dene.", cancellationToken);
        }

        HttpContext.Session.Remove("ilan-ver-publish-token");

        if (!Draft.HasCategory || !Draft.HasMachine || !Draft.HasSaleInfo(RequirePhoneVerification) || !Draft.HasImages)
        {
            return await FailAsync(5, "İlan henüz yayınlanmaya hazır değil. Adımları tamamla.", cancellationToken);
        }

        var accountId = GetAccountId();
        if (accountId is null)
        {
            return Challenge();
        }

        var account = await accounts.GetByIdAsync(accountId.Value, cancellationToken);
        if (account is null)
        {
            return Challenge();
        }

        if (!account.EmailConfirmed)
        {
            return await FailAsync(
                5,
                "E-posta adresini doğrulamadan ilan yayınlanamaz. Gelen kutundaki bağlantıyı aç.",
                cancellationToken);
        }

        var listingPhone = AccountService.NormalizePhone(Draft.Phone)
                           ?? AccountService.NormalizePhone(account.Phone);
        if (listingPhone is null || (!account.PhoneConfirmed && !Draft.PhoneVerified))
        {
            return await FailAsync(5, "Telefon doğrulanmadan yayınlanamaz. Satış adımına dön.", cancellationToken);
        }

        // Hesap telefonunu yalnızca kişisel numara seçildiyse / eksikse güncelle; bayi numarasını hesap profiline yazma.
        if ((string.IsNullOrWhiteSpace(account.Phone) || !account.PhoneConfirmed)
            && !string.Equals(Draft.ContactPhoneSource, "corporate", StringComparison.OrdinalIgnoreCase))
        {
            var (ok, error, updated) = await accounts.UpdatePhoneAsync(accountId.Value, listingPhone, cancellationToken);
            if (!ok || updated is null)
            {
                return await FailAsync(5, error ?? "Telefon güncellenemedi.", cancellationToken);
            }

            await RefreshAuthCookieAsync(accountId.Value, cancellationToken);
            account = await accounts.GetByIdAsync(accountId.Value, cancellationToken) ?? updated;
        }

        string sellerDisplayName = account.DisplayName;
        long? corporateAccountId = null;
        string publishPhone = listingPhone;
        if (Draft.CorporateAccountId is > 0)
        {
            var option = await corporate.GetApprovedOptionAsync(
                Draft.CorporateAccountId.Value,
                account.Id,
                cancellationToken);
            if (option is null)
            {
                return await FailAsync(
                    5,
                    "Seçilen kurumsal hesap artık onaylı değil. Fiyat adımına dönüp satıcıyı güncelle.",
                    cancellationToken);
            }

            corporateAccountId = option.Id;
            sellerDisplayName = option.DisplayName;
            Draft.SellerType = SellerType.Dealer;
            Draft.CorporateName = option.DisplayName;

            if (string.Equals(Draft.ContactPhoneSource, "corporate", StringComparison.OrdinalIgnoreCase))
            {
                var corpPhone = AccountService.NormalizePhone(option.Phone);
                if (corpPhone is not null)
                {
                    publishPhone = corpPhone;
                    Draft.Phone = corpPhone;
                }
            }
        }
        else
        {
            Draft.SellerType = SellerType.Owner;
            Draft.CorporateAccountId = null;
            Draft.CorporateName = null;
            Draft.ContactPhoneSource = "account";
        }

        var command = new CreatePublishedListingCommand
        {
            AccountId = account.Id,
            SellerDisplayName = sellerDisplayName,
            Phone = publishPhone,
            SellerType = Draft.SellerType,
            CorporateAccountId = corporateAccountId,
            CategoryId = Draft.CategoryId,
            BrandId = Draft.BrandId,
            ModelId = Draft.ModelId,
            ModelName = Draft.ModelName,
            SerialNo = Draft.SerialNo,
            CityId = Draft.CityId,
            DistrictId = Draft.DistrictId,
            NeighborhoodId = Draft.NeighborhoodId,
            PrimaryIntent = Draft.PrimaryIntent,
            Intents = [Draft.PrimaryIntent],
            Condition = Draft.Condition,
            ModelYear = Draft.ModelYear,
            Hours = Draft.HoursUnknown ? null : Draft.Hours,
            Tons = Draft.Tons,
            CapacityKg = Draft.CapacityKg,
            Horsepower = Draft.HorsepowerUnknown ? null : Draft.Horsepower,
            CapacityMetric = Draft.CapacityMetric,
            Price = Draft.Price,
            RentPrice = null,
            Currency = Currency.Normalize(Draft.Currency),
            PriceUnit = Draft.PriceUnit,
            IncludesOperator = Draft.IncludesOperator,
            Title = Draft.Title,
            Description = Draft.Description,
            SpecsJson = string.IsNullOrWhiteSpace(Draft.SpecsJson) ? "{}" : Draft.SpecsJson,
            ImageUrls = Draft.ImageUrls,
            ImageAssets = Draft.ImageAssets,
            AttachmentIds = Draft.AttachmentIds
        };

        try
        {
            string adNo;
            if (!string.IsNullOrWhiteSpace(Draft.EditingAdNo))
            {
                await listingCommands.UpdateForReviewAsync(Draft.EditingAdNo, command, cancellationToken);
                adNo = Draft.EditingAdNo;
            }
            else
            {
                adNo = await listingCommands.CreatePublishedAsync(command, cancellationToken);
            }

            await WizardDraftStore.ClearAllAsync(HttpContext.Session, wizardDrafts, accountId, cancellationToken);
            TempData["AuthNotice"] = $"İlanın incelemeye alındı ({adNo}). Onay sonrası yayınlanır.";
            return RedirectToPage("/Ilanlarim/Index");
        }
        catch (ValidationException ex)
        {
            FormErrors = ex.Errors.Select(e => e.ErrorMessage).Distinct().ToArray();
            FormError = FormErrors.FirstOrDefault() ?? "İlan doğrulanamadı.";
            logger.LogWarning(ex, "Listing publish validation failed");
            await LoadLookupsAsync(cancellationToken);
            PublishToken = EnsurePublishToken();
            SetMeta();
            return Page();
        }
        catch (Exception ex)
        {
            FormError = "İlan yayınlanırken bir hata oluştu. Lütfen tekrar dene.";
            logger.LogError(ex, "Listing publish failed for account {AccountId}", account.Id);
            await LoadLookupsAsync(cancellationToken);
            PublishToken = EnsurePublishToken();
            SetMeta();
            return Page();
        }
    }

    public IReadOnlyList<string> EnumOptions(CategoryAttributeDto attr)
    {
        if (string.IsNullOrWhiteSpace(attr.EnumOptionsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(attr.EnumOptionsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public bool HasAttachment(int id) => Draft.AttachmentIds.Contains(id);

    private async Task<IActionResult?> RejectIfPhoneGateAsync(CancellationToken cancellationToken)
    {
        await LoadAccountPhoneAsync(cancellationToken);
        if (!RequirePhoneVerification)
        {
            return null;
        }

        await LoadDraftAsync(cancellationToken);
        FormError = "İlan vermek için önce telefonunu doğrula.";
        FormErrors = [FormError];
        Step = Math.Clamp(Draft.Step is >= 1 and <= 5 ? Draft.Step : 1, 1, 5);
        await LoadLookupsAsync(cancellationToken);
        PublishToken = EnsurePublishToken();
        SetMeta();
        return Page();
    }

    private async Task<IActionResult> FailAsync(
        int step,
        string message,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? fieldErrors = null)
    {
        FormError = message;
        FieldErrors = fieldErrors is { Count: > 0 }
            ? new Dictionary<string, string>(fieldErrors, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
        FormErrors = FieldErrors.Count > 0
            ? FieldErrors.Values.Distinct().ToArray()
            : [message];
        Step = step;
        await LoadLookupsAsync(cancellationToken);
        PublishToken = EnsurePublishToken();
        SetMeta();
        return Page();
    }

    /// <summary>
    /// Maps HasMachine gaps to field keys for inline errors (Deque / WCAG 3.3.1).
    /// </summary>
    private static Dictionary<string, string> CollectMachineFieldErrors(WizardDraft draft)
    {
        var gaps = new Dictionary<string, string>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(draft.Condition) || !EquipmentCondition.Known.Contains(draft.Condition))
        {
            gaps["condition"] = "Durum seç (İkinci el / Sıfır).";
        }

        if (!draft.HoursUnknown && draft.Hours is null)
        {
            gaps["hours"] = "Çalışma saati gir veya “Bilmiyorum” seç.";
        }

        if (string.Equals(draft.CapacityMetric, "capacity_kg", StringComparison.Ordinal))
        {
            if (draft.CapacityKg is not > 0)
            {
                gaps["capacityKg"] = "Kapasite (kg) gir.";
            }
        }
        else if (draft.Tons <= 0)
        {
            gaps["tons"] = "Tonaj / kapasite (ton) gir.";
        }

        if (string.IsNullOrWhiteSpace(draft.Title))
        {
            gaps["title"] = "İlan başlığı zorunlu.";
        }

        if (ListingDescriptionHtml.IsBlank(draft.Description))
        {
            gaps["description"] = "Açıklama zorunlu.";
        }
        else if (ListingDescriptionHtml.Sanitize(draft.Description).Length > ListingDescriptionHtml.MaxLength)
        {
            gaps["description"] = $"Açıklama en fazla {ListingDescriptionHtml.MaxLength} karakter.";
        }

        return gaps;
    }

    private static Dictionary<string, string> CollectSaleFieldErrors(WizardDraft draft, bool requirePhone)
    {
        var gaps = new Dictionary<string, string>(StringComparer.Ordinal);

        if (draft.Price <= 0)
        {
            gaps["price"] = draft.PrimaryIntent == ListingIntent.Kiralik
                ? "Kira bedeli gir."
                : "Satış fiyatı gir.";
        }

        if (!Currency.Known.Contains(Currency.Normalize(draft.Currency)))
        {
            gaps["currency"] = "Para birimi seç.";
        }

        if (draft.PrimaryIntent == ListingIntent.Kiralik
            && (string.IsNullOrWhiteSpace(draft.PriceUnit) || !PriceUnit.Known.Contains(draft.PriceUnit)))
        {
            gaps["priceUnit"] = "Kira birimi seç.";
        }

        if (string.IsNullOrWhiteSpace(draft.SellerType) || !SellerType.Known.Contains(draft.SellerType))
        {
            gaps["sellerType"] = "Satıcı tipi seç.";
        }

        if (draft.CityId <= 0)
        {
            gaps["cityId"] = "İl seç.";
        }

        if (draft.DistrictId <= 0)
        {
            gaps["districtId"] = "İlçe seç.";
        }

        if (requirePhone
            && (AccountService.NormalizePhone(draft.Phone) is null || !draft.PhoneVerified))
        {
            gaps["phone"] = "Telefon doğrulaması gerekli.";
        }

        return gaps;
    }

    private async Task ResolveDraftGateAsync(int? adim, CancellationToken cancellationToken)
    {
        ShowDraftResumeModal = false;
        DraftResume = null;

        if (RequirePhoneVerification)
        {
            return;
        }

        var accountId = GetAccountId();
        if (accountId is null)
        {
            return;
        }

        // Mid-wizard (?adim=N): keep the previous continue/new choice.
        // Fresh entry (/ilan-ver): always re-ask when a saved draft exists.
        var isFreshEntry = adim is null;
        if (!isFreshEntry)
        {
            var choice = WizardDraftStore.GetChoice(HttpContext.Session);
            if (!string.IsNullOrWhiteSpace(choice))
            {
                return;
            }
        }
        else
        {
            WizardDraftStore.ClearChoice(HttpContext.Session);
        }

        var (dbDraft, meta) = await WizardDraftStore.PeekDbDraftAsync(
            wizardDrafts, accountId.Value, cancellationToken);
        if (meta is null || !dbDraft.IsMeaningful)
        {
            return;
        }

        ShowDraftResumeModal = true;
        var step = Math.Clamp(meta.Step is >= 1 and <= 5 ? meta.Step : dbDraft.Step, 1, 5);
        DraftResume = new DraftResumeSummary
        {
            Step = step,
            StepLabel = StepTitles[step - 1],
            Title = string.IsNullOrWhiteSpace(dbDraft.Title) ? null : dbDraft.Title.Trim(),
            CategoryName = dbDraft.CategoryName,
            ImageCount = dbDraft.ImageUrls.Count,
            UpdatedAt = meta.UpdatedAt
        };
    }

    private async Task PersistDraftAsync(CancellationToken cancellationToken, string? notice = null)
    {
        if (WizardDraftStore.GetChoice(HttpContext.Session) is null && Draft.IsMeaningful)
        {
            WizardDraftStore.SetChoice(HttpContext.Session, WizardDraftStore.ChoiceContinue);
        }

        await WizardDraftStore.PersistAsync(
            HttpContext.Session,
            wizardDrafts,
            GetAccountId(),
            Draft,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(notice))
        {
            TempData["DraftNotice"] = notice.Trim();
        }
    }

    private async Task LoadAccountPhoneAsync(CancellationToken cancellationToken)
    {
        var id = GetAccountId();
        if (id is null)
        {
            RequirePhoneVerification = true;
            AccountPhoneConfirmed = false;
            return;
        }

        var account = await accounts.GetByIdAsync(id.Value, cancellationToken);
        AccountPhone = account?.Phone;
        AccountPhoneConfirmed = account?.PhoneConfirmed == true;
        RequirePhoneVerification = !AccountPhoneConfirmed;
    }

    private void SyncPhoneFromAccount()
    {
        if (!AccountPhoneConfirmed)
        {
            return;
        }

        Draft.PhoneVerified = true;
        var normalized = AccountService.NormalizePhone(AccountPhone);
        if (normalized is not null)
        {
            Draft.Phone = normalized;
        }
    }

    private async Task RefreshAuthCookieAsync(long accountId, CancellationToken cancellationToken)
    {
        var updated = await accounts.GetByIdAsync(accountId, cancellationToken);
        if (updated is null)
        {
            return;
        }

        var existing = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var props = existing.Properties ?? new AuthenticationProperties();
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AuthCookie.CreatePrincipal(updated),
            props);
    }

    private async Task LoadLookupsAsync(CancellationToken cancellationToken)
    {
        if (Step == 1)
        {
            CategoryGroups = await catalog.GetCategoryGroupsAsync(cancellationToken);
            return;
        }

        if (Step == 2 && Draft.CategoryId > 0)
        {
            Attributes = await catalog.GetCategoryAttributesAsync(Draft.CategoryId, cancellationToken);
            Attachments = await catalog.GetAttachmentsByCategoryAsync(Draft.CategoryId, cancellationToken);
            return;
        }

        if (Step == 3)
        {
            Cities = await catalog.GetAllCitiesAsync(cancellationToken);
            if (Draft.CityId > 0)
            {
                Districts = await catalog.GetDistrictsByCityAsync(Draft.CityId, cancellationToken);
            }

            if (Draft.DistrictId > 0)
            {
                Neighborhoods = await catalog.GetNeighborhoodsByDistrictAsync(Draft.DistrictId, cancellationToken);
            }

            var accountId = GetAccountId();
            if (accountId is not null)
            {
                ApprovedCorporateAccounts = await corporate.ListApprovedAsync(accountId.Value, cancellationToken);
                var account = await accounts.GetByIdAsync(accountId.Value, cancellationToken);
                OwnerDisplayName = account?.DisplayName ?? "";

                // Geçersiz / onaysız kurumsal seçimi temizle
                if (Draft.CorporateAccountId is > 0
                    && ApprovedCorporateAccounts.All(c => c.Id != Draft.CorporateAccountId))
                {
                    Draft.CorporateAccountId = null;
                    Draft.CorporateName = null;
                    Draft.SellerType = SellerType.Owner;
                }
            }
        }
    }

    private static int ResolveStep(int? adim, WizardDraft draft, bool requirePhoneVerification)
    {
        var requested = adim is >= 1 and <= 5 ? adim.Value : Math.Clamp(draft.Step, 1, 5);

        if (requested >= 2 && (!draft.HasCategory || !draft.HasIntent))
        {
            return 1;
        }

        if (requested >= 3 && !draft.HasMachine)
        {
            return 2;
        }

        if (requested >= 4 && !draft.HasSaleInfo(requirePhoneVerification))
        {
            return 3;
        }

        if (requested >= 5 && !draft.HasImages)
        {
            return 4;
        }

        return requested;
    }

    /// <summary>True when the user may jump to <paramref name="target"/> (prerequisites filled).</summary>
    internal static bool CanAccessStep(int target, WizardDraft draft, bool requirePhoneVerification)
    {
        if (target is < 1 or > 5)
        {
            return false;
        }

        return ResolveStep(target, draft, requirePhoneVerification) == target;
    }

    /// <summary>True when the step's own form data is complete (shown as done in the stepper).</summary>
    internal static bool IsStepComplete(int step, WizardDraft draft, bool requirePhoneVerification) => step switch
    {
        1 => draft.HasCategory && draft.HasIntent,
        2 => draft.HasMachine,
        3 => draft.HasSaleInfo(requirePhoneVerification),
        4 => draft.HasImages,
        5 => draft.HasImages,
        _ => false
    };

    private string EnsurePublishToken()
    {
        var existing = HttpContext.Session.GetString("ilan-ver-publish-token");
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var token = WebEncoders.Base64UrlEncode(Guid.NewGuid().ToByteArray());
        HttpContext.Session.SetString("ilan-ver-publish-token", token);
        return token;
    }

    private static bool CryptographicOperationsEquals(string a, string b)
    {
        var ba = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        return ba.Length == bb.Length
               && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private async Task<bool> TryLoadEditDraftAsync(
        string adNo,
        long accountId,
        CancellationToken cancellationToken)
    {
        var edit = await listings.GetOwnedForEditAsync(adNo, accountId, cancellationToken);
        if (edit is null)
        {
            return false;
        }

        var specs = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(edit.SpecsJson) ? "{}" : edit.SpecsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    specs[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString() ?? "",
                        JsonValueKind.Number => prop.Value.ToString(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        _ => prop.Value.ToString()
                    };
                }
            }
        }
        catch (JsonException)
        {
            // keep empty specs
        }

        var draft = new WizardDraft
        {
            Step = 2,
            EditingAdNo = edit.AdNo,
            CategoryId = edit.CategoryId,
            CategoryName = edit.CategoryName,
            CapacityMetric = edit.CapacityMetric,
            GroupId = edit.GroupId,
            GroupName = edit.GroupName,
            BrandId = edit.BrandId,
            BrandName = edit.BrandName,
            ModelId = edit.ModelId,
            ModelName = edit.ModelName,
            SerialNo = edit.SerialNo,
            Condition = edit.Condition,
            ModelYear = edit.ModelYear,
            Hours = edit.Hours,
            HoursUnknown = edit.Hours is null,
            Tons = edit.Tons,
            CapacityKg = edit.CapacityKg,
            Horsepower = edit.Horsepower,
            HorsepowerUnknown = edit.Horsepower is null,
            PrimaryIntent = edit.PrimaryIntent,
            Intents = [edit.PrimaryIntent],
            Title = edit.Title,
            Description = edit.Description,
            Specs = specs,
            SpecsJson = edit.SpecsJson,
            AttachmentIds = edit.AttachmentIds.ToList(),
            Price = edit.Price,
            Currency = edit.Currency,
            PriceUnit = edit.PriceUnit,
            IncludesOperator = edit.IncludesOperator,
            SellerType = edit.SellerType,
            CorporateAccountId = edit.CorporateAccountId,
            CorporateName = edit.CorporateName,
            CityId = edit.CityId,
            CityName = edit.CityName,
            DistrictId = edit.DistrictId,
            DistrictName = edit.DistrictName,
            NeighborhoodId = edit.NeighborhoodId,
            NeighborhoodName = edit.NeighborhoodName,
            ImageUrls = edit.ImageUrls.ToList(),
            ImageAssets = edit.ImageUrls.Select(ListingImageAsset.FromUrl).ToList()
        };

        await WizardDraftStore.PersistAsync(
            HttpContext.Session, wizardDrafts, accountId, draft, cancellationToken);
        WizardDraftStore.SetChoice(HttpContext.Session, WizardDraftStore.ChoiceContinue);
        Draft = draft;
        return true;
    }

    private long? GetAccountId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out var id) ? id : null;
    }

    private void SetMeta()
    {
        ViewData["PageKey"] = "wizard";
        ViewData["Title"] = $"Ücretsiz İlan Ver · {StepTitle} | Araç Parkı";
        ViewData["Description"] = "Makineni Araç Parkı’nda ücretsiz yayınla — satılık veya kiralık.";
        ViewData["Robots"] = "noindex, nofollow";
    }
}
