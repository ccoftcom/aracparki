namespace AracParki.Application.Corporate.Dtos;

public sealed class CorporateAccountDto
{
    public long Id { get; init; }
    public long AccountId { get; init; }
    public required string CompanyType { get; init; }
    public required string TradeName { get; init; }
    public required string DisplayName { get; init; }
    public required string Slug { get; init; }
    public required string TaxOffice { get; init; }
    public required string TaxNumber { get; init; }
    public string? MersisNo { get; init; }
    public string? TradeRegistryNo { get; init; }
    public string? KepAddress { get; init; }
    public required string AuthorizedName { get; init; }
    public required string Phone { get; init; }
    public required string Email { get; init; }
    public string? Website { get; init; }
    public int CityId { get; init; }
    public int DistrictId { get; init; }
    public required string AddressLine { get; init; }
    public string? LogoUrl { get; init; }
    public required string Status { get; init; }
    public string? RejectionReason { get; init; }
    public DateTimeOffset? SubmittedAt { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }
    public long? ReviewedByAccountId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Join'lenen okunabilir alanlar (liste/detay ekranları).</summary>
    public string? CityName { get; init; }
    public string? DistrictName { get; init; }
    public string? OwnerEmail { get; init; }
    public string? OwnerName { get; init; }
}
