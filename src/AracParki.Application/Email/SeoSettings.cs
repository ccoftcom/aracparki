namespace AracParki.Application.Email;

/// <summary>Optional Search Console / Analytics hooks (configure in App:Seo).</summary>
public sealed class SeoSettings
{
    public const string SectionName = "App:Seo";

    /// <summary>Google Search Console HTML tag verification content value.</summary>
    public string? GoogleSiteVerification { get; set; }

    /// <summary>GA4 measurement ID, e.g. G-XXXXXXXX.</summary>
    public string? GoogleAnalyticsMeasurementId { get; set; }

    public bool HasGoogleAnalytics =>
        !string.IsNullOrWhiteSpace(GoogleAnalyticsMeasurementId)
        && GoogleAnalyticsMeasurementId.StartsWith("G-", StringComparison.Ordinal);
}
