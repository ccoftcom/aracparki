using System.Globalization;
using System.Xml.Linq;
using AracParki.Application.Catalog.Services;
using AracParki.Application.Listings.Services;
using AracParki.Domain.Listings;
using Microsoft.Extensions.Caching.Memory;

namespace AracParki.Web.Infrastructure;

public static class SitemapEndpoints
{
    public const int UrlsPerSitemap = 40_000;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    public static IEndpointRouteBuilder MapSitemaps(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/sitemap.xml", GetIndexAsync);
        endpoints.MapGet("/sitemap-static.xml", GetStaticAsync);
        endpoints.MapGet("/sitemap-hubs.xml", GetHubsAsync);
        endpoints.MapGet("/sitemap-dealers.xml", GetDealersAsync);
        endpoints.MapGet("/sitemap-listings-{page:int}.xml", GetListingsAsync);
        return endpoints;
    }

    private static async Task<IResult> GetIndexAsync(
        ListingService listings,
        SiteUrls siteUrls,
        IMemoryCache cache,
        CancellationToken cancellationToken)
    {
        var xml = await cache.GetOrCreateAsync("seo:sitemap:index", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            var total = await listings.CountPublishedAsync(cancellationToken);
            var listingPages = Math.Max(1, (int)Math.Ceiling(total / (double)UrlsPerSitemap));
            var now = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            var root = new XElement(Ns + "sitemapindex",
                SitemapRef(siteUrls.Absolute("/sitemap-static.xml"), now),
                SitemapRef(siteUrls.Absolute("/sitemap-hubs.xml"), now),
                SitemapRef(siteUrls.Absolute("/sitemap-dealers.xml"), now));

            for (var page = 1; page <= listingPages; page++)
            {
                root.Add(SitemapRef(siteUrls.Absolute($"/sitemap-listings-{page}.xml"), now));
            }

            return ToXml(root);
        });

        return XmlResult(xml!);
    }

    private static Task<IResult> GetStaticAsync(SiteUrls siteUrls, IMemoryCache cache)
    {
        var xml = cache.GetOrCreate("seo:sitemap:static", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var urls = new (string Path, string Changefreq, string Priority)[]
            {
                ("/", "daily", "1.0"),
                (ListingRoutes.List, "daily", "0.9"),
                (ListingRoutes.HubUrl(ListingIntent.Satilik), "daily", "0.85"),
                (ListingRoutes.HubUrl(ListingIntent.Kiralik), "daily", "0.85"),
                ("/guvenli-alisveris", "yearly", "0.3"),
                ("/kullanim-kosullari", "yearly", "0.2"),
                ("/gizlilik", "yearly", "0.2"),
                ("/kvkk", "yearly", "0.2")
            };

            var root = new XElement(Ns + "urlset",
                urls.Select(u => UrlEl(siteUrls.Absolute(u.Path), today, u.Changefreq, u.Priority)));
            return ToXml(root);
        });

        return Task.FromResult(XmlResult(xml!));
    }

    private static async Task<IResult> GetHubsAsync(
        CatalogService catalog,
        SiteUrls siteUrls,
        IMemoryCache cache,
        CancellationToken cancellationToken)
    {
        var xml = await cache.GetOrCreateAsync("seo:sitemap:hubs", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            var categories = await catalog.GetCategoriesWithCountsAsync(cancellationToken);
            var cities = (await catalog.GetPopularCitiesAsync(cancellationToken))
                .Where(c => c.ListingCount > 0 && !string.IsNullOrWhiteSpace(c.Slug))
                .Take(40)
                .ToArray();

            var paths = new HashSet<string>(StringComparer.Ordinal)
            {
                ListingRoutes.HubUrl(ListingIntent.Satilik),
                ListingRoutes.HubUrl(ListingIntent.Kiralik)
            };

            foreach (var intent in new[] { ListingIntent.Satilik, ListingIntent.Kiralik })
            {
                foreach (var cat in categories.Where(c => c.ListingCount > 0 && !string.IsNullOrWhiteSpace(c.Slug)))
                {
                    paths.Add(ListingRoutes.HubUrl(intent, cat.Slug));

                    foreach (var city in cities)
                    {
                        paths.Add(ListingRoutes.HubUrl(intent, cat.Slug, city.Slug));
                    }

                    // City-only intent hubs stay query-based (indexable allowlist).
                    foreach (var city in cities)
                    {
                        paths.Add(ListingRoutes.ListUrl(new()
                        {
                            Intent = intent,
                            CityIds = [city.Id]
                        }));
                    }
                }
            }

            var root = new XElement(Ns + "urlset",
                paths.OrderBy(p => p, StringComparer.Ordinal)
                    .Select(p => UrlEl(siteUrls.Absolute(p), today, "daily", "0.7")));
            return ToXml(root);
        });

        return XmlResult(xml!);
    }

    private static async Task<IResult> GetDealersAsync(
        AracParki.Application.Corporate.ICorporateAccountStore corporate,
        SiteUrls siteUrls,
        IMemoryCache cache,
        CancellationToken cancellationToken)
    {
        var xml = await cache.GetOrCreateAsync("seo:sitemap:dealers", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            var dealers = await corporate.ListApprovedForSitemapAsync(cancellationToken);
            var root = new XElement(Ns + "urlset",
                dealers.Select(d => UrlEl(
                    siteUrls.Absolute(ListingRoutes.Dealer(d.Slug)),
                    d.LastModified.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    "weekly",
                    "0.6")));
            return ToXml(root);
        });

        return XmlResult(xml!);
    }

    private static async Task<IResult> GetListingsAsync(
        int page,
        ListingService listings,
        SiteUrls siteUrls,
        IMemoryCache cache,
        CancellationToken cancellationToken)
    {
        if (page < 1)
        {
            return Results.NotFound();
        }

        var xml = await cache.GetOrCreateAsync($"seo:sitemap:listings:{page}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            var skip = (page - 1) * UrlsPerSitemap;
            var items = await listings.ListPublishedForSitemapAsync(skip, UrlsPerSitemap, cancellationToken);
            if (page > 1 && items.Count == 0)
            {
                return null;
            }

            var root = new XElement(Ns + "urlset",
                items.Select(i => UrlEl(
                    siteUrls.Absolute(ListingRoutes.Detail(i.AdNo)),
                    i.LastModified.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    "daily",
                    "0.8")));
            return ToXml(root);
        });

        return xml is null ? Results.NotFound() : XmlResult(xml);
    }

    private static XElement SitemapRef(string loc, string lastmod)
        => new(Ns + "sitemap",
            new XElement(Ns + "loc", loc),
            new XElement(Ns + "lastmod", lastmod));

    private static XElement UrlEl(string loc, string lastmod, string changefreq, string priority)
        => new(Ns + "url",
            new XElement(Ns + "loc", loc),
            new XElement(Ns + "lastmod", lastmod),
            new XElement(Ns + "changefreq", changefreq),
            new XElement(Ns + "priority", priority));

    private static string ToXml(XElement root)
    {
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        using var writer = new StringWriter();
        doc.Save(writer);
        return writer.ToString();
    }

    private static IResult XmlResult(string xml)
        => Results.Content(xml, "application/xml; charset=utf-8");
}
