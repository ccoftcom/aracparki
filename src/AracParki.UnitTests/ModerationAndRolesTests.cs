using AracParki.Application.Authorization;
using AracParki.Application.Listings;
using AracParki.Application.Listings.Services;
using AracParki.Domain.Accounts;
using AracParki.Domain.Listings;
using AracParki.Web.Infrastructure;
using System.Security.Claims;

namespace AracParki.UnitTests;

public sealed class ModerationAndRolesTests
{
    [Fact]
    public void ListingStatus_labels_and_known()
    {
        Assert.Contains(ListingStatus.PendingReview, ListingStatus.Known);
        Assert.Contains(ListingStatus.Rejected, ListingStatus.Known);
        Assert.Equal("İncelemede", ListingStatus.Label(ListingStatus.PendingReview));
        Assert.Equal("Reddedildi", ListingStatus.Label(ListingStatus.Rejected));
    }

    [Fact]
    public void ListingStatus_owner_editable_includes_published()
    {
        Assert.True(ListingStatus.IsOwnerEditable(ListingStatus.Published));
        Assert.True(ListingStatus.IsOwnerEditable(ListingStatus.PendingReview));
        Assert.True(ListingStatus.IsOwnerEditable(ListingStatus.Rejected));
        Assert.False(ListingStatus.IsOwnerEditable(ListingStatus.Archived));
        Assert.False(ListingStatus.IsOwnerImageMutable(ListingStatus.Published));
        Assert.True(ListingStatus.IsOwnerImageMutable(ListingStatus.PendingReview));
    }

    [Fact]
    public void AccountRole_is_admin()
    {
        Assert.True(AccountRole.IsAdmin("admin"));
        Assert.True(AccountRole.IsAdmin("Admin"));
        Assert.False(AccountRole.IsAdmin("user"));
        Assert.False(AccountRole.IsAdmin(null));
    }

    [Fact]
    public void AuthCookie_maps_admin_role_claim()
    {
        var admin = AuthCookie.CreatePrincipal(new Application.Accounts.Dtos.AccountDto
        {
            Id = 1,
            Email = "a@b.com",
            PasswordHash = "x",
            FirstName = "A",
            LastName = "B",
            SecurityStamp = "s",
            Role = AccountRole.Admin,
            EmailConfirmedAt = DateTimeOffset.UtcNow
        });

        Assert.True(admin.IsInRole(AuthRoles.Admin));
        Assert.True(AuthCookie.IsAdmin(admin));
        Assert.Equal(AuthRoles.Admin, admin.FindFirstValue(ClaimTypes.Role));
    }

    [Fact]
    public void AuthCookie_maps_user_to_seller_claim()
    {
        var user = AuthCookie.CreatePrincipal(new Application.Accounts.Dtos.AccountDto
        {
            Id = 2,
            Email = "u@b.com",
            PasswordHash = "x",
            FirstName = "U",
            LastName = "Ser",
            SecurityStamp = "s",
            Role = AccountRole.User
        });

        Assert.False(AuthCookie.IsAdmin(user));
        Assert.Equal(AuthRoles.Seller, user.FindFirstValue(ClaimTypes.Role));
    }

    [Fact]
    public void ListingAccessContext_from_principal_maps_admin_and_account()
    {
        var admin = AuthCookie.CreatePrincipal(new Application.Accounts.Dtos.AccountDto
        {
            Id = 9,
            Email = "a@b.com",
            PasswordHash = "x",
            FirstName = "A",
            LastName = "B",
            SecurityStamp = "s",
            Role = AccountRole.Admin,
            EmailConfirmedAt = DateTimeOffset.UtcNow
        });

        var ctx = ListingAccessContext.FromPrincipal(admin);
        Assert.Equal(9, ctx.AccountId);
        Assert.True(ctx.IsAdmin);

        Assert.Equal(ListingAccessContext.Anonymous, ListingAccessContext.FromPrincipal(null));
        Assert.False(ListingAccessContext.FromPrincipal(new ClaimsPrincipal()).IsAdmin);
    }

    [Fact]
    public async Task RejectAsync_requires_reason()
    {
        var svc = new ListingModerationService(new FakeStore());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.RejectAsync("AP-1", 1, "  ", CancellationToken.None));
    }

    [Fact]
    public async Task ApproveAsync_rejects_invalid_admin()
    {
        var svc = new ListingModerationService(new FakeStore());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            svc.ApproveAsync("AP-1", 0, CancellationToken.None));
    }

    [Fact]
    public async Task RejectAsync_rejects_overlong_reason()
    {
        var svc = new ListingModerationService(new FakeStore());
        var reason = new string('x', ListingModerationService.RejectionReasonMaxLength + 1);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.RejectAsync("AP-1", 1, reason, CancellationToken.None));
    }

    private sealed class FakeStore : Application.Listings.IListingStore
    {
        public Task ApproveAsync(string adNo, long adminAccountId, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<string> CreatePublishedAsync(
            Application.Listings.Commands.CreatePublishedListingCommand command,
            CancellationToken cancellationToken)
            => Task.FromResult("AP-1");

        public Task<Application.Listings.Dtos.ModerationCountsDto> GetModerationCountsAsync(
            CancellationToken cancellationToken)
            => Task.FromResult(new Application.Listings.Dtos.ModerationCountsDto());

        public Task<IReadOnlyList<Application.Listings.Dtos.ModerationListItemDto>> ListForModerationAsync(
            string status,
            int take,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Application.Listings.Dtos.ModerationListItemDto>>([]);

        public Task RejectAsync(string adNo, long adminAccountId, string reason, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task UpdateForReviewAsync(
            string adNo,
            long accountId,
            Application.Listings.Commands.CreatePublishedListingCommand command,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
