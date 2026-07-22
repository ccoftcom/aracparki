namespace AracParki.Application.Listings.Queries;

public sealed class ListingSearchQuery
{
    public string Intent { get; init; } = Domain.Listings.ListingIntent.All;
    public int? CategoryId { get; init; }
    public int? BrandId { get; init; }
    public int? ModelId { get; init; }
    public IReadOnlyList<int> CityIds { get; init; } = [];
    public IReadOnlyList<int> DistrictIds { get; init; } = [];
    public string? Condition { get; init; }
    public string? SellerType { get; init; }
    public int? YearMin { get; init; }
    public int? YearMax { get; init; }
    public int? HoursMin { get; init; }
    public int? HoursMax { get; init; }
    public decimal? WeightMin { get; init; }
    public decimal? WeightMax { get; init; }
    public decimal? PriceMin { get; init; }
    public decimal? PriceMax { get; init; }
    public int? HorsepowerMin { get; init; }
    public int? HorsepowerMax { get; init; }
    public int? CapacityKgMin { get; init; }
    public int? CapacityKgMax { get; init; }
    public bool? IncludesOperator { get; init; }
    public string? PriceUnit { get; init; }
    public bool VerifiedOnly { get; init; }
    public IReadOnlyList<int> AttachmentIds { get; init; } = [];
    /// <summary>Raw form values keyed by attribute key (oz_* → key).</summary>
    public IReadOnlyDictionary<string, string> SpecValues { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
    /// <summary>JSON equality filter for bool/enum/exact specs, e.g. {"fuel":"diesel"}.</summary>
    public string? SpecsFilterJson { get; init; }
    /// <summary>JSON minimums for numeric specs, e.g. {"lift_height_m":"3"}.</summary>
    public string? SpecMinJson { get; init; }
    public string? Query { get; init; }
    public long? CorporateAccountId { get; init; }
    public string Sort { get; init; } = Domain.Listings.ListingSort.Newest;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 24;
    public DateTimeOffset? CursorListedAt { get; init; }
    public long? CursorId { get; init; }

    // Legacy name-based filters kept for URL compatibility during transition
    public string? Category { get; init; }
    public string? City { get; init; }
}
