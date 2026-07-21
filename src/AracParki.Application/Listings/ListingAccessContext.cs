using System.Security.Claims;
using AracParki.Application.Authorization;

namespace AracParki.Application.Listings;

/// <summary>
/// Viewer context for listing detail visibility. Prefer this over a free-floating <c>isAdmin</c> bool.
/// </summary>
public readonly record struct ListingAccessContext(long? AccountId, bool IsAdmin)
{
    public static ListingAccessContext Anonymous { get; } = new(null, false);

    public static ListingAccessContext FromPrincipal(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Anonymous;
        }

        long? accountId = null;
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (long.TryParse(raw, out var id) && id > 0)
        {
            accountId = id;
        }

        return new ListingAccessContext(accountId, user.IsInRole(AuthRoles.Admin));
    }
}
