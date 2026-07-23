using AracParki.Application.Listings.Dtos;
using AracParki.Application.Listings.Services;
using AracParki.Application.Site.Services;
using AracParki.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AracParki.Web.Pages.Karsilastir;

public sealed class IndexModel(ListingCompareService compare, SiteUrls siteUrls) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "ilanlar")]
    public string? Ilanlar { get; set; }

    public CompareMatrixDto Matrix { get; private set; } = new()
    {
        Columns = [],
        Sections = [],
        RequestedAdNos = [],
        MissingAdNos = []
    };

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var adNos = ListingCompareService.ParseAdNos(Ilanlar);
        Matrix = await compare.BuildAsync(adNos, cancellationToken);

        ViewData["PageKey"] = "compare";
        ViewData["Robots"] = "noindex, follow";

        var count = Matrix.Columns.Count;
        var path = count > 0
            ? ListingRoutes.CompareUrl(Matrix.Columns.Select(c => c.AdNo))
            : ListingRoutes.Compare;
        ViewData["CanonicalUrl"] = siteUrls.Absolute(path);
        ViewData["CanonicalIncludeQuery"] = true;

        if (count >= 2)
        {
            var categories = Matrix.Columns
                .Select(c => c.Category)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToArray();
            var topic = categories.Length == 1
                ? categories[0].ToLowerInvariant()
                : "iş makinesi";
            ViewData["Title"] = $"{count} {topic} karşılaştırma | Araç Parkı";
            ViewData["Description"] =
                $"{count} ilanı yan yana karşılaştır: fiyat, yıl, saat, kapasite ve teknik özellikler.";
        }
        else
        {
            ViewData["Title"] = "İlan karşılaştırma | Araç Parkı";
            ViewData["Description"] =
                "İş makinelerini yan yana karşılaştır. En fazla 4 ilan seç, linki paylaş.";
        }
    }
}
