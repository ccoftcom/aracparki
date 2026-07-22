using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace AracParki.Web.Infrastructure;

/// <summary>Single crumb in a trail. <see cref="Url"/> is null for the current page.</summary>
public sealed record BreadcrumbItem(string Name, string? Url = null);

/// <summary>Builds HTML trail models and schema.org BreadcrumbList JSON-LD.</summary>
public static class Breadcrumbs
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    public const string ViewDataKey = "Breadcrumbs";
    public const string JsonLdViewDataKey = "BreadcrumbJsonLd";

    public static IReadOnlyList<BreadcrumbItem> Create(params BreadcrumbItem[] items)
        => items;

    /// <summary>Stores trail for optional HTML rendering and emits BreadcrumbList JSON-LD.</summary>
    public static void Set(
        ViewDataDictionary viewData,
        SiteUrls siteUrls,
        params BreadcrumbItem[] items)
        => Set(viewData, siteUrls, (IReadOnlyList<BreadcrumbItem>)items);

    public static void Set(
        ViewDataDictionary viewData,
        SiteUrls siteUrls,
        IReadOnlyList<BreadcrumbItem> items)
    {
        ArgumentNullException.ThrowIfNull(viewData);
        ArgumentNullException.ThrowIfNull(siteUrls);
        if (items is null || items.Count == 0)
        {
            return;
        }

        viewData[ViewDataKey] = items;
        viewData[JsonLdViewDataKey] = ToJsonLd(items, siteUrls);
    }

    public static string ToJsonLd(IReadOnlyList<BreadcrumbItem> items, SiteUrls siteUrls)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(siteUrls);

        var listElements = new List<Dictionary<string, object?>>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var crumb = items[i];
            var element = new Dictionary<string, object?>
            {
                ["@type"] = "ListItem",
                ["position"] = i + 1,
                ["name"] = crumb.Name
            };

            // Prefer explicit URL; for the last crumb fall back to the current canonical URL.
            if (!string.IsNullOrWhiteSpace(crumb.Url))
            {
                element["item"] = siteUrls.Absolute(crumb.Url);
            }
            else if (i == items.Count - 1)
            {
                element["item"] = siteUrls.CanonicalFromRequest(includeQuery: false);
            }

            listElements.Add(element);
        }

        var graph = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = listElements
        };

        return JsonSerializer.Serialize(graph, JsonOptions);
    }
}
