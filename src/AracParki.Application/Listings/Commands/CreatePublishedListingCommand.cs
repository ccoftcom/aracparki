namespace AracParki.Application.Listings.Commands;

public sealed class CreatePublishedListingCommand
{
    public long AccountId { get; init; }
    public required string SellerDisplayName { get; init; }
    public required string Phone { get; init; }

    public int CategoryId { get; init; }
    public int BrandId { get; init; }
    public int? ModelId { get; init; }
    public required string ModelName { get; init; }
    public string? SerialNo { get; init; }

    public int CityId { get; init; }
    public int DistrictId { get; init; }

    public required string PrimaryIntent { get; init; }
    public required string[] Intents { get; init; }
    public required string Condition { get; init; }

    public int ModelYear { get; init; }
    public int Hours { get; init; }
    public decimal Tons { get; init; }
    public int? CapacityKg { get; init; }
    public int Horsepower { get; init; }

    public decimal Price { get; init; }
    public string? PriceUnit { get; init; }
    public bool IncludesOperator { get; init; }

    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string SpecsJson { get; init; }

    public required IReadOnlyList<string> ImageUrls { get; init; }

    public IReadOnlyList<int> AttachmentIds { get; init; } = [];
}
