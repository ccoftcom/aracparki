using AracParki.Application.Accounts.Dtos;

namespace AracParki.Application.Accounts;

public interface IAccountStore
{
    Task<AccountDto?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<AccountDto?> FindByIdAsync(long id, CancellationToken cancellationToken);
    Task<long> CreateAsync(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        string? phone,
        CancellationToken cancellationToken);
    Task UpdatePasswordHashAsync(long accountId, string passwordHash, CancellationToken cancellationToken);

    /// <summary>Invalidates prior unused reset tokens, then inserts the new hash.</summary>
    Task SaveResetTokenAsync(long accountId, string tokenHash, DateTimeOffset expiresAt, CancellationToken cancellationToken);

    /// <summary>Returns account id for a still-valid unused reset token (does not consume).</summary>
    Task<long?> FindValidResetAccountIdAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically consumes the reset token, updates password, and rotates security stamp.
    /// </summary>
    Task<bool> TryResetPasswordWithTokenAsync(
        string tokenHash,
        long accountId,
        string passwordHash,
        string securityStamp,
        CancellationToken cancellationToken);

    /// <summary>Invalidates prior unused verification tokens, then inserts the new hash.</summary>
    Task SaveEmailVerificationTokenAsync(long accountId, string tokenHash, DateTimeOffset expiresAt, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically consumes a valid verification token and marks the account confirmed.
    /// Returns account id or null.
    /// </summary>
    Task<long?> TryConfirmEmailWithTokenAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Looks up any verification token row (used or not) and its account confirmation state.</summary>
    Task<(long AccountId, bool EmailConfirmed)?> FindAccountByVerificationTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken);
}
