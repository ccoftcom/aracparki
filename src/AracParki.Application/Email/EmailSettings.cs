namespace AracParki.Application.Email;

public sealed class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Araç Parkı";
}

public sealed class AppSettings
{
    public const string SectionName = "App";

    /// <summary>Public site origin used in email links, e.g. https://www.aracparki.com</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5245";
}
