using System.Text.Json;
using AracParki.Application.Corporate;
using AracParki.Application.Corporate.Dtos;
using AracParki.Application.Listings.Dtos;
using AracParki.Application.Listings.Queries;
using AracParki.Application.Listings.Services;
using AracParki.Domain.Listings;
using AracParki.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AracParki.Web.Pages.Satici;

public sealed class IndexModel(
    ICorporateAccountStore corporateStore,
    ListingService listingService,
    SiteUrls siteUrls) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Slug { get; set; } = string.Empty;

    public PublicDealerDto? Dealer { get; private set; }
    public ListingSearchResult Result { get; private set; } = new()
    {
        Items = [],
        TotalCount = 0,
        Page = 1,
        PageSize = 24,
        HasMore = false
    };

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Slug))
        {
            return NotFound();
        }

        Dealer = await corporateStore.GetApprovedPublicBySlugAsync(Slug.Trim(), cancellationToken);
        if (Dealer is null)
        {
            return NotFound();
        }

        Result = await listingService.SearchAsync(new ListingSearchQuery
        {
            CorporateAccountId = Dealer.Id,
            Page = 1,
            PageSize = 24,
            Sort = ListingSort.Newest
        }, cancellationToken);

        ViewData["PageKey"] = "list";
        ViewData["Title"] = $"{Dealer.DisplayName} İş Makinesi İlanları | Araç Parkı";
        ViewData["Description"] =
            $"{Dealer.DisplayName} — {Dealer.CityName} / {Dealer.DistrictName}. Onaylı satıcı ilanlarını Araç Parkı’nda inceleyin.";
        ViewData["CanonicalIncludeQuery"] = false;
        ViewData["CanonicalUrl"] = siteUrls.Absolute(ListingRoutes.Dealer(Dealer.Slug));
        if (!string.IsNullOrWhiteSpace(Dealer.LogoUrl))
        {
            ViewData["OgImage"] = Dealer.LogoUrl;
            ViewData["OgImageAlt"] = Dealer.DisplayName;
        }

        ViewData["JsonLd"] = BuildLocalBusinessJsonLd(Dealer);
        Breadcrumbs.Set(ViewData, siteUrls,
            Breadcrumbs.Create(
                new BreadcrumbItem("Anasayfa", "/"),
                new BreadcrumbItem("Satıcılar", ListingRoutes.List),
                new BreadcrumbItem(Dealer.DisplayName, ListingRoutes.Dealer(Dealer.Slug))));

        return Page();
    }

    private string BuildLocalBusinessJsonLd(PublicDealerDto dealer)
    {
        var ld = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "LocalBusiness",
            ["name"] = dealer.DisplayName,
            ["url"] = siteUrls.Absolute(ListingRoutes.Dealer(dealer.Slug)),
            ["address"] = new Dictionary<string, object?>
            {
                ["@type"] = "PostalAddress",
                ["addressLocality"] = dealer.CityName,
                ["addressRegion"] = dealer.DistrictName,
                ["streetAddress"] = dealer.AddressLine,
                ["addressCountry"] = "TR"
            }
        };

        if (!string.IsNullOrWhiteSpace(dealer.Phone))
        {
            ld["telephone"] = dealer.Phone;
        }

        if (!string.IsNullOrWhiteSpace(dealer.Email))
        {
            ld["email"] = dealer.Email;
        }

        if (!string.IsNullOrWhiteSpace(dealer.Website))
        {
            ld["sameAs"] = dealer.Website;
        }

        if (!string.IsNullOrWhiteSpace(dealer.LogoUrl))
        {
            ld["image"] = siteUrls.Absolute(dealer.LogoUrl);
        }

        return JsonSerializer.Serialize(ld);
    }
}
