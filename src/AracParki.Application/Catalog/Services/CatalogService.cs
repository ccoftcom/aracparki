using AracParki.Application.Catalog.Dtos;
using AracParki.Application.Common;
using Microsoft.Extensions.Caching.Distributed;

namespace AracParki.Application.Catalog.Services;

public sealed class CatalogService(ICatalogQuery catalogQuery, IDistributedCache cache)
{
    private static readonly TimeSpan HotTtl = TimeSpan.FromMinutes(5);

    public Task<IReadOnlyList<CategorySummaryDto>> GetCategoriesWithCountsAsync(CancellationToken cancellationToken)
        => CacheAsync("catalog:categories-counts", ct => catalogQuery.GetCategoriesWithCountsAsync(ct), cancellationToken);

    public Task<IReadOnlyList<CitySummaryDto>> GetPopularCitiesAsync(CancellationToken cancellationToken)
        => CacheAsync("catalog:popular-cities", ct => catalogQuery.GetPopularCitiesAsync(ct), cancellationToken);

    public Task<IReadOnlyList<CityOptionDto>> GetAllCitiesAsync(CancellationToken cancellationToken)
        => CacheAsync("catalog:cities", ct => catalogQuery.GetAllCitiesAsync(ct), cancellationToken);

    public Task<IReadOnlyList<CategoryOptionDto>> GetAllCategoriesAsync(CancellationToken cancellationToken)
        => CacheAsync("catalog:categories", ct => catalogQuery.GetAllCategoriesAsync(ct), cancellationToken);

    public Task<IReadOnlyList<CategoryGroupDto>> GetCategoryGroupsAsync(CancellationToken cancellationToken)
        => CacheAsync("catalog:category-groups", ct => catalogQuery.GetCategoryGroupsAsync(ct), cancellationToken);

    public Task<IReadOnlyList<BrandOptionDto>> GetAllBrandsAsync(CancellationToken cancellationToken)
        => CacheAsync("catalog:brands", ct => catalogQuery.GetAllBrandsAsync(ct), cancellationToken);

    public Task<IReadOnlyList<BrandOptionDto>> GetBrandsByCategoryAsync(int categoryId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(categoryId);
        return CacheAsync(
            $"catalog:brands-by-cat:{categoryId}",
            ct => catalogQuery.GetBrandsByCategoryAsync(categoryId, ct),
            cancellationToken);
    }

    public Task<IReadOnlyList<EquipmentModelOptionDto>> GetModelsByBrandCategoryAsync(
        int brandId,
        int categoryId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(brandId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(categoryId);
        return catalogQuery.GetModelsByBrandCategoryAsync(brandId, categoryId, cancellationToken);
    }

    public Task<EquipmentModelOptionDto?> GetModelByIdAsync(int modelId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(modelId);
        return catalogQuery.GetModelByIdAsync(modelId, cancellationToken);
    }

    public Task<IReadOnlyList<CategoryAttributeDto>> GetCategoryAttributesAsync(int categoryId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(categoryId);
        return CacheAsync(
            $"catalog:attrs:{categoryId}",
            ct => catalogQuery.GetCategoryAttributesAsync(categoryId, ct),
            cancellationToken);
    }

    public Task<IReadOnlyList<AttachmentOptionDto>> GetAttachmentsAsync(CancellationToken cancellationToken)
        => CacheAsync("catalog:attachments", ct => catalogQuery.GetAttachmentsAsync(ct), cancellationToken);

    public Task<IReadOnlyList<AttachmentOptionDto>> GetAttachmentsByCategoryAsync(
        int categoryId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(categoryId);
        return catalogQuery.GetAttachmentsByCategoryAsync(categoryId, cancellationToken);
    }

    public Task<IReadOnlyList<FacetCountDto>> GetBrandFacetsAsync(int? categoryId, CancellationToken cancellationToken)
        => CacheAsync(
            $"catalog:brand-facets:{categoryId?.ToString() ?? "all"}",
            ct => catalogQuery.GetBrandFacetsAsync(categoryId, ct),
            cancellationToken,
            TimeSpan.FromMinutes(2));

    public Task<IReadOnlyList<DistrictOptionDto>> GetDistrictsByCityAsync(int cityId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cityId);
        return CacheAsync(
            $"catalog:districts:{cityId}",
            ct => catalogQuery.GetDistrictsByCityAsync(cityId, ct),
            cancellationToken);
    }

    public Task<IReadOnlyList<DistrictOptionDto>> GetDistrictsByCitiesAsync(
        IReadOnlyList<int> cityIds,
        CancellationToken cancellationToken)
    {
        var ids = cityIds.Where(id => id > 0).Distinct().OrderBy(id => id).ToArray();
        if (ids.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<DistrictOptionDto>>([]);
        }

        return catalogQuery.GetDistrictsByCitiesAsync(ids, cancellationToken);
    }

    public Task<IReadOnlyList<NeighborhoodOptionDto>> GetNeighborhoodsByDistrictAsync(
        int districtId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(districtId);
        return catalogQuery.GetNeighborhoodsByDistrictAsync(districtId, 500, cancellationToken);
    }

    public Task<IReadOnlyList<StreetOptionDto>> GetStreetsByNeighborhoodAsync(
        int neighborhoodId,
        string? query,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(neighborhoodId);
        return catalogQuery.GetStreetsByNeighborhoodAsync(neighborhoodId, query, 100, cancellationToken);
    }

    private async Task<IReadOnlyList<T>> CacheAsync<T>(
        string key,
        Func<CancellationToken, Task<IReadOnlyList<T>>> factory,
        CancellationToken cancellationToken,
        TimeSpan? ttl = null)
    {
        // System.Text.Json deserializes to List<T>; store/read concrete lists.
        var cached = await cache.GetJsonAsync<List<T>>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var value = await factory(cancellationToken);
        var list = value as List<T> ?? value.ToList();
        await cache.SetJsonAsync(key, list, ttl ?? HotTtl, cancellationToken);
        return list;
    }
}
