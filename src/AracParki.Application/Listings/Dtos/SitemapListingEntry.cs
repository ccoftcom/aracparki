namespace AracParki.Application.Listings.Dtos;

public sealed class SitemapListingEntry
{
    public required string AdNo { get; init; }
    public DateTimeOffset LastModified { get; init; }
}
