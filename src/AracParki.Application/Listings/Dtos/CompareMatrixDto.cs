namespace AracParki.Application.Listings.Dtos;

public sealed class CompareMatrixDto
{
    public required IReadOnlyList<CompareColumnDto> Columns { get; init; }
    public required IReadOnlyList<CompareSectionDto> Sections { get; init; }
    public required IReadOnlyList<string> RequestedAdNos { get; init; }
    public required IReadOnlyList<string> MissingAdNos { get; init; }
}

public sealed class CompareColumnDto
{
    public required string AdNo { get; init; }
    public required string Title { get; init; }
    public required string CoverImageUrl { get; init; }
    public required string PriceText { get; init; }
    public required string Category { get; init; }
    public required string Brand { get; init; }
    public required string ModelName { get; init; }
    public required string DetailUrl { get; init; }
    public bool IsVerified { get; init; }
    public required string SellerLabel { get; init; }
}

public sealed class CompareSectionDto
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required IReadOnlyList<CompareRowDto> Rows { get; init; }
}

public sealed class CompareRowDto
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required IReadOnlyList<string> Values { get; init; }
    public bool IsDifferent { get; init; }
}
