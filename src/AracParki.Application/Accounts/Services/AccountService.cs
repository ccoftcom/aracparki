using System.Security.Cryptography;
using System.Text;
using AracParki.Application.Accounts.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace AracParki.Application.Accounts.Services;

public sealed class AccountService(
    IAccountStore store,
    AuthEmailService authEmail,
    ILogger<AccountService> logger)
{
    private readonly PasswordHasher<string> _hasher = new();

    /// <summary>
    /// Creates account and issues verification token.
    /// <paramref name="VerificationEmailSent"/> is false when the account exists but SMTP failed.
    /// </summary>
    public async Task<(bool Ok, string? Error, bool VerificationEmailSent)> RegisterAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken)
    {
        email = NormalizeEmail(email);
        firstName = firstName.Trim();
        lastName = lastName.Trim();
        var displayName = $"{firstName} {lastName}".Trim();

        var passwordErrors = PasswordRules.Validate(password, displayName, email);
        if (passwordErrors.Count > 0)
        {
            return (false, passwordErrors[0], false);
        }

        if (await store.FindByEmailAsync(email, cancellationToken) is not null)
        {
            return (false, "Bu e-posta adresiyle bir hesap zaten var.", false);
        }

        var hash = _hasher.HashPassword(email, password);
        long id;
        try
        {
            id = await store.CreateAsync(email, hash, firstName, lastName, phone: null, cancellationToken);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            return (false, "Bu e-posta adresiyle bir hesap zaten var.", false);
        }

        var account = await store.FindByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Account create failed.");

        var verifyToken = await IssueEmailVerificationTokenAsync(id, cancellationToken);
        try
        {
            await authEmail.SendEmailVerificationAsync(
                account.Email,
                account.FirstName,
                verifyToken,
                cancellationToken);
            return (true, null, true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Verification email failed for {Email}", MaskEmail(account.Email));
            return (true, null, false);
        }
    }

    public async Task<(bool Ok, string? Error, AccountDto? Account)> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        email = NormalizeEmail(email);
        var account = await store.FindByEmailAsync(email, cancellationToken);
        if (account is null)
        {
            return (false, "E-posta veya şifre hatalı.", null);
        }

        var result = _hasher.VerifyHashedPassword(email, account.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            return (false, "E-posta veya şifre hatalı.", null);
        }

        if (!account.EmailConfirmed)
        {
            return (false, "email_unconfirmed", account);
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var newHash = _hasher.HashPassword(email, password);
            await store.UpdatePasswordHashAsync(account.Id, newHash, cancellationToken);
        }

        return (true, null, account);
    }

    /// <summary>Anti-enumeration: always succeeds from the caller's perspective.</summary>
    public async Task ResendEmailVerificationAsync(string email, CancellationToken cancellationToken)
    {
        email = NormalizeEmail(email);
        var account = await store.FindByEmailAsync(email, cancellationToken);
        if (account is null || account.EmailConfirmed)
        {
            return;
        }

        var token = await IssueEmailVerificationTokenAsync(account.Id, cancellationToken);
        await SendVerificationSafeAsync(account.Email, account.FirstName, token, cancellationToken);
    }

    public async Task<(bool Ok, string? Error, AccountDto? Account, bool AlreadyConfirmed)> ConfirmEmailAsync(
        string rawToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return (false, "Geçersiz veya süresi dolmuş bağlantı.", null, false);
        }

        var hash = HashToken(rawToken.Trim());
        var accountId = await store.TryConfirmEmailWithTokenAsync(hash, cancellationToken);
        if (accountId is not null)
        {
            var account = await store.FindByIdAsync(accountId.Value, cancellationToken);
            return account is null
                ? (false, "Geçersiz veya süresi dolmuş bağlantı.", null, false)
                : (true, null, account, false);
        }

        // Same link clicked twice, or already confirmed via another tab.
        var existing = await store.FindAccountByVerificationTokenHashAsync(hash, cancellationToken);
        if (existing is { EmailConfirmed: true })
        {
            var account = await store.FindByIdAsync(existing.Value.AccountId, cancellationToken);
            return account is null
                ? (false, "Geçersiz veya süresi dolmuş bağlantı.", null, false)
                : (true, null, account, true);
        }

        return (false, "Geçersiz veya süresi dolmuş bağlantı.", null, false);
    }

    /// <summary>Anti-enumeration: always succeeds from the caller's perspective.</summary>
    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken)
    {
        email = NormalizeEmail(email);
        var account = await store.FindByEmailAsync(email, cancellationToken);
        if (account is null)
        {
            return;
        }

        var raw = CreateRawToken();
        var hash = HashToken(raw);
        await store.SaveResetTokenAsync(account.Id, hash, DateTimeOffset.UtcNow.AddHours(1), cancellationToken);

        try
        {
            await authEmail.SendPasswordResetAsync(account.Email, account.FirstName, raw, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Password reset email failed");
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> ResetPasswordAsync(
        string rawToken,
        string newPassword,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return (false, "Geçersiz veya süresi dolmuş bağlantı.");
        }

        var tokenHash = HashToken(rawToken.Trim());
        var accountId = await store.FindValidResetAccountIdAsync(tokenHash, cancellationToken);
        if (accountId is null)
        {
            return (false, "Geçersiz veya süresi dolmuş bağlantı.");
        }

        var account = await store.FindByIdAsync(accountId.Value, cancellationToken);
        if (account is null)
        {
            return (false, "Geçersiz veya süresi dolmuş bağlantı.");
        }

        var passwordErrors = PasswordRules.Validate(newPassword, account.DisplayName, account.Email);
        if (passwordErrors.Count > 0)
        {
            return (false, passwordErrors[0]);
        }

        var passwordHash = _hasher.HashPassword(account.Email, newPassword);
        var stamp = Guid.NewGuid().ToString("N");
        var reset = await store.TryResetPasswordWithTokenAsync(
            tokenHash,
            account.Id,
            passwordHash,
            stamp,
            cancellationToken);
        if (!reset)
        {
            return (false, "Geçersiz veya süresi dolmuş bağlantı.");
        }

        try
        {
            await authEmail.SendPasswordChangedAsync(account.Email, account.FirstName, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Password-changed notification failed");
        }

        return (true, null);
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    private async Task SendVerificationSafeAsync(
        string email,
        string firstName,
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            await authEmail.SendEmailVerificationAsync(email, firstName, token, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Verification email failed for {Email}", MaskEmail(email));
            throw;
        }
    }

    private async Task<string> IssueEmailVerificationTokenAsync(long accountId, CancellationToken cancellationToken)
    {
        var raw = CreateRawToken();
        var hash = HashToken(raw);
        await store.SaveEmailVerificationTokenAsync(
            accountId,
            hash,
            DateTimeOffset.UtcNow.AddDays(2),
            cancellationToken);
        return raw;
    }

    private static string CreateRawToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1)
        {
            return "***";
        }

        return string.Concat(email.AsSpan(0, 1), "***", email.AsSpan(at));
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("ux_accounts_email_lower", StringComparison.OrdinalIgnoreCase)
                || message.Contains("unique", StringComparison.OrdinalIgnoreCase)
                || message.Contains("23505", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
