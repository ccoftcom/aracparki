using System.Globalization;
using AracParki.Application.Catalog.Dtos;
using AracParki.Application.Catalog.Services;
using AracParki.Application.Listings;
using AracParki.Application.Listings.Dtos;
using AracParki.Application.Listings.Queries;
using AracParki.Application.Listings.Services;
using AracParki.Domain.Listings;
using AracParki.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AracParki.Web.Pages.Ilan;

public sealed class IndexModel(ListingService listingService, CatalogService catalog, SiteUrls siteUrls) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string AdNo { get; set; } = string.Empty;

    public ListingDetailDto? Listing { get; private set; }
    public IReadOnlyList<SpecDisplayRow> SpecRows { get; private set; } = [];
    public IReadOnlyList<ListingCardDto> Similar { get; private set; } = [];
    public IReadOnlyList<CategorySummaryDto> CategoryNav { get; private set; } = [];
    public IReadOnlyList<BreadcrumbItem> BreadcrumbTrail { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(AdNo))
        {
            return NotFound();
        }

        var access = ListingAccessContext.FromPrincipal(User);
        var listingTask = listingService.GetByAdNoAsync(AdNo, access, cancellationToken);
        var categoryNavTask = catalog.GetCategoriesWithCountsAsync(cancellationToken);
        await Task.WhenAll(listingTask, categoryNavTask);

        Listing = await listingTask;
        CategoryNav = await categoryNavTask;
        if (Listing is null)
        {
            return NotFound();
        }

        if (Listing.CategoryId > 0)
        {
            var attrs = await catalog.GetCategoryAttributesAsync(Listing.CategoryId, cancellationToken);
            SpecRows = SpecsJsonBuilder.ToDisplayRows(Listing.SpecsJson, attrs);

            if (Listing.Status == ListingStatus.Published)
            {
                var similar = await listingService.SearchAsync(new ListingSearchQuery
                {
                    Intent = Listing.PrimaryIntent,
                    CategoryId = Listing.CategoryId,
                    Page = 1,
                    PageSize = 5
                }, cancellationToken);
                Similar = similar.Items.Where(x => x.AdNo != Listing.AdNo).Take(4).ToArray();
            }
        }

        ViewData["PageKey"] = "detail";
        var (seoTitle, seoDescription) = ListingSeo.BuildDetailMeta(
            Listing.Title,
            Listing.ModelYear,
            Listing.Hours,
            Listing.City,
            Listing.District,
            Listing.Price,
            Listing.Currency,
            Listing.PriceUnit,
            Listing.PrimaryIntent);
        ViewData["Title"] = seoTitle;
        ViewData["OgTitle"] = seoTitle;
        ViewData["OgType"] = "product";
        ViewData["Description"] = seoDescription;
        ViewData["SearchQuery"] = string.Empty;
        ViewData["CanonicalIncludeQuery"] = false;
        if (Listing.Status != ListingStatus.Published)
        {
            ViewData["Robots"] = "noindex, nofollow";
        }

        var cover = !string.IsNullOrWhiteSpace(Listing.CoverImageUrl)
            ? Listing.CoverImageUrl
            : Listing.ImageUrls.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(cover))
        {
            ViewData["OgImage"] = ListingImageUrlVariants.WithVariant(cover, ListingImageVariants.Og);
            ViewData["OgImageAlt"] = Listing.Title;
            ViewData["TwitterCard"] = "summary_large_image";
        }

        BreadcrumbTrail = BuildBreadcrumbTrail(Listing);
        if (Listing.Status == ListingStatus.Published)
        {
            ViewData["JsonLd"] = BuildProductJsonLd(Listing);
            Breadcrumbs.Set(ViewData, siteUrls, BreadcrumbTrail);
        }

        return Page();
    }

    public string CrumbIntent => Listing?.PrimaryIntent switch
    {
        ListingIntent.Kiralik => ListingIntent.Kiralik,
        _ => ListingIntent.Satilik
    };

    public string RootNavLabel =>
        Listing is { CategoryId: > 0 }
            ? CategoryNav.FirstOrDefault(c => c.Id == Listing.CategoryId)?.GroupName
              ?? CategoryNav.Select(c => c.GroupName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
              ?? "İş Makineleri"
            : "İş Makineleri";

    public string RootNavUrl() => ListingRoutes.List;

    public string IntentNavUrl(string intent) => ListingRoutes.HubUrl(intent);

    public string CategoryNavUrl(int categoryId)
    {
        var slug = CategoryNav.FirstOrDefault(c => c.Id == categoryId)?.Slug
            ?? (Listing?.CategoryId == categoryId ? Listing.CategorySlug : null);
        return ListingRoutes.ListUrl(new ListingSearchQuery
        {
            Intent = CrumbIntent,
            CategoryId = categoryId
        }, categorySlug: slug);
    }

    private IReadOnlyList<BreadcrumbItem> BuildBreadcrumbTrail(ListingDetailDto listing)
    {
        var intent = listing.PrimaryIntent switch
        {
            ListingIntent.Kiralik => ListingIntent.Kiralik,
            _ => ListingIntent.Satilik
        };

        var groupLabel = RootNavLabel;
        var showGroup = !string.Equals(groupLabel, "İş Makineleri", StringComparison.Ordinal);
        var items = new List<BreadcrumbItem>
        {
            new("Anasayfa", "/"),
            new(groupLabel, RootNavUrl())
        };

        if (showGroup)
        {
            items.Add(new BreadcrumbItem("İş Makineleri", RootNavUrl()));
        }

        items.Add(new BreadcrumbItem(ListingIntent.Label(intent), IntentNavUrl(intent)));

        if (listing.CategoryId > 0 && !string.IsNullOrWhiteSpace(listing.Category))
        {
            items.Add(new BreadcrumbItem(listing.Category, CategoryNavUrl(listing.CategoryId)));
        }

        items.Add(new BreadcrumbItem(listing.Title, $"/ilan/{listing.AdNo}"));
        return items;
    }

    private string BuildProductJsonLd(ListingDetailDto listing)
    {
        var images = listing.ImageUrls.Count > 0
            ? listing.ImageUrls.Select(u => siteUrls.Absolute(ListingImageUrlVariants.WithVariant(u, ListingImageVariants.Lg))).ToList()
            : string.IsNullOrWhiteSpace(listing.CoverImageUrl)
                ? new List<string>()
                : [siteUrls.Absolute(ListingImageUrlVariants.WithVariant(listing.CoverImageUrl, ListingImageVariants.Lg))];

        var sellerName = !string.IsNullOrWhiteSpace(listing.CorporateDisplayName)
            ? listing.CorporateDisplayName
            : listing.SellerName;

        var availability = listing.Status == ListingStatus.Published
            ? "https://schema.org/InStock"
            : "https://schema.org/OutOfStock";

        var additionalProperties = new List<Dictionary<string, object?>>
        {
            Prop("Model yılı", listing.ModelYear.ToString(CultureInfo.InvariantCulture)),
            Prop("Şehir", listing.City),
            Prop("İlçe", listing.District)
        };

        if (listing.Hours is int hours)
        {
            additionalProperties.Add(Prop("Çalışma saati", hours.ToString(CultureInfo.InvariantCulture)));
        }

        if (listing.Tons > 0)
        {
            additionalProperties.Add(Prop("Tonaj", listing.Tons.ToString("0.##", CultureInfo.InvariantCulture)));
        }

        if (listing.Horsepower is > 0)
        {
            additionalProperties.Add(Prop("Beygir gücü", listing.Horsepower.Value.ToString(CultureInfo.InvariantCulture)));
        }

        if (!string.IsNullOrWhiteSpace(listing.ModelName))
        {
            additionalProperties.Add(Prop("Model", listing.ModelName));
        }

        var product = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["name"] = listing.Title,
            ["description"] = ListingDescriptionHtml.ToPlainText(listing.Description),
            ["sku"] = listing.AdNo,
            ["mpn"] = listing.AdNo,
            ["brand"] = new Dictionary<string, object?>
            {
                ["@type"] = "Brand",
                ["name"] = listing.Brand
            },
            ["category"] = listing.Category,
            ["image"] = images,
            ["additionalProperty"] = additionalProperties,
            ["offers"] = new Dictionary<string, object?>
            {
                ["@type"] = "Offer",
                ["url"] = siteUrls.Absolute($"/ilan/{listing.AdNo}"),
                ["priceCurrency"] = Currency.Normalize(listing.Currency),
                ["price"] = listing.Price.ToString("0.##", CultureInfo.InvariantCulture),
                ["availability"] = availability,
                ["itemCondition"] = listing.Condition.Contains("sıfır", StringComparison.OrdinalIgnoreCase)
                    || listing.Condition.Contains("sifir", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(listing.Condition, "new", StringComparison.OrdinalIgnoreCase)
                    ? "https://schema.org/NewCondition"
                    : "https://schema.org/UsedCondition",
                ["seller"] = new Dictionary<string, object?>
                {
                    ["@type"] = listing.CorporateAccountId is > 0 ? "Organization" : "Person",
                    ["name"] = sellerName
                }
            }
        };

        return System.Text.Json.JsonSerializer.Serialize(product);
    }

    private static Dictionary<string, object?> Prop(string name, string value)
        => new()
        {
            ["@type"] = "PropertyValue",
            ["name"] = name,
            ["value"] = value
        };

    public string BackToListUrl()
    {
        if (Listing is null)
        {
            return ListingRoutes.List;
        }

        var intent = Listing.PrimaryIntent switch
        {
            ListingIntent.Kiralik => ListingIntent.Kiralik,
            _ => ListingIntent.Satilik
        };

        return ListingRoutes.ListUrl(new()
        {
            Intent = intent,
            Category = Listing.Category
        });
    }
}
