using AracParki.Application.Catalog.Dtos;

namespace AracParki.Application.Catalog.Services;

public sealed class CatalogService(ICatalogQuery catalogQuery)
{
    public Task<IReadOnlyList<CategorySummaryDto>> GetCategoriesWithCountsAsync(CancellationToken cancellationToken)
        => catalogQuery.GetCategoriesWithCountsAsync(cancellationToken);

    public Task<IReadOnlyList<CitySummaryDto>> GetPopularCitiesAsync(CancellationToken cancellationToken)
        => catalogQuery.GetPopularCitiesAsync(cancellationToken);

    public Task<IReadOnlyList<CityOptionDto>> GetAllCitiesAsync(CancellationToken cancellationToken)
        => catalogQuery.GetAllCitiesAsync(cancellationToken);

    public Task<IReadOnlyList<CategoryOptionDto>> GetAllCategoriesAsync(CancellationToken cancellationToken)
        => catalogQuery.GetAllCategoriesAsync(cancellationToken);

    public Task<IReadOnlyList<CategoryGroupDto>> GetCategoryGroupsAsync(CancellationToken cancellationToken)
        => catalogQuery.GetCategoryGroupsAsync(cancellationToken);

    public Task<IReadOnlyList<BrandOptionDto>> GetAllBrandsAsync(CancellationToken cancellationToken)
        => catalogQuery.GetAllBrandsAsync(cancellationToken);

    public Task<IReadOnlyList<BrandOptionDto>> GetBrandsByCategoryAsync(int categoryId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(categoryId);
        return catalogQuery.GetBrandsByCategoryAsync(categoryId, cancellationToken);
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
        return catalogQuery.GetCategoryAttributesAsync(categoryId, cancellationToken);
    }

    public Task<IReadOnlyList<AttachmentOptionDto>> GetAttachmentsAsync(CancellationToken cancellationToken)
        => catalogQuery.GetAttachmentsAsync(cancellationToken);

    public Task<IReadOnlyList<AttachmentOptionDto>> GetAttachmentsByCategoryAsync(
        int categoryId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(categoryId);
        return catalogQuery.GetAttachmentsByCategoryAsync(categoryId, cancellationToken);
    }

    public Task<IReadOnlyList<FacetCountDto>> GetBrandFacetsAsync(int? categoryId, CancellationToken cancellationToken)
        => catalogQuery.GetBrandFacetsAsync(categoryId, cancellationToken);

    public Task<IReadOnlyList<DistrictOptionDto>> GetDistrictsByCityAsync(int cityId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cityId);
        return catalogQuery.GetDistrictsByCityAsync(cityId, cancellationToken);
    }

    public Task<IReadOnlyList<DistrictOptionDto>> GetDistrictsByCitiesAsync(
        IReadOnlyList<int> cityIds,
        CancellationToken cancellationToken)
    {
        var ids = cityIds.Where(id => id > 0).Distinct().ToArray();
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
}
