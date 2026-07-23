using AracParki.Application.Listings.Dtos;
using AracParki.Application.Listings.Queries;

namespace AracParki.Application.Listings;

public interface IListingQuery
{
    Task<ListingSearchResult> SearchAsync(ListingSearchQuery query, CancellationToken cancellationToken);
    Task<ListingDetailDto?> GetByAdNoAsync(
        string adNo,
        ListingAccessContext access,
        CancellationToken cancellationToken);

    /// <summary>Published, non-expired listings by AdNo (order not guaranteed).</summary>
    Task<IReadOnlyList<ListingDetailDto>> GetPublishedByAdNosAsync(
        IReadOnlyList<string> adNos,
        CancellationToken cancellationToken);

    Task<string?> GetPhoneByAdNoAsync(string adNo, CancellationToken cancellationToken);
    Task<IReadOnlyList<ListingCardDto>> GetFeaturedAsync(ListingSearchQuery query, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<ListingCardDto>> GetByAccountIdAsync(long accountId, int take, CancellationToken cancellationToken);

    Task<int> CountByAccountIdAsync(long accountId, CancellationToken cancellationToken);

    Task<ListingEditDto?> GetOwnedForEditAsync(string adNo, long accountId, CancellationToken cancellationToken);

    Task<int> CountPublishedAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SitemapListingEntry>> ListPublishedForSitemapAsync(
        int skip,
        int take,
        CancellationToken cancellationToken);
}
