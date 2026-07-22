namespace AracParki.Application.Messaging;

/// <summary>
/// Meta Cloud API WhatsApp settings — keys aligned with waponi-api (Turkish OTP only).
/// </summary>
public sealed class WhatsAppSettings
{
    public const string SectionName = "WhatsAppSettings";

    /// <summary>Meta phone number ID (Graph API path segment).</summary>
    public string AccountSid { get; set; } = "";

    /// <summary>Meta permanent / system user access token.</summary>
    public string AuthToken { get; set; } = "";

    /// <summary>e.g. "+90".</summary>
    public string FromNumberCountryCode { get; set; } = "+90";

    public string BaseUrl { get; set; } = "https://graph.facebook.com";

    public string ApiVersion { get; set; } = "v25.0";

    /// <summary>Turkish OTP template (Meta approved).</summary>
    public string TurkishTemplateName { get; set; } = "otp_tr_general_template";

    public string TurkishTemplateLanguageCode { get; set; } = "tr";

    /// <summary>Brand name injected into OTP template body ({{2}}). Must match Meta-approved sample.</summary>
    public string BrandName { get; set; } = "babuba";

    public int OtpRateLimitMaxRequests { get; set; } = 5;

    public int OtpRateLimitWindowMinutes { get; set; } = 60;

    /// <summary>
    /// When false (default), Development skips the Graph API call.
    /// </summary>
    public bool SendRealWhatsAppOtpInDevelopment { get; set; }

    /// <summary>
    /// When true (Development only), returns OTP in API response for local testing.
    /// Never enable in production.
    /// </summary>
    public bool ExposeDevOtpCode { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountSid) && !string.IsNullOrWhiteSpace(AuthToken);
}
