using System.Text.RegularExpressions;

namespace AracParki.Application.Listings;

/// <summary>Rewrites Cloudflare media delivery URLs to a given variant.</summary>
public static partial class ListingImageUrlVariants
{
    /// <summary>
    /// If <paramref name="url"/> is a Worker delivery URL (<c>/m/{key}</c>),
    /// returns the same URL with <c>?v=</c> set to <paramref name="variant"/>.
    /// Otherwise returns the original URL.
    /// </summary>
    public static string WithVariant(string? url, string variant)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(variant))
        {
            return url ?? string.Empty;
        }

        var match = MediaPathRegex().Match(url);
        if (!match.Success)
        {
            return url;
        }

        var prefix = url[..match.Index];
        var key = match.Groups[1].Value;
        return $"{prefix}/m/{key}?v={Uri.EscapeDataString(variant)}";
    }

    public static string? SrcSet(string? url, params (string Variant, int Width)[] variants)
    {
        if (string.IsNullOrWhiteSpace(url) || variants.Length == 0)
        {
            return null;
        }

        if (!MediaPathRegex().IsMatch(url))
        {
            return null;
        }

        return string.Join(", ", variants.Select(v => $"{WithVariant(url, v.Variant)} {v.Width}w"));
    }

    [GeneratedRegex(@"\/m\/([^?#\s]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MediaPathRegex();
}
