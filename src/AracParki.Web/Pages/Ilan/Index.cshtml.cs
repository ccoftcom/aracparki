using AracParki.Application.Listings.Dtos;
using AracParki.Application.Listings.Services;
using AracParki.Domain.Listings;
using AracParki.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AracParki.Web.Pages.Ilan;

public sealed class IndexModel(ListingService listingService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string AdNo { get; set; } = string.Empty;

    public ListingDetailDto? Listing { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(AdNo))
        {
            return NotFound();
        }

        Listing = await listingService.GetByAdNoAsync(AdNo, cancellationToken);
        if (Listing is null)
        {
            return NotFound();
        }

        ViewData["PageKey"] = "detail";
        ViewData["Title"] = $"{Listing.Title} | Araç Parkı";
        ViewData["OgTitle"] = $"{Listing.Title} | Araç Parkı";
        ViewData["Description"] =
            $"{Listing.Title} — {Listing.ModelYear}, {Listing.Hours} saat, {Listing.City}. İş makinesi ilanı.";
        ViewData["SearchQuery"] = string.Empty;

        return Page();
    }

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
