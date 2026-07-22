namespace AracParki.Application.Corporate.Dtos;

/// <summary>Public dealer profile — no tax / owner PII.</summary>
public sealed class PublicDealerDto
{
    public long Id { get; init; }
    public required string Slug { get; init; }
    public required string DisplayName { get; init; }
    public required string CompanyType { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Website { get; init; }
    public string? LogoUrl { get; init; }
    public required string CityName { get; init; }
    public required string DistrictName { get; init; }
    public string? AddressLine { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class PublicDealerSitemapEntry
{
    public required string Slug { get; init; }
    public DateTimeOffset LastModified { get; init; }
}
