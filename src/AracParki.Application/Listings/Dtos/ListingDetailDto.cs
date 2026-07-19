namespace AracParki.Application.Listings.Dtos;

public sealed class ListingDetailDto
{
    public long Id { get; init; }
    public required string AdNo { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required string CategorySlug { get; init; }
    public int CategoryId { get; init; }
    public required string CapacityMetric { get; init; }
    public required string Brand { get; init; }
    public required string ModelName { get; init; }
    public string? SerialNo { get; init; }
    public required string PrimaryIntent { get; init; }
    public required string[] Intents { get; init; }
    public required string Condition { get; init; }
    public int ModelYear { get; init; }
    public int Hours { get; init; }
    public decimal Tons { get; init; }
    public int? CapacityKg { get; init; }
    public int Horsepower { get; init; }
    public required string City { get; init; }
    public required string District { get; init; }
    public decimal Price { get; init; }
    public string? PriceUnit { get; init; }
    public bool IncludesOperator { get; init; }
    public required string SpecsJson { get; init; }
    public required string CoverImageUrl { get; init; }
    public required IReadOnlyList<string> ImageUrls { get; init; }
    public required IReadOnlyList<AttachmentItemDto> Attachments { get; init; }
    public required string SellerName { get; init; }
    public required string SellerType { get; init; }
    public bool IsVerified { get; init; }
    public DateTimeOffset ListedAt { get; init; }
}

public sealed class AttachmentItemDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
}
