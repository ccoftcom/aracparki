namespace AracParki.Domain.Listings;

public static class ListingStatus
{
    public const string Draft = "draft";
    public const string PendingReview = "pending_review";
    public const string Published = "published";
    public const string Rejected = "rejected";
    public const string Archived = "archived";

    public static readonly HashSet<string> Known =
    [
        Draft,
        PendingReview,
        Published,
        Rejected,
        Archived
    ];

    /// <summary>Statuses an owner may edit / resubmit for review (including unpublish).</summary>
    public static readonly HashSet<string> OwnerEditable =
    [
        PendingReview,
        Rejected,
        Published
    ];

    /// <summary>Statuses where the listing-image API may mutate rows (not silent published edits).</summary>
    public static readonly HashSet<string> OwnerImageMutable =
    [
        PendingReview,
        Rejected
    ];

    public static bool IsOwnerEditable(string? status)
        => !string.IsNullOrWhiteSpace(status) && OwnerEditable.Contains(status);

    public static bool IsOwnerImageMutable(string? status)
        => !string.IsNullOrWhiteSpace(status) && OwnerImageMutable.Contains(status);

    public static string Label(string status) => status switch
    {
        Draft => "Taslak",
        PendingReview => "İncelemede",
        Published => "Yayında",
        Rejected => "Reddedildi",
        Archived => "Arşiv",
        _ => status
    };

    public static string BadgeClass(string status) => status switch
    {
        PendingReview => "badge badge-warn",
        Published => "badge badge-ok",
        Rejected => "badge badge-danger",
        Archived => "badge badge-muted",
        _ => "badge"
    };
}
