using AracParki.Application.Media;
using Microsoft.Extensions.Options;

namespace AracParki.Application.Listings;

public static class ListingImageUrl
{
    public const string UploadPrefix = "/uploads/listings/";
    public const int MaxCount = 30;
    public const long MaxUploadBytes = 10 * 1024 * 1024;
    public const int MaxWidthPx = 8000;
    public const int MaxHeightPx = 8000;
    public const int MaxMegapixels = 40;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/heic",
        "image/heif"
    };

    public static bool IsAllowedContentType(string? contentType)
        => !string.IsNullOrWhiteSpace(contentType) && AllowedContentTypes.Contains(contentType);

    /// <summary>
    /// Allowed: local upload paths (dev fallback) or HTTPS URLs that are not private/localhost.
    /// When <paramref name="media"/> is configured, HTTPS hosts are restricted to the media public host
    /// (plus existing relative upload paths for legacy drafts).
    /// </summary>
    public static bool IsAllowed(string? url, CloudflareMediaSettings? media = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var trimmed = url.Trim();
        if (trimmed.StartsWith(UploadPrefix, StringComparison.OrdinalIgnoreCase)
            && !trimmed.Contains("..", StringComparison.Ordinal)
            && trimmed.Length < 500)
        {
            return true;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || uri.Host.Length == 0
            || IsBlockedHost(uri.Host))
        {
            return false;
        }

        if (media?.IsConfigured == true)
        {
            if (!Uri.TryCreate(media.ResolvedPublicBaseUrl, UriKind.Absolute, out var allowed)
                || !string.Equals(uri.Host, allowed.Host, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsBlockedHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host is "127.0.0.1" or "::1" or "0.0.0.0")
        {
            return true;
        }

        if (System.Net.IPAddress.TryParse(host, out var ip))
        {
            return System.Net.IPAddress.IsLoopback(ip)
                   || ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                      && IsPrivateIpv4(ip);
        }

        return false;
    }

    private static bool IsPrivateIpv4(System.Net.IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        return bytes[0] == 10
               || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
               || (bytes[0] == 192 && bytes[1] == 168)
               || (bytes[0] == 169 && bytes[1] == 254);
    }

    public static string ExtensionForContentType(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/heic" or "image/heif" => ".heic",
        _ => ".jpg"
    };

    /// <summary>
    /// Extracts R2 storage key from a media delivery URL
    /// (e.g. https://media…/m/masters/1/abc/v1?v=card → masters/1/abc/v1).
    /// </summary>
    public static bool TryGetStorageKey(string? url, out string storageKey)
    {
        storageKey = string.Empty;
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var trimmed = url.Trim();
        string path;
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            path = uri.AbsolutePath;
        }
        else if (trimmed.StartsWith("/m/", StringComparison.OrdinalIgnoreCase))
        {
            path = trimmed.Split('?', 2)[0];
        }
        else
        {
            return false;
        }

        const string marker = "/m/";
        var idx = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return false;
        }

        var key = Uri.UnescapeDataString(path[(idx + marker.Length)..]).Trim('/');
        if (string.IsNullOrWhiteSpace(key)
            || key.Contains("..", StringComparison.Ordinal)
            || !key.StartsWith("masters/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        storageKey = key;
        return true;
    }

    /// <summary>
    /// Resolves a hard-deletable storage key from asset metadata or a delivery/local URL.
    /// </summary>
    public static bool TryResolveStorageKey(ListingImageAsset? asset, string? url, out string storageKey)
    {
        storageKey = string.Empty;
        if (!string.IsNullOrWhiteSpace(asset?.StorageKey))
        {
            storageKey = asset.StorageKey.Trim();
            return true;
        }

        var target = !string.IsNullOrWhiteSpace(url) ? url.Trim() : asset?.DeliveryUrl?.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        if (TryGetStorageKey(target, out storageKey))
        {
            return true;
        }

        if (target.StartsWith(UploadPrefix, StringComparison.OrdinalIgnoreCase)
            && !target.Contains("..", StringComparison.Ordinal))
        {
            storageKey = target;
            return true;
        }

        return false;
    }
}

/// <summary>Injects media settings into static URL checks used by validators.</summary>
public sealed class ListingImageUrlPolicy(IOptions<CloudflareMediaSettings> mediaOptions)
{
    public bool IsAllowed(string? url) => ListingImageUrl.IsAllowed(url, mediaOptions.Value);
}
