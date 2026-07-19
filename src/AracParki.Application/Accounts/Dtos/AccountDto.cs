namespace AracParki.Application.Accounts.Dtos;

public sealed class AccountDto
{
    public long Id { get; init; }
    public required string Email { get; init; }
    public required string PasswordHash { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? Phone { get; init; }
    public DateTimeOffset? EmailConfirmedAt { get; init; }
    public required string SecurityStamp { get; init; }

    public bool EmailConfirmed => EmailConfirmedAt.HasValue;

    public string DisplayName => string.Join(' ', new[] { FirstName, LastName }
        .Where(static part => !string.IsNullOrWhiteSpace(part)));
}
