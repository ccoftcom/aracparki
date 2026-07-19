using System.Text.Json;
using AracParki.Application.Catalog.Dtos;
using AracParki.Application.Catalog.Services;
using AracParki.Application.Listings.Dtos;
using AracParki.Application.Listings.Queries;
using AracParki.Application.Listings.Services;
using AracParki.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AracParki.Web.Pages.Ilanlar;

public sealed class IndexModel(ListingService listingService, CatalogService catalogService) : PageModel
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
    public IReadOnlyList<BrandOptionDto> Brands { get; private set; } = [];
    public IReadOnlyList<EquipmentModelOptionDto> Models { get; private set; } = [];
    public IReadOnlyList<CityOptionDto> Cities { get; private set; } = [];
    public IReadOnlyList<DistrictOptionDto> Districts { get; private set; } = [];
    public IReadOnlyList<CategoryAttributeDto> FilterableAttributes { get; private set; } = [];
    public IReadOnlyList<AttachmentOptionDto> Attachments { get; private set; } = [];
    public bool ShowCapacityKg { get; private set; }
    public bool ShowRentUnit { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Filter = ListingRoutes.FromRequest(Request.Query);
        Categories = await catalogService.GetAllCategoriesAsync(cancellationToken);
        Cities = await catalogService.GetAllCitiesAsync(cancellationToken);
        Attachments = await catalogService.GetAttachmentsAsync(cancellationToken);
        Brands = Filter.CategoryId is > 0
            ? await catalogService.GetBrandsByCategoryAsync(Filter.CategoryId.Value, cancellationToken)
            : await catalogService.GetAllBrandsAsync(cancellationToken);

        if (Filter.CategoryId is > 0 && Filter.BrandId is > 0)
        {
            Models = await catalogService.GetModelsByBrandCategoryAsync(
                Filter.BrandId.Value, Filter.CategoryId.Value, cancellationToken);
        }

        if (Filter.CityId is > 0)
        {
            Districts = await catalogService.GetDistrictsByCityAsync(Filter.CityId.Value, cancellationToken);
        }

        if (Filter.CategoryId is > 0)
        {
            var attrs = await catalogService.GetCategoryAttributesAsync(Filter.CategoryId.Value, cancellationToken);
            FilterableAttributes = attrs.Where(a => a.IsFilterable).ToArray();
            var category = Categories.FirstOrDefault(c => c.Id == Filter.CategoryId);
            ShowCapacityKg = category?.CapacityMetric is "capacity_kg";
        }

        ShowRentUnit = Filter.Intent == Domain.Listings.ListingIntent.Kiralik;

        Result = await listingService.SearchAsync(Filter, cancellationToken);

        var categoryName = Categories.FirstOrDefault(c => c.Id == Filter.CategoryId)?.Name ?? Filter.Category;
        var heading = string.IsNullOrWhiteSpace(categoryName)
            ? "İş Makinesi İlanları"
            : $"{categoryName} İlanları";

        ViewData["PageKey"] = "list";
        ViewData["Title"] = $"{categoryName ?? "İş Makinesi"} İlanları | Araç Parkı";
        ViewData["Description"] = "İş makinesi ilanları — satılık ve kiralık. Araç Parkı.";
        ViewData["SearchQuery"] = Filter.Query;
        ViewData["ListHeading"] = heading;
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

    public string PageUrl(int page)
    {
        var q = new ListingSearchQuery
        {
            Intent = Filter.Intent,
            CategoryId = Filter.CategoryId,
            BrandId = Filter.BrandId,
            ModelId = Filter.ModelId,
            CityId = Filter.CityId,
            DistrictId = Filter.DistrictId,
            Category = Filter.Category,
            City = Filter.City,
            Condition = Filter.Condition,
            SellerType = Filter.SellerType,
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
            Page = Math.Max(1, page),
            PageSize = Filter.PageSize
        };
        return ListingRoutes.ListUrl(q);
    }
}
