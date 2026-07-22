using System.Globalization;
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
    public IReadOnlyList<BreadcrumbItem> BreadcrumbTrail { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(AdNo))
        {
            return NotFound();
        }

        var access = ListingAccessContext.FromPrincipal(User);
        Listing = await listingService.GetByAdNoAsync(AdNo, access, cancellationToken);
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

        if (Listing.Status == ListingStatus.Published)
        {
            ViewData["JsonLd"] = BuildProductJsonLd(Listing);
            BreadcrumbTrail = BuildBreadcrumbTrail(Listing);
            Breadcrumbs.Set(ViewData, siteUrls, BreadcrumbTrail);
        }
        else
        {
            BreadcrumbTrail = BuildBreadcrumbTrail(Listing);
        }

        return Page();
    }

    private IReadOnlyList<BreadcrumbItem> BuildBreadcrumbTrail(ListingDetailDto listing)
    {
        var intent = listing.PrimaryIntent switch
        {
            ListingIntent.Kiralik => ListingIntent.Kiralik,
            _ => ListingIntent.Satilik
        };
        var rootUrl = ListingRoutes.List;
        var intentUrl = ListingRoutes.ListUrl(new() { Intent = intent });
        var categoryUrl = ListingRoutes.ListUrl(new()
        {
            Intent = intent,
            CategoryId = listing.CategoryId > 0 ? listing.CategoryId : null,
            Category = listing.CategoryId > 0 ? null : listing.Category
        });

        return Breadcrumbs.Create(
            new BreadcrumbItem("Anasayfa", "/"),
            new BreadcrumbItem("İş Makineleri", rootUrl),
            new BreadcrumbItem(ListingIntent.Label(intent), intentUrl),
            new BreadcrumbItem(listing.Category, categoryUrl),
            new BreadcrumbItem(listing.Title, $"/ilan/{listing.AdNo}"));
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
