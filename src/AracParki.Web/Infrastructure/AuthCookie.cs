using System.Security.Cryptography;
using System.Text;
using AracParki.Application.Accounts;
using AracParki.Application.Accounts.Dtos;
using AracParki.Application.Authorization;
using AracParki.Domain.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;

namespace AracParki.Web.Infrastructure;

public static class AuthCookie
{
    public const string SecurityStampClaimType = "sstamp";
    public const string EmailConfirmedClaimType = "email_confirmed";
    public static readonly TimeSpan StampValidationInterval = TimeSpan.FromMinutes(5);
    private static readonly byte[] ValidMarker = [1];

    public static ClaimsPrincipal CreatePrincipal(AccountDto account)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Email, account.Email),
            new(ClaimTypes.Name, account.DisplayName),
            new(SecurityStampClaimType, account.SecurityStamp),
            new(EmailConfirmedClaimType, account.EmailConfirmed ? "1" : "0"),
            new(ClaimTypes.Role, MapRoleClaim(account.Role))
        };

        if (!string.IsNullOrWhiteSpace(account.Phone))
        {
            claims.Add(new Claim("phone", account.Phone));
        }

        return new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public static bool IsEmailConfirmed(ClaimsPrincipal user)
        => user.FindFirstValue(EmailConfirmedClaimType) == "1";

    public static bool IsAdmin(ClaimsPrincipal user)
        => user.IsInRole(AuthRoles.Admin);

    /// <summary>DB role (user/admin) → cookie role claim (Admin for staff).</summary>
    public static string MapRoleClaim(string? dbRole)
        => AccountRole.IsAdmin(dbRole) ? AuthRoles.Admin : AuthRoles.Seller;

    public static void ConfigureSecurityStampValidation(CookieAuthenticationOptions options)
    {
        options.Events.OnValidatePrincipal = ValidateSecurityStampAsync;
    }

    private static async Task ValidateSecurityStampAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var idValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var stamp = principal.FindFirstValue(SecurityStampClaimType);
        if (!long.TryParse(idValue, out var accountId) || string.IsNullOrWhiteSpace(stamp))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        var cache = context.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
        var ct = context.HttpContext.RequestAborted;
        var cacheKey = $"auth:sstamp:{accountId}:{stamp}";

        try
        {
            var hit = await cache.GetAsync(cacheKey, ct);
            if (hit is { Length: > 0 })
            {
                return;
            }
        }
        catch
        {
            // Redis unavailable → fall through to DB validation.
        }

        var store = context.HttpContext.RequestServices.GetRequiredService<IAccountStore>();
        var account = await store.FindByIdAsync(accountId, ct);
        if (account is null || !FixedTimeStampEquals(account.SecurityStamp, stamp))
        {
            try
            {
                await cache.RemoveAsync(cacheKey, ct);
            }
            catch
            {
                // ignore cache errors on reject path
            }

            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        try
        {
            await cache.SetAsync(
                cacheKey,
                ValidMarker,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = StampValidationInterval
                },
                ct);
        }
        catch
        {
            // ignore — next request re-validates against DB
        }

        var emailClaim = principal.FindFirst(EmailConfirmedClaimType)?.Value == "1";
        var roleClaim = principal.FindFirstValue(ClaimTypes.Role) ?? "";
        var expectedRole = MapRoleClaim(account.Role);
        if (emailClaim != account.EmailConfirmed
            || !string.Equals(roleClaim, expectedRole, StringComparison.Ordinal))
        {
            context.ReplacePrincipal(CreatePrincipal(account));
            context.ShouldRenew = true;
        }
    }

    private static bool FixedTimeStampEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
