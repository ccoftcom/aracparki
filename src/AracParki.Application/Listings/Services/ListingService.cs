using AracParki.Application.Listings.Dtos;
using AracParki.Application.Listings.Queries;
using FluentValidation;

namespace AracParki.Application.Listings.Services;

public sealed class ListingService(
    IListingQuery listingQuery,
    IValidator<ListingSearchQuery> validator)
{
    public async Task<ListingSearchResult> SearchAsync(ListingSearchQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken);
        return await listingQuery.SearchAsync(Normalize(query), cancellationToken);
    }

    public async Task<IReadOnlyList<ListingCardDto>> GetFeaturedAsync(
        ListingSearchQuery query,
        int take,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken);
        return await listingQuery.GetFeaturedAsync(Normalize(query), take, cancellationToken);
    }

    public Task<ListingDetailDto?> GetByAdNoAsync(string adNo, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adNo);
        return listingQuery.GetByAdNoAsync(adNo.Trim(), cancellationToken);
    }

    public Task<string?> GetPhoneByAdNoAsync(string adNo, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adNo);
        return listingQuery.GetPhoneByAdNoAsync(adNo.Trim(), cancellationToken);
    }

    public Task<IReadOnlyList<ListingCardDto>> GetByAccountIdAsync(
        long accountId,
        int take,
        CancellationToken cancellationToken)
    {
        if (accountId <= 0)
        {
            return Task.FromResult<IReadOnlyList<ListingCardDto>>([]);
        }

        return listingQuery.GetByAccountIdAsync(accountId, Math.Clamp(take, 1, 100), cancellationToken);
    }

    private static ListingSearchQuery Normalize(ListingSearchQuery query)
    {
        var specValues = query.SpecValues
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
            .ToDictionary(kv => kv.Key.Trim(), kv => kv.Value.Trim(), StringComparer.Ordinal);

        var (equalityJson, minJson) = SpecFilterBuilder.Build(specValues);
        if (!string.IsNullOrWhiteSpace(query.SpecsFilterJson))
        {
            equalityJson = query.SpecsFilterJson.Trim();
        }

        if (!string.IsNullOrWhiteSpace(query.SpecMinJson))
        {
            minJson = query.SpecMinJson.Trim();
        }

        return new ListingSearchQuery
        {
            Intent = string.IsNullOrWhiteSpace(query.Intent)
                ? Domain.Listings.ListingIntent.All
                : query.Intent.Trim(),
            CategoryId = query.CategoryId,
            BrandId = query.BrandId,
            ModelId = query.ModelId,
            CityIds = query.CityIds.Where(id => id > 0).Distinct().ToArray(),
            DistrictIds = query.DistrictIds.Where(id => id > 0).Distinct().ToArray(),
            Condition = string.IsNullOrWhiteSpace(query.Condition) ? null : query.Condition.Trim(),
            SellerType = string.IsNullOrWhiteSpace(query.SellerType) ? null : query.SellerType.Trim(),
            YearMin = query.YearMin,
            YearMax = query.YearMax,
            HoursMin = query.HoursMin,
            HoursMax = query.HoursMax,
            WeightMin = query.WeightMin,
            WeightMax = query.WeightMax,
            PriceMin = query.PriceMin,
            PriceMax = query.PriceMax,
            HorsepowerMin = query.HorsepowerMin,
            HorsepowerMax = query.HorsepowerMax,
            CapacityKgMin = query.CapacityKgMin,
            CapacityKgMax = query.CapacityKgMax,
            IncludesOperator = query.IncludesOperator,
            PriceUnit = string.IsNullOrWhiteSpace(query.PriceUnit) ? null : query.PriceUnit.Trim(),
            VerifiedOnly = query.VerifiedOnly,
            AttachmentIds = query.AttachmentIds.Where(id => id > 0).Distinct().ToArray(),
            SpecValues = specValues,
            SpecsFilterJson = equalityJson,
            SpecMinJson = minJson,
            Category = string.IsNullOrWhiteSpace(query.Category) ? null : query.Category.Trim(),
            City = string.IsNullOrWhiteSpace(query.City) ? null : query.City.Trim(),
            Query = string.IsNullOrWhiteSpace(query.Query) || query.Query.Trim().Length < 2
                ? null
                : query.Query.Trim(),
            Sort = string.IsNullOrWhiteSpace(query.Sort)
                ? Domain.Listings.ListingSort.Newest
                : query.Sort.Trim(),
            Page = query.Page <= 0 ? 1 : query.Page,
            PageSize = query.PageSize <= 0 ? 24 : query.PageSize,
            CursorListedAt = query.CursorListedAt,
            CursorId = query.CursorId
        };
    }
}
