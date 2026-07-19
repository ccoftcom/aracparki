using AracParki.Application.Abstractions;
using AracParki.Application.Accounts;
using AracParki.Application.Accounts.Dtos;
using Dapper;

namespace AracParki.Infrastructure.Accounts;

public sealed class AccountRepository(IDbConnectionFactory connectionFactory) : IAccountStore
{
    private const string AccountSelect = """
        SELECT id,
               email,
               password_hash AS PasswordHash,
               first_name AS FirstName,
               last_name AS LastName,
               phone,
               email_confirmed_at AS EmailConfirmedAt,
               security_stamp AS SecurityStamp
        FROM accounts
        """;

    public async Task<AccountDto?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AccountDto>(
            new CommandDefinition(
                AccountSelect + """
                 WHERE lower(email) = @Email
                LIMIT 1
                """,
                new { Email = email },
                cancellationToken: cancellationToken));
    }

    public async Task<AccountDto?> FindByIdAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AccountDto>(
            new CommandDefinition(
                AccountSelect + """
                 WHERE id = @Id
                LIMIT 1
                """,
                new { Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<long> CreateAsync(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        string? phone,
        CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                """
                INSERT INTO accounts (email, password_hash, first_name, last_name, phone)
                VALUES (@Email, @PasswordHash, @FirstName, @LastName, @Phone)
                RETURNING id
                """,
                new
                {
                    Email = email,
                    PasswordHash = passwordHash,
                    FirstName = firstName,
                    LastName = lastName,
                    Phone = phone
                },
                cancellationToken: cancellationToken));
    }

    public async Task UpdatePasswordHashAsync(long accountId, string passwordHash, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE accounts
                SET password_hash = @PasswordHash
                WHERE id = @Id
                """,
                new { Id = accountId, PasswordHash = passwordHash },
                cancellationToken: cancellationToken));
    }

    public async Task SaveResetTokenAsync(
        long accountId,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE password_reset_tokens
                    SET used_at = COALESCE(used_at, NOW())
                    WHERE account_id = @AccountId
                      AND used_at IS NULL
                    """,
                    new { AccountId = accountId },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO password_reset_tokens (account_id, token_hash, expires_at)
                    VALUES (@AccountId, @TokenHash, @ExpiresAt)
                    """,
                    new { AccountId = accountId, TokenHash = tokenHash, ExpiresAt = expiresAt },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<long?> FindValidResetAccountIdAsync(string tokenHash, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(
                """
                SELECT account_id
                FROM password_reset_tokens
                WHERE token_hash = @TokenHash
                  AND used_at IS NULL
                  AND expires_at > NOW()
                LIMIT 1
                """,
                new { TokenHash = tokenHash },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> TryResetPasswordWithTokenAsync(
        string tokenHash,
        long accountId,
        string passwordHash,
        string securityStamp,
        CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var consumedId = await connection.ExecuteScalarAsync<long?>(
                new CommandDefinition(
                    """
                    UPDATE password_reset_tokens
                    SET used_at = NOW()
                    WHERE token_hash = @TokenHash
                      AND account_id = @AccountId
                      AND used_at IS NULL
                      AND expires_at > NOW()
                    RETURNING account_id
                    """,
                    new { TokenHash = tokenHash, AccountId = accountId },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            if (consumedId is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return false;
            }

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE accounts
                    SET password_hash = @PasswordHash,
                        security_stamp = @SecurityStamp
                    WHERE id = @Id
                    """,
                    new { Id = accountId, PasswordHash = passwordHash, SecurityStamp = securityStamp },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE password_reset_tokens
                    SET used_at = COALESCE(used_at, NOW())
                    WHERE account_id = @Id
                      AND used_at IS NULL
                    """,
                    new { Id = accountId },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            await tx.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task SaveEmailVerificationTokenAsync(
        long accountId,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE email_verification_tokens
                    SET used_at = COALESCE(used_at, NOW())
                    WHERE account_id = @AccountId
                      AND used_at IS NULL
                    """,
                    new { AccountId = accountId },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO email_verification_tokens (account_id, token_hash, expires_at)
                    VALUES (@AccountId, @TokenHash, @ExpiresAt)
                    """,
                    new { AccountId = accountId, TokenHash = tokenHash, ExpiresAt = expiresAt },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<long?> TryConfirmEmailWithTokenAsync(string tokenHash, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var accountId = await connection.ExecuteScalarAsync<long?>(
                new CommandDefinition(
                    """
                    UPDATE email_verification_tokens
                    SET used_at = NOW()
                    WHERE token_hash = @TokenHash
                      AND used_at IS NULL
                      AND expires_at > NOW()
                    RETURNING account_id
                    """,
                    new { TokenHash = tokenHash },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            if (accountId is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return null;
            }

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE accounts
                    SET email_confirmed_at = COALESCE(email_confirmed_at, NOW())
                    WHERE id = @Id
                    """,
                    new { Id = accountId.Value },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE email_verification_tokens
                    SET used_at = COALESCE(used_at, NOW())
                    WHERE account_id = @Id
                      AND used_at IS NULL
                    """,
                    new { Id = accountId.Value },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            await tx.CommitAsync(cancellationToken);
            return accountId;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<(long AccountId, bool EmailConfirmed)?> FindAccountByVerificationTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<TokenAccountRow>(
            new CommandDefinition(
                """
                SELECT a.id AS AccountId,
                       (a.email_confirmed_at IS NOT NULL) AS EmailConfirmed
                FROM email_verification_tokens t
                JOIN accounts a ON a.id = t.account_id
                WHERE t.token_hash = @TokenHash
                LIMIT 1
                """,
                new { TokenHash = tokenHash },
                cancellationToken: cancellationToken));

        return row is null ? null : (row.AccountId, row.EmailConfirmed);
    }

    private sealed class TokenAccountRow
    {
        public long AccountId { get; init; }
        public bool EmailConfirmed { get; init; }
    }
}
