using System.Globalization;
using System.Text.Json;
using AracParki.Application.Catalog.Dtos;
using AracParki.Application.Catalog.Services;
using AracParki.Application.Listings.Dtos;
using AracParki.Application.Listings.Queries;
using AracParki.Application.Listings.Services;
using AracParki.Domain.Equipment;
using AracParki.Domain.Listings;
using AracParki.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace AracParki.Web.Pages.Ilanlar;

public sealed class IndexModel(ListingService listingService, CatalogService catalogService, SiteUrls siteUrls) : PageModel
{
    public ListingSearchQuery Filter { get; private set; } = new();
    public ListingSearchResult Result { get; private set; } = new()
    {
        Items = [],
        TotalCount = 0,
        Page = 1,
        PageSize = 24,
        HasMore = false
    };
    public IReadOnlyList<CategoryOptionDto> Categories { get; private set; } = [];
    public IReadOnlyList<CategorySummaryDto> CategoryNav { get; private set; } = [];
    public IReadOnlyList<BrandOptionDto> Brands { get; private set; } = [];
    public IReadOnlyList<FacetCountDto> BrandFacets { get; private set; } = [];
    public IReadOnlyList<EquipmentModelOptionDto> Models { get; private set; } = [];
    public IReadOnlyList<CityOptionDto> Cities { get; private set; } = [];
    public IReadOnlyList<DistrictOptionDto> Districts { get; private set; } = [];
    public IReadOnlyList<CategoryAttributeDto> FilterableAttributes { get; private set; } = [];
    public IReadOnlyList<AttachmentOptionDto> Attachments { get; private set; } = [];
    public bool ShowCapacityKg { get; private set; }
    public bool ShowRentUnit { get; private set; }

    public IReadOnlyList<FilterChip> ActiveFilters { get; private set; } = [];
    public int ActiveFilterCount => ActiveFilters.Count;
    public string ClearAllUrl { get; private set; } = ListingRoutes.List;
    public string CurrentSearchUrl { get; private set; } = ListingRoutes.List;

    public sealed record FilterChip(string Label, string RemoveUrl);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Filter = ListingRoutes.FromRequest(Request.Query);

        // Bağımsız sorgular tek tek beklenmek yerine paralel çalışır (her repo
        // metodu kendi bağlantısını açtığı için eşzamanlı çalıştırmak güvenlidir).
        var categoriesTask = catalogService.GetAllCategoriesAsync(cancellationToken);
        var categoryNavTask = catalogService.GetCategoriesWithCountsAsync(cancellationToken);
        var citiesTask = catalogService.GetAllCitiesAsync(cancellationToken);
        var attachmentsTask = catalogService.GetAttachmentsAsync(cancellationToken);

        var brandsTask = Filter.CategoryId is > 0
            ? catalogService.GetBrandsByCategoryAsync(Filter.CategoryId.Value, cancellationToken)
            : catalogService.GetAllBrandsAsync(cancellationToken);

        var brandFacetsTask = Filter.CategoryId is > 0
            ? catalogService.GetBrandFacetsAsync(Filter.CategoryId, cancellationToken)
            : Task.FromResult<IReadOnlyList<FacetCountDto>>([]);

        var modelsTask = Filter is { CategoryId: > 0, BrandId: > 0 }
            ? catalogService.GetModelsByBrandCategoryAsync(Filter.BrandId.Value, Filter.CategoryId.Value, cancellationToken)
            : Task.FromResult<IReadOnlyList<EquipmentModelOptionDto>>([]);

        var districtsTask = Filter.CityIds.Count > 0
            ? catalogService.GetDistrictsByCitiesAsync(Filter.CityIds, cancellationToken)
            : Task.FromResult<IReadOnlyList<DistrictOptionDto>>([]);

        var attributesTask = Filter.CategoryId is > 0
            ? catalogService.GetCategoryAttributesAsync(Filter.CategoryId.Value, cancellationToken)
            : Task.FromResult<IReadOnlyList<CategoryAttributeDto>>([]);

        var searchTask = listingService.SearchAsync(Filter, cancellationToken);

        await Task.WhenAll(
            categoriesTask,
            categoryNavTask,
            citiesTask,
            attachmentsTask,
            brandsTask,
            brandFacetsTask,
            modelsTask,
            districtsTask,
            attributesTask,
            searchTask);

        Categories = await categoriesTask;
        CategoryNav = await categoryNavTask;
        Cities = await citiesTask;
        Attachments = await attachmentsTask;
        Brands = await brandsTask;
        BrandFacets = await brandFacetsTask;
        Models = await modelsTask;
        Districts = await districtsTask;
        Result = await searchTask;

        if (Filter.CategoryId is > 0)
        {
            FilterableAttributes = (await attributesTask).Where(a => a.IsFilterable).ToArray();
            var category = Categories.FirstOrDefault(c => c.Id == Filter.CategoryId);
            ShowCapacityKg = category?.CapacityMetric is "capacity_kg";
        }

        ShowRentUnit = Filter.Intent == Domain.Listings.ListingIntent.Kiralik;

        CurrentSearchUrl = Request.Path + Request.QueryString;
        ActiveFilters = BuildActiveFilters();
        ClearAllUrl = BuildClearAllUrl();

        ViewData["PageKey"] = "list";
        var categoryName = Categories.FirstOrDefault(c => c.Id == Filter.CategoryId)?.Name ?? Filter.Category;
        var brandName = Brands.FirstOrDefault(b => b.Id == Filter.BrandId)?.Name;
        var cityName = Filter.CityIds.Count == 1
            ? Cities.FirstOrDefault(c => c.Id == Filter.CityIds[0])?.Name
            : Filter.City;

        var (seoTitle, seoDescription, heading) = ListingSeo.BuildListMeta(
            Filter,
            categoryName,
            brandName,
            cityName,
            Result.TotalCount);

        ViewData["Title"] = seoTitle;
        ViewData["Description"] = seoDescription;
        ViewData["SearchQuery"] = Filter.Query;
        ViewData["ListHeading"] = heading;
        ViewData["CanonicalIncludeQuery"] = false;
        ViewData["CanonicalUrl"] = siteUrls.Absolute(ListingSeo.BuildCanonicalListPath(Filter));
        ViewData["Robots"] = ListingSeo.IsIndexableList(Filter)
            ? ListingSeo.IndexRobots
            : ListingSeo.NoIndexRobots;
        Breadcrumbs.Set(ViewData, siteUrls, BuildBreadcrumbTrail());
    }

    /// <summary>Linear trail matching <c>_ListBreadcrumb</c> (without hover menus).</summary>
    public IReadOnlyList<BreadcrumbItem> BuildBreadcrumbTrail()
    {
        var items = new List<BreadcrumbItem>
        {
            new("Anasayfa", "/")
        };

        var groupLabel = RootNavLabel;
        var showGroup = !string.Equals(groupLabel, "İş Makineleri", StringComparison.Ordinal);
        var rootUrl = RootNavUrl();
        var hasIntent = Filter.Intent is not ListingIntent.All;
        var hasCategory = Filter.CategoryId is > 0 && !string.IsNullOrWhiteSpace(CurrentCategoryName);
        var hasBrand = Filter.BrandId is > 0 && !string.IsNullOrWhiteSpace(CurrentBrandName);

        items.Add(new BreadcrumbItem(groupLabel, rootUrl));
        if (showGroup)
        {
            items.Add(new BreadcrumbItem("İş Makineleri", rootUrl));
        }

        if (hasIntent)
        {
            items.Add(new BreadcrumbItem(ListingIntent.Label(Filter.Intent), IntentNavUrl(Filter.Intent)));
        }

        if (hasCategory)
        {
            items.Add(new BreadcrumbItem(CurrentCategoryName!, CategoryNavUrl(Filter.CategoryId!.Value)));
        }

        if (hasBrand)
        {
            items.Add(new BreadcrumbItem(CurrentBrandName!, BrandNavUrl(Filter.BrandId!.Value)));
        }

        return items;
    }

    public string SpecValue(string key)
        => Filter.SpecValues.TryGetValue(key, out var value) ? value : string.Empty;

    public bool HasAttachment(int id)
        => Filter.AttachmentIds.Contains(id);

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

    public int TotalPages => Result.PageSize <= 0
        ? 1
        : Math.Max(1, (int)Math.Ceiling(Result.TotalCount / (double)Result.PageSize));

    public string PageUrl(int page) => ListingRoutes.ListUrl(CopyFilter(page: Math.Max(1, page)));

    public string SellerTabUrl(string? sellerType)
        => ListingRoutes.ListUrl(CopyFilter(
            page: 1,
            sellerType: sellerType,
            clearSeller: string.IsNullOrWhiteSpace(sellerType)));

    public string RootNavUrl()
        => ListingRoutes.ListUrl(CopyFilter(
            page: 1,
            clearIntent: true,
            clearCategory: true,
            clearBrand: true,
            clearModel: true,
            clearCategoryName: true));

    public string IntentNavUrl(string intent)
        => ListingRoutes.ListUrl(CopyFilter(
            page: 1,
            intent: intent,
            clearCategory: true,
            clearBrand: true,
            clearModel: true,
            clearCategoryName: true));

    public string CategoryNavUrl(int categoryId)
        => ListingRoutes.ListUrl(CopyFilter(
            page: 1,
            categoryId: categoryId,
            clearBrand: true,
            clearModel: true,
            clearCategoryName: true));

    public string BrandNavUrl(int brandId)
        => ListingRoutes.ListUrl(CopyFilter(page: 1, brandId: brandId, clearModel: true));

    public string? CurrentCategoryName =>
        CategoryNav.FirstOrDefault(c => c.Id == Filter.CategoryId)?.Name
        ?? Categories.FirstOrDefault(c => c.Id == Filter.CategoryId)?.Name;

    public string? CurrentBrandName =>
        Brands.FirstOrDefault(b => b.Id == Filter.BrandId)?.Name;

    public string ResultContextLabel
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Filter.Query))
            {
                return Filter.Query.Trim();
            }

            var brand = CurrentBrandName;
            var category = CurrentCategoryName;
            if (!string.IsNullOrWhiteSpace(brand) && !string.IsNullOrWhiteSpace(category))
            {
                return $"{brand} {category}";
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                return category;
            }

            if (!string.IsNullOrWhiteSpace(brand))
            {
                return brand;
            }

            if (Filter.Intent == Domain.Listings.ListingIntent.Satilik)
            {
                return "Satılık iş makineleri";
            }

            if (Filter.Intent == Domain.Listings.ListingIntent.Kiralik)
            {
                return "Kiralık iş makineleri";
            }

            return "İş makineleri";
        }
    }

    public string RootNavLabel =>
        CategoryNav.FirstOrDefault(c => c.Id == Filter.CategoryId)?.GroupName
        ?? CategoryNav.Select(c => c.GroupName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
        ?? "İş Makineleri";

    public IReadOnlyList<int> VisiblePages
    {
        get
        {
            var total = TotalPages;
            var current = Result.Page;
            var pages = new SortedSet<int> { 1 };
            if (total > 1)
            {
                pages.Add(total);
            }

            for (var i = current - 2; i <= current + 2; i++)
            {
                if (i is >= 1 && i <= total)
                {
                    pages.Add(i);
                }
            }

            return pages.ToArray();
        }
    }

    private IReadOnlyList<FilterChip> BuildActiveFilters()
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        var chips = new List<FilterChip>();

        if (!string.IsNullOrWhiteSpace(Filter.Query))
        {
            chips.Add(new($"Arama: {Filter.Query}", BuildUrlWithout("q")));
        }

        if (!string.IsNullOrWhiteSpace(Filter.Condition))
        {
            var label = Filter.Condition == EquipmentCondition.Used
                ? "İkinci el"
                : Filter.Condition == EquipmentCondition.New ? "Sıfır" : Filter.Condition;
            chips.Add(new($"Durum: {label}", BuildUrlWithout("durum")));
        }

        if (Filter.CityIds.Count > 0)
        {
            var first = Cities.FirstOrDefault(c => c.Id == Filter.CityIds[0])?.Name ?? "İl";
            var label = Filter.CityIds.Count == 1 ? first : $"{first} +{Filter.CityIds.Count - 1}";
            chips.Add(new($"İl: {label}", BuildUrlWithout("ilId", "ilceId")));
        }

        if (Filter.DistrictIds.Count > 0)
        {
            var first = Districts.FirstOrDefault(d => d.Id == Filter.DistrictIds[0])?.Name ?? "İlçe";
            var label = Filter.DistrictIds.Count == 1 ? first : $"{first} +{Filter.DistrictIds.Count - 1}";
            chips.Add(new($"İlçe: {label}", BuildUrlWithout("ilceId")));
        }

        if (Filter.PriceMin is not null || Filter.PriceMax is not null)
        {
            chips.Add(new(RangeLabel("Fiyat", Filter.PriceMin, Filter.PriceMax, " TL", tr), BuildUrlWithout("fiyatMin", "fiyatMax")));
        }

        if (Filter.YearMin is not null || Filter.YearMax is not null)
        {
            var body = (Filter.YearMin, Filter.YearMax) switch
            {
                (not null, not null) => $"{Filter.YearMin} – {Filter.YearMax}",
                (not null, null) => $"{Filter.YearMin}+",
                (null, not null) => $"≤ {Filter.YearMax}",
                _ => string.Empty
            };
            chips.Add(new($"Yıl: {body}", BuildUrlWithout("yilMin", "yilMax")));
        }

        if (Filter.HoursMin is not null || Filter.HoursMax is not null)
        {
            chips.Add(new(RangeLabel("Saat", Filter.HoursMin, Filter.HoursMax, "", tr), BuildUrlWithout("saatMin", "saatMax")));
        }

        if (Filter.HorsepowerMin is not null || Filter.HorsepowerMax is not null)
        {
            chips.Add(new(RangeLabel("HP", Filter.HorsepowerMin, Filter.HorsepowerMax, "", tr), BuildUrlWithout("hpMin", "hpMax")));
        }

        if (Filter.CapacityKgMin is not null || Filter.CapacityKgMax is not null)
        {
            chips.Add(new(RangeLabel("Kapasite", Filter.CapacityKgMin, Filter.CapacityKgMax, " kg", tr), BuildUrlWithout("kgMin", "kgMax")));
        }

        if (Filter.WeightMin is not null || Filter.WeightMax is not null)
        {
            chips.Add(new(RangeLabel("Tonaj", Filter.WeightMin, Filter.WeightMax, " t", tr, "0.##"), BuildUrlWithout("tonMin", "tonMax")));
        }

        if (!string.IsNullOrWhiteSpace(Filter.PriceUnit))
        {
            chips.Add(new($"Kira: {PriceUnit.Label(Filter.PriceUnit)}", BuildUrlWithout("birim")));
        }

        if (Filter.IncludesOperator is true)
        {
            chips.Add(new("Operatör dahil", BuildUrlWithout("operator")));
        }

        if (Filter.VerifiedOnly)
        {
            chips.Add(new("Doğrulanmış satıcı", BuildUrlWithout("dogrulanmis")));
        }

        if (Filter.AttachmentIds.Count > 0)
        {
            var first = Attachments.FirstOrDefault(a => a.Id == Filter.AttachmentIds[0])?.Name ?? "Ekipman";
            var label = Filter.AttachmentIds.Count == 1 ? first : $"{first} +{Filter.AttachmentIds.Count - 1}";
            chips.Add(new($"Ekipman: {label}", BuildUrlWithout("ekipman")));
        }

        foreach (var (key, value) in Filter.SpecValues)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var attr = FilterableAttributes.FirstOrDefault(a => a.Key == key);
            var attrLabel = attr?.Label ?? key;
            var valueLabel = attr?.DataType switch
            {
                "bool" => string.Empty,
                "number" => value + (string.IsNullOrWhiteSpace(attr?.Unit) ? "" : " " + attr!.Unit) + "+",
                _ => ListingRoutes.SpecOptionLabel(value)
            };
            var chipLabel = string.IsNullOrEmpty(valueLabel) ? attrLabel : $"{attrLabel}: {valueLabel}";
            chips.Add(new(chipLabel, BuildUrlWithout(ListingRoutes.SpecQueryPrefix + key)));
        }

        return chips;
    }

    private static string RangeLabel(string prefix, decimal? min, decimal? max, string suffix, CultureInfo tr, string format = "N0")
    {
        string Fmt(decimal v) => v.ToString(format, tr);
        var body = (min, max) switch
        {
            (not null, not null) => $"{Fmt(min.Value)} – {Fmt(max.Value)}",
            (not null, null) => $"{Fmt(min.Value)}+",
            (null, not null) => $"≤ {Fmt(max.Value)}",
            _ => string.Empty
        };
        return $"{prefix}: {body}{suffix}";
    }

    private static string RangeLabel(string prefix, int? min, int? max, string suffix, CultureInfo tr, string format = "N0")
        => RangeLabel(prefix, (decimal?)min, (decimal?)max, suffix, tr, format);

    private string BuildUrlWithout(params string[] removeKeys)
    {
        var remove = new HashSet<string>(removeKeys, StringComparer.Ordinal);
        var dict = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var kv in Request.Query)
        {
            if (remove.Contains(kv.Key) || string.Equals(kv.Key, "sayfa", StringComparison.Ordinal))
            {
                continue;
            }

            var value = kv.Value.ToString();
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            dict[kv.Key] = value;
        }

        return dict.Count == 0 ? ListingRoutes.List : QueryHelpers.AddQueryString(ListingRoutes.List, dict);
    }

    private string BuildClearAllUrl()
    {
        var keep = new HashSet<string>(
            ["tip", "kategoriId", "kategori", "markaId", "modelId", "satici", "sort"],
            StringComparer.Ordinal);
        var dict = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var kv in Request.Query)
        {
            if (!keep.Contains(kv.Key))
            {
                continue;
            }

            var value = kv.Value.ToString();
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            dict[kv.Key] = value;
        }

        return dict.Count == 0 ? ListingRoutes.List : QueryHelpers.AddQueryString(ListingRoutes.List, dict);
    }

    private ListingSearchQuery CopyFilter(
        int? page = null,
        string? intent = null,
        bool clearIntent = false,
        string? sellerType = null,
        bool clearSeller = false,
        int? categoryId = null,
        bool clearCategory = false,
        bool clearCategoryName = false,
        int? brandId = null,
        bool clearBrand = false,
        bool clearModel = false)
    {
        return new ListingSearchQuery
        {
            Intent = clearIntent
                ? Domain.Listings.ListingIntent.All
                : intent ?? Filter.Intent,
            CategoryId = clearCategory ? null : categoryId ?? Filter.CategoryId,
            BrandId = clearBrand ? null : brandId ?? Filter.BrandId,
            ModelId = clearModel ? null : Filter.ModelId,
            CityIds = Filter.CityIds,
            DistrictIds = Filter.DistrictIds,
            Category = clearCategoryName || clearCategory ? null : Filter.Category,
            City = Filter.City,
            Condition = Filter.Condition,
            SellerType = clearSeller ? null : sellerType ?? Filter.SellerType,
            YearMin = Filter.YearMin,
            YearMax = Filter.YearMax,
            HoursMin = Filter.HoursMin,
            HoursMax = Filter.HoursMax,
            WeightMin = Filter.WeightMin,
            WeightMax = Filter.WeightMax,
            PriceMin = Filter.PriceMin,
            PriceMax = Filter.PriceMax,
            HorsepowerMin = Filter.HorsepowerMin,
            HorsepowerMax = Filter.HorsepowerMax,
            CapacityKgMin = Filter.CapacityKgMin,
            CapacityKgMax = Filter.CapacityKgMax,
            IncludesOperator = Filter.IncludesOperator,
            PriceUnit = Filter.PriceUnit,
            VerifiedOnly = Filter.VerifiedOnly,
            AttachmentIds = Filter.AttachmentIds,
            SpecValues = Filter.SpecValues,
            SpecsFilterJson = Filter.SpecsFilterJson,
            SpecMinJson = Filter.SpecMinJson,
            Query = Filter.Query,
            Sort = Filter.Sort,
            Page = page ?? Filter.Page,
            PageSize = Filter.PageSize
        };
    }
}
