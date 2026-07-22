using System.Security.Claims;
using AracParki.Application.Listings.Dtos;
using AracParki.Application.Listings.Services;
using AracParki.Domain.Listings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AracParki.Web.Pages.Ilanlarim;

[Authorize]
public sealed class IndexModel(ListingService listings, ListingCommandService commands) : PageModel
{
    private static readonly string[] FilterStatuses =
    [
        ListingStatus.Published,
        ListingStatus.PendingReview,
        ListingStatus.Rejected,
        ListingStatus.Archived
    ];

    [BindProperty(SupportsGet = true, Name = "durum")]
    public string? Status { get; set; }

    public IReadOnlyList<ListingCardDto> Items { get; private set; } = [];
    public int TotalCount { get; private set; }
    public IReadOnlyDictionary<string, int> StatusCounts { get; private set; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
    public string? FormError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return Challenge();
        }

        Status = NormalizeStatus(Status);
        var all = await listings.GetByAccountIdAsync(accountId, 100, cancellationToken);
        TotalCount = all.Count;
        StatusCounts = FilterStatuses.ToDictionary(
            s => s,
            s => all.Count(i => string.Equals(i.Status, s, StringComparison.Ordinal)),
            StringComparer.Ordinal);

        Items = string.IsNullOrEmpty(Status)
            ? all
            : all.Where(i => string.Equals(i.Status, Status, StringComparison.Ordinal)).ToList();

        ViewData["PageKey"] = "account";
        ViewData["Title"] = "İlanlarım | Araç Parkı";
        ViewData["Description"] = "Yayınladığın iş makinesi ilanları";
        ViewData["Robots"] = "noindex, nofollow";

        return Page();
    }

    public async Task<IActionResult> OnPostArchiveAsync(string adNo, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return Challenge();
        }

        try
        {
            await commands.ArchiveAsync(adNo, accountId, cancellationToken);
            TempData["AuthNotice"] = $"{adNo} yayından kaldırıldı.";
            return RedirectToFilter();
        }
        catch (InvalidOperationException)
        {
            FormError = "Yayından kaldırılacak ilan bulunamadı.";
            await OnGetAsync(cancellationToken);
            return Page();
        }
        catch (Exception)
        {
            FormError = "İşlem başarısız. Lütfen tekrar dene.";
            await OnGetAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostRepublishAsync(string adNo, CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return Challenge();
        }

        try
        {
            await commands.RepublishAsync(adNo, accountId, cancellationToken);
            TempData["AuthNotice"] = $"{adNo} incelemeye gönderildi.";
            return RedirectToFilter();
        }
        catch (InvalidOperationException)
        {
            FormError = "Yeniden yayınlanacak ilan bulunamadı.";
            await OnGetAsync(cancellationToken);
            return Page();
        }
        catch (Exception)
        {
            FormError = "İşlem başarısız. Lütfen tekrar dene.";
            await OnGetAsync(cancellationToken);
            return Page();
        }
    }

    private IActionResult RedirectToFilter()
        => string.IsNullOrEmpty(Status)
            ? RedirectToPage()
            : RedirectToPage(new { durum = Status });

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var trimmed = status.Trim();
        return FilterStatuses.Contains(trimmed, StringComparer.Ordinal) ? trimmed : null;
    }

    private bool TryGetAccountId(out long accountId)
    {
        accountId = 0;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out accountId) && accountId > 0;
    }
}
