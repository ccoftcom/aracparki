using System.Security.Claims;
using System.Text.Json;
using AracParki.Application.Accounts.Services;
using AracParki.Application.Catalog.Dtos;
using AracParki.Application.Catalog.Services;
using AracParki.Application.Listings;
using AracParki.Application.Listings.Commands;
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

namespace AracParki.Web.Pages.IlanVer;

[Authorize]
public sealed class IndexModel(
    CatalogService catalog,
    AccountService accounts,
    ListingCommandService listingCommands,
    ILogger<IndexModel> logger) : PageModel
{
    private static readonly string[] StepTitles =
    [
        "Kategori seçimi",
        "Makine",
        "Satış bilgisi",
        "Görseller",
        "Önizleme"
    ];

    public WizardDraft Draft { get; private set; } = new();
    public int Step { get; private set; } = 1;
    public string StepTitle => StepTitles[Math.Clamp(Step, 1, 5) - 1];
    public string? FormError { get; private set; }
    public bool RequirePhone { get; private set; }
    public string? AccountPhone { get; private set; }

    public IReadOnlyList<CategoryGroupDto> CategoryGroups { get; private set; } = [];
    public IReadOnlyList<BrandOptionDto> Brands { get; private set; } = [];
    public IReadOnlyList<EquipmentModelOptionDto> Models { get; private set; } = [];
    public IReadOnlyList<CategoryAttributeDto> Attributes { get; private set; } = [];
    public IReadOnlyList<AttachmentOptionDto> Attachments { get; private set; } = [];
    public IReadOnlyList<CityOptionDto> Cities { get; private set; } = [];
    public IReadOnlyList<DistrictOptionDto> Districts { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int? adim, CancellationToken cancellationToken)
    {
        await LoadAccountPhoneAsync(cancellationToken);
        Draft = WizardDraftStore.Get(HttpContext.Session);
        Step = ResolveStep(adim, Draft);
        await LoadLookupsAsync(cancellationToken);
        SetMeta();
        return Page();
    }

    public async Task<IActionResult> OnPostCascadeAsync(
        int categoryId,
        int brandId,
        int? modelId,
        string? modelName,
        int modelYear,
        int? groupId,
        string? groupName,
        CancellationToken cancellationToken)
    {
        await LoadAccountPhoneAsync(cancellationToken);
        Draft = WizardDraftStore.Get(HttpContext.Session);

        var categories = await catalog.GetAllCategoriesAsync(cancellationToken);
        var category = categories.FirstOrDefault(c => c.Id == categoryId);
        if (category is null)
        {
            FormError = "Kategori seçimi geçersiz.";
            Step = 1;
            await LoadLookupsAsync(cancellationToken);
            SetMeta();
            return Page();
        }

        var brands = await catalog.GetBrandsByCategoryAsync(categoryId, cancellationToken);
        var brand = brands.FirstOrDefault(b => b.Id == brandId);
        if (brand is null)
        {
            FormError = "Marka seçimi geçersiz — cascader’da markayı seç.";
            Step = 1;
            await LoadLookupsAsync(cancellationToken);
            SetMeta();
            return Page();
        }

        string resolvedModelName = modelName?.Trim() ?? "";
        int? resolvedModelId = modelId is > 0 ? modelId : null;
        if (resolvedModelId is not null)
        {
            var models = await catalog.GetModelsByBrandCategoryAsync(brandId, categoryId, cancellationToken);
            var model = models.FirstOrDefault(m => m.Id == resolvedModelId);
            if (model is null)
            {
                FormError = "Model seçimi geçersiz.";
                Step = 1;
                await LoadLookupsAsync(cancellationToken);
                SetMeta();
                return Page();
            }

            resolvedModelName = model.Name;
        }

        if (string.IsNullOrWhiteSpace(resolvedModelName))
        {
            FormError = "Model seçimi zorunlu.";
            Step = 1;
            await LoadLookupsAsync(cancellationToken);
            SetMeta();
            return Page();
        }

        if (modelYear is < 1950 or > 2100)
        {
            FormError = "Model yılı geçersiz.";
            Step = 1;
            await LoadLookupsAsync(cancellationToken);
            SetMeta();
            return Page();
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
        Draft.Step = 2;
        WizardDraftStore.Save(HttpContext.Session, Draft);
        return RedirectToPage(new { adim = 2 });
    }

    public async Task<IActionResult> OnPostMachineAsync(
        int brandId,
        int? modelId,
        string? modelName,
        string? condition,
        int modelYear,
        int hours,
        decimal tons,
        int? capacityKg,
        int horsepower,
        string? title,
        string? description,
        [FromForm] Dictionary<string, string>? specs,
        int[]? attachmentIds,
        CancellationToken cancellationToken)
    {
        await LoadAccountPhoneAsync(cancellationToken);
        Draft = WizardDraftStore.Get(HttpContext.Session);
        if (!Draft.HasCategory)
        {
            return RedirectToPage(new { adim = 1 });
        }

        var brands = await catalog.GetBrandsByCategoryAsync(Draft.CategoryId, cancellationToken);
        var brand = brands.FirstOrDefault(b => b.Id == brandId);
        if (brand is null)
        {
            FormError = "Marka seçimi geçersiz.";
            Step = 2;
            await LoadLookupsAsync(cancellationToken);
            SetMeta();
            return Page();
        }

        string resolvedModelName = modelName?.Trim() ?? "";
        int? resolvedModelId = modelId is > 0 ? modelId : null;
        if (resolvedModelId is not null)
        {
            var models = await catalog.GetModelsByBrandCategoryAsync(
                brandId, Draft.CategoryId, cancellationToken);
            var model = models.FirstOrDefault(m => m.Id == resolvedModelId);
            if (model is null)
            {
                FormError = "Model seçimi geçersiz.";
                Step = 2;
                await LoadLookupsAsync(cancellationToken);
                SetMeta();
                return Page();
            }

            resolvedModelName = model.Name;
        }

        var categoryAttrs = await catalog.GetCategoryAttributesAsync(Draft.CategoryId, cancellationToken);
        var (specsOk, specsError, specsJson, storedRaw) = SpecsJsonBuilder.TryBuild(specs, categoryAttrs);
        if (!specsOk)
        {
            FormError = specsError ?? "Özellikler geçersiz.";
            Step = 2;
            await LoadLookupsAsync(cancellationToken);
            SetMeta();
            return Page();
        }

        var allAttachments = await catalog.GetAttachmentsAsync(cancellationToken);
        var allowedAttachmentIds = allAttachments.Select(a => a.Id).ToHashSet();
        var selectedAttachments = (attachmentIds ?? [])
            .Where(id => allowedAttachmentIds.Contains(id))
            .Distinct()
            .Take(20)
            .ToList();

        Draft.BrandId = brand.Id;
        Draft.BrandName = brand.Name;
        Draft.ModelId = resolvedModelId;
        Draft.ModelName = resolvedModelName;
        Draft.Condition = EquipmentCondition.Known.Contains(condition ?? "")
            ? condition!
            : EquipmentCondition.Used;
        Draft.ModelYear = modelYear;
        Draft.Hours = hours;
        Draft.Tons = tons;
        Draft.CapacityKg = Draft.CapacityMetric == "capacity_kg" ? capacityKg : null;
        Draft.Horsepower = horsepower;
        Draft.Title = string.IsNullOrWhiteSpace(title) ? Draft.SuggestedTitle() : title.Trim();
        Draft.Description = description?.Trim() ?? "";
        Draft.Specs = storedRaw;
        Draft.SpecsJson = specsJson;
        Draft.AttachmentIds = selectedAttachments;

        if (!Draft.HasMachine)
        {
            FormError = "Makine bilgilerini kontrol et — zorunlu alanlar eksik.";
            Step = 2;
            await LoadLookupsAsync(cancellationToken);
            SetMeta();
            return Page();
        }

        if (Draft.CapacityMetric == "capacity_kg" && Draft.CapacityKg is not > 0)
        {
            FormError = "Kapasite (kg) zorunlu.";
            Step = 2;
            await LoadLookupsAsync(cancellationToken);
            SetMeta();
            return Page();
        }

        Draft.Step = 3;
        WizardDraftStore.Save(HttpContext.Session, Draft);
        return RedirectToPage(new { adim = 3 });
    }

    public async Task<IActionResult> OnPostSaleAsync(
        string? primaryIntent,
        string[]? intents,
        decimal price,
        string? priceUnit,
        bool includesOperator,
        int cityId,
        int districtId,
        string? phone,
        CancellationToken cancellationToken)
    {
        await LoadAccountPhoneAsync(cancellationToken);
        Draft = WizardDraftStore.Get(HttpContext.Session);
        if (!Draft.HasMachine)
        {
            return RedirectToPage(new { adim = Draft.HasCategory ? 2 : 1 });
        }

        var selectedIntents = (intents ?? [])
            .Where(i => i is ListingIntent.Satilik or ListingIntent.Kiralik)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (selectedIntents.Count == 0)
        {
            FormError = "En az bir ilan tipi seç (Satılık ve/veya Kiralık).";
            Step = 3;
            await LoadLookupsAsync(cancellationToken);
            SetMeta();
            return Page();
        }

        if (primaryIntent is not (ListingIntent.Satilik or ListingIntent.Kiralik)
            || !selectedIntents.Contains(primaryIntent))
        {
            FormError = "Birincil tip, işaretlediğin tiplerden biri olmalı.";
            Step = 3;
            await LoadLookupsAsync(cancellationToken);
            SetMeta();
            return Page();
        }

        var cities = await catalog.GetAllCitiesAsync(cancellationToken);
        var city = cities.FirstOrDefault(c => c.Id == cityId);
        DistrictOptionDto? district = null;
        if (city is not null)
        {
            var districts = await catalog.GetDistrictsByCityAsync(cityId, cancellationToken);
            district = districts.FirstOrDefault(d => d.Id == districtId);
        }

        Draft.PrimaryIntent = primaryIntent;
        Draft.Intents = selectedIntents;
        Draft.Price = price;
        Draft.PriceUnit = string.IsNullOrWhiteSpace(priceUnit) ? null : priceUnit.Trim();
        Draft.IncludesOperator = includesOperator && selectedIntents.Contains(ListingIntent.Kiralik);
        Draft.CityId = city?.Id ?? 0;
        Draft.CityName = city?.Name;
        Draft.DistrictId = district?.Id ?? 0;
        Draft.DistrictName = district?.Name;

        if (RequirePhone)
        {
            var normalized = AccountService.NormalizePhone(phone);
            if (normalized is null)
            {
                FormError = "Geçerli bir telefon numarası gir (10–15 rakam).";
                Draft.Phone = phone?.Trim() ?? "";
                Step = 3;
                await LoadLookupsAsync(cancellationToken);
                SetMeta();
                return Page();
            }

            Draft.Phone = normalized;
        }
        else
        {
            Draft.Phone = AccountService.NormalizePhone(AccountPhone) ?? Draft.Phone;
        }

        if (city is null || district is null)
        {
            FormError = "İl ve ilçe seçimi zorunlu.";
            Step = 3;
            await LoadLookupsAsync(cancellationToken);
            SetMeta();
            return Page();
        }

        if (!Draft.HasSaleInfo(RequirePhone))
        {
            FormError = "Satış bilgilerini kontrol et.";
            Step = 3;
            await LoadLookupsAsync(cancellationToken);
            SetMeta();
            return Page();
        }

        Draft.Step = 4;
        WizardDraftStore.Save(HttpContext.Session, Draft);
        return RedirectToPage(new { adim = 4 });
    }

    public async Task<IActionResult> OnPostImagesAsync(string[]? imageUrls, CancellationToken cancellationToken)
    {
        await LoadAccountPhoneAsync(cancellationToken);
        Draft = WizardDraftStore.Get(HttpContext.Session);
        if (!Draft.HasSaleInfo(RequirePhone))
        {
            return RedirectToPage(new { adim = 3 });
        }

        var submitted = (imageUrls ?? [])
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .ToList();

        if (submitted.Count > 8)
        {
            FormError = "En fazla 8 görsel ekleyebilirsin.";
            Step = 4;
            await LoadLookupsAsync(cancellationToken);
            SetMeta();
            return Page();
        }

        Draft.ImageUrls = submitted
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        if (Draft.ImageUrls.Count == 0)
        {
            FormError = "En az 1 görsel URL gir.";
            Step = 4;
            await LoadLookupsAsync(cancellationToken);
            SetMeta();
            return Page();
        }

        if (Draft.ImageUrls.Any(u => !Uri.TryCreate(u, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            FormError = "Görseller https:// ile başlamalı (karma içerik engeli).";
            Step = 4;
            await LoadLookupsAsync(cancellationToken);
            SetMeta();
            return Page();
        }

        Draft.Step = 5;
        WizardDraftStore.Save(HttpContext.Session, Draft);
        return RedirectToPage(new { adim = 5 });
    }

    [EnableRateLimiting("listing-publish")]
    public async Task<IActionResult> OnPostPublishAsync(CancellationToken cancellationToken)
    {
        await LoadAccountPhoneAsync(cancellationToken);
        Draft = WizardDraftStore.Get(HttpContext.Session);
        Step = 5;

        if (!Draft.HasCategory || !Draft.HasMachine || !Draft.HasSaleInfo(RequirePhone) || !Draft.HasImages)
        {
            FormError = "İlan henüz yayınlanmaya hazır değil. Adımları tamamla.";
            await LoadLookupsAsync(cancellationToken);
            SetMeta();
            return Page();
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

        var phone = RequirePhone
            ? AccountService.NormalizePhone(Draft.Phone)
            : AccountService.NormalizePhone(account.Phone ?? Draft.Phone);

        if (phone is null)
        {
            FormError = "Geçerli bir telefon numarası gerekli. Satış adımına dönüp düzelt.";
            await LoadLookupsAsync(cancellationToken);
            SetMeta();
            return Page();
        }

        if (RequirePhone || string.IsNullOrWhiteSpace(account.Phone))
        {
            var (ok, error, updated) = await accounts.UpdatePhoneAsync(accountId.Value, phone, cancellationToken);
            if (!ok || updated is null)
            {
                FormError = error ?? "Telefon güncellenemedi.";
                await LoadLookupsAsync(cancellationToken);
                SetMeta();
                return Page();
            }

            var existing = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var props = existing.Properties ?? new AuthenticationProperties();
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                AuthCookie.CreatePrincipal(updated),
                props);
            account = updated;
        }

        var command = new CreatePublishedListingCommand
        {
            AccountId = account.Id,
            SellerDisplayName = account.DisplayName,
            Phone = phone,
            CategoryId = Draft.CategoryId,
            BrandId = Draft.BrandId,
            ModelId = Draft.ModelId,
            ModelName = Draft.ModelName,
            CityId = Draft.CityId,
            DistrictId = Draft.DistrictId,
            PrimaryIntent = Draft.PrimaryIntent,
            Intents = Draft.Intents.ToArray(),
            Condition = Draft.Condition,
            ModelYear = Draft.ModelYear,
            Hours = Draft.Hours,
            Tons = Draft.Tons,
            CapacityKg = Draft.CapacityKg,
            Horsepower = Draft.Horsepower,
            Price = Draft.Price,
            PriceUnit = Draft.PriceUnit,
            IncludesOperator = Draft.IncludesOperator,
            Title = Draft.Title,
            Description = Draft.Description,
            SpecsJson = string.IsNullOrWhiteSpace(Draft.SpecsJson) ? "{}" : Draft.SpecsJson,
            ImageUrls = Draft.ImageUrls,
            AttachmentIds = Draft.AttachmentIds
        };

        try
        {
            var adNo = await listingCommands.CreatePublishedAsync(command, cancellationToken);
            WizardDraftStore.Clear(HttpContext.Session);
            return Redirect(ListingRoutes.Detail(adNo));
        }
        catch (ValidationException ex)
        {
            FormError = ex.Errors.FirstOrDefault()?.ErrorMessage ?? "İlan doğrulanamadı.";
            logger.LogWarning(ex, "Listing publish validation failed");
            await LoadLookupsAsync(cancellationToken);
            SetMeta();
            return Page();
        }
        catch (Exception ex)
        {
            FormError = "İlan yayınlanırken bir hata oluştu. Lütfen tekrar dene.";
            logger.LogError(ex, "Listing publish failed for account {AccountId}", account.Id);
            await LoadLookupsAsync(cancellationToken);
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

    private async Task LoadAccountPhoneAsync(CancellationToken cancellationToken)
    {
        var id = GetAccountId();
        if (id is null)
        {
            RequirePhone = true;
            return;
        }

        var account = await accounts.GetByIdAsync(id.Value, cancellationToken);
        AccountPhone = account?.Phone;
        RequirePhone = string.IsNullOrWhiteSpace(AccountPhone);
    }

    private async Task LoadLookupsAsync(CancellationToken cancellationToken)
    {
        CategoryGroups = await catalog.GetCategoryGroupsAsync(cancellationToken);
        Cities = await catalog.GetAllCitiesAsync(cancellationToken);
        Attachments = await catalog.GetAttachmentsAsync(cancellationToken);

        if (Draft.CategoryId > 0)
        {
            Brands = await catalog.GetBrandsByCategoryAsync(Draft.CategoryId, cancellationToken);
            Attributes = await catalog.GetCategoryAttributesAsync(Draft.CategoryId, cancellationToken);

            if (Draft.BrandId > 0)
            {
                Models = await catalog.GetModelsByBrandCategoryAsync(
                    Draft.BrandId, Draft.CategoryId, cancellationToken);
            }
        }

        if (Draft.CityId > 0)
        {
            Districts = await catalog.GetDistrictsByCityAsync(Draft.CityId, cancellationToken);
        }
    }

    private static int ResolveStep(int? adim, WizardDraft draft)
    {
        var requested = adim is >= 1 and <= 5 ? adim.Value : Math.Clamp(draft.Step, 1, 5);

        if (requested >= 2 && !draft.HasCategory)
        {
            return 1;
        }

        if (requested >= 3 && !draft.HasMachine)
        {
            return 2;
        }

        if (requested >= 4 && (draft.Price <= 0 || draft.CityId <= 0 || draft.DistrictId <= 0))
        {
            return 3;
        }

        if (requested >= 5 && !draft.HasImages)
        {
            return 4;
        }

        return requested;
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
    }
}
