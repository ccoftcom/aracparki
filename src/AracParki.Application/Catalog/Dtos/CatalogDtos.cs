namespace AracParki.Application.Catalog.Dtos;

public sealed class CategorySummaryDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required string IconKey { get; init; }
    public int ListingCount { get; init; }
    public int? GroupId { get; init; }
    public string? GroupName { get; init; }
}

public sealed class CategoryOptionDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required string CapacityMetric { get; init; }
    public int? GroupId { get; init; }
}

public sealed class CategoryGroupDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public IReadOnlyList<CategoryOptionDto> Categories { get; init; } = [];
}

public sealed class CitySummaryDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public int ListingCount { get; init; }
}

public sealed class CityOptionDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
}

public sealed class DistrictOptionDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public int CityId { get; init; }
    public string? CityName { get; init; }
}

public sealed class NeighborhoodOptionDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
}

public sealed class StreetOptionDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
}

public sealed class BrandOptionDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
}

public sealed class EquipmentModelOptionDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public decimal? TypicalWeightMinT { get; init; }
    public decimal? TypicalWeightMaxT { get; init; }
    public int? Horsepower { get; init; }
    public int? CapacityKg { get; init; }
    public decimal? CapacityT { get; init; }
    public string? DefaultSpecsJson { get; init; }
}

public sealed class CategoryAttributeDto
{
    public int Id { get; init; }
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required string DataType { get; init; }
    public string? Unit { get; init; }
    public bool IsFilterable { get; init; }
    public bool IsRequired { get; init; }
    public string? EnumOptionsJson { get; init; }
}

public sealed class AttachmentOptionDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
}

public sealed class FacetCountDto
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public int Count { get; init; }
}
