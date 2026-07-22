using System.Security.Cryptography;
using System.Text;
using AracParki.Application.Accounts;
using AracParki.Application.Accounts.Services;
using AracParki.Application.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AracParki.Infrastructure.Accounts;

public sealed class PhoneOtpService(
    IPhoneOtpStore otpStore,
    IAccountStore accounts,
    IWhatsAppOtpSender whatsApp,
    IOptions<WhatsAppSettings> whatsAppOptions,
    IHostEnvironment environment,
    ILogger<PhoneOtpService> logger) : IPhoneOtpService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    public const int MaxVerifyAttempts = 5;

    public async Task<(bool Ok, string? Error, string? DevCode)> SendAsync(
        long accountId,
        string phone,
        CancellationToken cancellationToken)
    {
        var normalized = AccountService.NormalizePhone(phone);
        if (normalized is null)
        {
            return (false, "Geçerli bir telefon numarası gir.", null);
        }

        var settings = whatsAppOptions.Value;
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        var skipWhatsApp = environment.IsDevelopment()
                           && !settings.SendRealWhatsAppOtpInDevelopment;

        if (!skipWhatsApp)
        {
            var (sent, sendError) = await whatsApp.SendTurkishOtpAsync(normalized, code, cancellationToken);
            if (!sent)
            {
                return (false, sendError ?? "WhatsApp doğrulama kodu gönderilemedi.", null);
            }
        }
        else
        {
            logger.LogInformation(
                "Skipping WhatsApp OTP send in Development for account {AccountId} (…{Suffix})",
                accountId,
                normalized[^4..]);
        }

        var hash = Hash(code);
        await otpStore.SaveAsync(accountId, normalized, hash, DateTimeOffset.UtcNow.Add(Ttl), cancellationToken);

        logger.LogInformation(
            "Phone OTP issued for account {AccountId} phone ending {Suffix} via {Channel}",
            accountId,
            normalized[^4..],
            skipWhatsApp ? "dev" : "whatsapp");

        // Never log the code. Only expose to UI when explicitly enabled in Development.
        var exposeDevCode = environment.IsDevelopment()
                            && settings.ExposeDevOtpCode
                            && skipWhatsApp;
        return (true, null, exposeDevCode ? code : null);
    }

    public async Task<(bool Ok, string? Error)> VerifyAsync(
        long accountId,
        string phone,
        string code,
        CancellationToken cancellationToken)
    {
        var normalized = AccountService.NormalizePhone(phone);
        if (normalized is null)
        {
            return (false, "Geçerli bir telefon numarası gir.");
        }

        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length is < 4 or > 8)
        {
            return (false, "Doğrulama kodu geçersiz.");
        }

        var latest = await otpStore.GetLatestAsync(accountId, cancellationToken);
        if (latest is null)
        {
            return (false, "Önce doğrulama kodu iste.");
        }

        if (latest.Value.AttemptCount >= MaxVerifyAttempts)
        {
            await otpStore.ConsumeLatestAsync(accountId, cancellationToken);
            return (false, "Çok fazla hatalı deneme. Yeni kod iste.");
        }

        if (!string.Equals(latest.Value.Phone, normalized, StringComparison.Ordinal)
            || !FixedTimeEquals(latest.Value.CodeHash, Hash(code.Trim())))
        {
            var attempts = await otpStore.RegisterFailedAttemptAsync(
                accountId,
                MaxVerifyAttempts,
                cancellationToken);
            return attempts >= MaxVerifyAttempts
                ? (false, "Çok fazla hatalı deneme. Yeni kod iste.")
                : (false, "Doğrulama kodu hatalı veya süresi dolmuş.");
        }

        await otpStore.ConsumeLatestAsync(accountId, cancellationToken);
        await accounts.UpdatePhoneAsync(accountId, normalized, cancellationToken);
        await accounts.ConfirmPhoneAsync(accountId, cancellationToken);
        return (true, null);
    }

    private static string Hash(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
