namespace AracParki.Domain.Corporate;

public static class CorporateStatus
{
    public const string Draft = "draft";
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";

    public static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.Ordinal)
    {
        Draft,
        Pending,
        Approved,
        Rejected
    };

    public static string Label(string status) => status switch
    {
        Draft => "Taslak",
        Pending => "Onay bekliyor",
        Approved => "Onaylı",
        Rejected => "Reddedildi",
        _ => status
    };

    public static string BadgeClass(string status) => status switch
    {
        Draft => "badge badge-muted",
        Pending => "badge badge-info",
        Approved => "badge badge-ok",
        Rejected => "badge badge-danger",
        _ => "badge badge-muted"
    };
}
