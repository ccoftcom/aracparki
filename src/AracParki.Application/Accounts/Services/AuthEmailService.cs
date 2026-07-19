using AracParki.Application.Abstractions;
using AracParki.Application.Email;
using Microsoft.Extensions.Options;

namespace AracParki.Application.Accounts.Services;

public sealed class AuthEmailService(IEmailSender email, IOptions<AppSettings> app)
{
    private readonly AppSettings _app = app.Value;

    public Task SendEmailVerificationAsync(
        string toEmail,
        string firstName,
        string rawToken,
        CancellationToken cancellationToken)
    {
        var url = BuildAbsoluteUrl($"/eposta-dogrula?token={Uri.EscapeDataString(rawToken)}");
        var name = string.IsNullOrWhiteSpace(firstName) ? "Merhaba" : firstName.Trim();
        var subject = "Hesabını onayla — Araç Parkı";
        var text =
            $"{name},\n\nAraç Parkı hesabını onaylamak için şu bağlantıyı aç (48 saat geçerli):\n{url}\n\nBu işlemi sen yapmadıysan bu e-postayı yok say.";
        var html = WrapHtml(
            name,
            "Hesabını onayla",
            "Araç Parkı’na hoş geldin. Üyeliğini tamamlamak için e-posta adresini doğrula.",
            url,
            "Hesabımı onayla",
            "Bağlantı 48 saat geçerlidir. Bu işlemi sen yapmadıysan e-postayı yok sayabilirsin.");

        return email.SendAsync(toEmail, subject, html, text, cancellationToken);
    }

    public Task SendPasswordResetAsync(
        string toEmail,
        string firstName,
        string rawToken,
        CancellationToken cancellationToken)
    {
        var url = BuildAbsoluteUrl($"/sifre-sifirla?token={Uri.EscapeDataString(rawToken)}");
        var name = string.IsNullOrWhiteSpace(firstName) ? "Merhaba" : firstName.Trim();
        var subject = "Şifre sıfırlama — Araç Parkı";
        var text =
            $"{name},\n\nŞifreni sıfırlamak için şu bağlantıyı aç (1 saat geçerli):\n{url}\n\nBu işlemi sen yapmadıysan bu e-postayı yok say; şifren değişmez.";
        var html = WrapHtml(
            name,
            "Şifreni sıfırla",
            "Hesabın için şifre sıfırlama talebi aldık. Yeni şifre belirlemek için aşağıdaki butona tıkla.",
            url,
            "Şifremi belirle",
            "Bağlantı 1 saat geçerlidir. Talebi sen oluşturmadıysan e-postayı yok say; şifren değişmez.");

        return email.SendAsync(toEmail, subject, html, text, cancellationToken);
    }

    public Task SendPasswordChangedAsync(
        string toEmail,
        string firstName,
        CancellationToken cancellationToken)
    {
        var name = string.IsNullOrWhiteSpace(firstName) ? "Merhaba" : firstName.Trim();
        var subject = "Şifren değişti — Araç Parkı";
        var loginUrl = BuildAbsoluteUrl("/giris");
        var text =
            $"{name},\n\nAraç Parkı hesabının şifresi az önce değiştirildi. Bu işlemi sen yapmadıysan hemen şifreni sıfırla: {BuildAbsoluteUrl("/sifremi-unuttum")}\n\nGiriş: {loginUrl}";
        var html = WrapHtml(
            name,
            "Şifren değiştirildi",
            "Hesabının şifresi az önce güncellendi. Bu işlemi sen yaptıysan bir şey yapmana gerek yok. Sen değilsen hemen şifreni sıfırla.",
            BuildAbsoluteUrl("/sifremi-unuttum"),
            "Şifremi sıfırla",
            "Bu e-posta güvenlik bildirimi olarak gönderildi.");

        return email.SendAsync(toEmail, subject, html, text, cancellationToken);
    }

    private string BuildAbsoluteUrl(string pathAndQuery)
    {
        var baseUrl = (_app.PublicBaseUrl ?? string.Empty).TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "http://localhost:5245";
        }

        return baseUrl + pathAndQuery;
    }

    private static string WrapHtml(
        string greetingName,
        string title,
        string lead,
        string actionUrl,
        string actionLabel,
        string footnote)
    {
        var safeName = System.Net.WebUtility.HtmlEncode(greetingName);
        var safeTitle = System.Net.WebUtility.HtmlEncode(title);
        var safeLead = System.Net.WebUtility.HtmlEncode(lead);
        var safeUrl = System.Net.WebUtility.HtmlEncode(actionUrl);
        var safeLabel = System.Net.WebUtility.HtmlEncode(actionLabel);
        var safeFoot = System.Net.WebUtility.HtmlEncode(footnote);

        return $$"""
            <!DOCTYPE html>
            <html lang="tr">
            <head><meta charset="utf-8" /><meta name="viewport" content="width=device-width" /></head>
            <body style="margin:0;padding:24px;background:#f4f4f2;font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #e6e6e2;border-radius:6px;">
                <tr>
                  <td style="padding:20px 24px;border-bottom:3px solid #ffe600;">
                    <strong style="font-size:18px;letter-spacing:-0.02em;">Araç<span style="color:#111;">Parkı</span></strong>
                  </td>
                </tr>
                <tr>
                  <td style="padding:24px;">
                    <p style="margin:0 0 8px;font-size:14px;color:#666;">{{safeName}},</p>
                    <h1 style="margin:0 0 12px;font-size:22px;line-height:1.25;">{{safeTitle}}</h1>
                    <p style="margin:0 0 20px;font-size:15px;line-height:1.5;color:#333;">{{safeLead}}</p>
                    <p style="margin:0 0 24px;">
                      <a href="{{safeUrl}}" style="display:inline-block;padding:12px 18px;background:#ffe600;color:#0c0c0c;text-decoration:none;font-weight:700;border-radius:4px;">{{safeLabel}}</a>
                    </p>
                    <p style="margin:0;font-size:12px;line-height:1.45;color:#777;">{{safeFoot}}</p>
                    <p style="margin:16px 0 0;font-size:11px;line-height:1.4;color:#999;word-break:break-all;">{{safeUrl}}</p>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }
}
