using AracParki.Application.Catalog.Dtos;

namespace AracParki.Application.Catalog;

public interface ICatalogQuery
{
    Task<IReadOnlyList<CategorySummaryDto>> GetCategoriesWithCountsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CitySummaryDto>> GetPopularCitiesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CityOptionDto>> GetAllCitiesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CategoryOptionDto>> GetAllCategoriesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CategoryGroupDto>> GetCategoryGroupsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<BrandOptionDto>> GetAllBrandsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<BrandOptionDto>> GetBrandsByCategoryAsync(int categoryId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EquipmentModelOptionDto>> GetModelsByBrandCategoryAsync(int brandId, int categoryId, CancellationToken cancellationToken);
    Task<EquipmentModelOptionDto?> GetModelByIdAsync(int modelId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CategoryAttributeDto>> GetCategoryAttributesAsync(int categoryId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AttachmentOptionDto>> GetAttachmentsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AttachmentOptionDto>> GetAttachmentsByCategoryAsync(int categoryId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FacetCountDto>> GetBrandFacetsAsync(int? categoryId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DistrictOptionDto>> GetDistrictsByCityAsync(int cityId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DistrictOptionDto>> GetDistrictsByCitiesAsync(IReadOnlyList<int> cityIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<NeighborhoodOptionDto>> GetNeighborhoodsByDistrictAsync(int districtId, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<StreetOptionDto>> GetStreetsByNeighborhoodAsync(int neighborhoodId, string? query, int take, CancellationToken cancellationToken);
}
