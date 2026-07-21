using System.Security.Claims;
using AracParki.Application.Authorization;
using AracParki.Application.Listings;
using AracParki.Application.Listings.Dtos;
using AracParki.Application.Listings.Services;
using AracParki.Domain.Listings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AracParki.Web.Pages.Admin.Ilanlar;

[Authorize(Policy = AuthPolicies.ListingModerate)]
public sealed class DetayModel(
    ListingService listings,
    ListingModerationService moderation) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string AdNo { get; set; } = string.Empty;

    public ListingDetailDto? Listing { get; private set; }
    public string? FormError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(AdNo))
        {
            return NotFound();
        }

        Listing = await listings.GetByAdNoAsync(
            AdNo,
            ListingAccessContext.FromPrincipal(User),
            cancellationToken);
        if (Listing is null)
        {
            return NotFound();
        }

        ViewData["PageKey"] = "account";
        ViewData["Title"] = $"Moderasyon · {Listing.AdNo} | Araç Parkı";
        ViewData["Robots"] = "noindex, nofollow";
        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out var adminId))
        {
            return Challenge();
        }

        try
        {
            await moderation.ApproveAsync(AdNo, adminId, cancellationToken);
            TempData["AuthNotice"] = $"{AdNo} onaylandı ve yayınlandı.";
            return RedirectToPage("/Admin/Ilanlar/Index", new { durum = ListingStatus.PendingReview });
        }
        catch (InvalidOperationException)
        {
            FormError = "Onaylanacak ilan bulunamadı veya durumu uygun değil.";
            await OnGetAsync(cancellationToken);
            return Page();
        }
        catch (Exception)
        {
            FormError = "Onay işlemi başarısız. Lütfen tekrar dene.";
            await OnGetAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostRejectAsync(string? reason, CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out var adminId))
        {
            return Challenge();
        }

        try
        {
            await moderation.RejectAsync(AdNo, adminId, reason, cancellationToken);
            TempData["AuthNotice"] = $"{AdNo} reddedildi.";
            return RedirectToPage("/Admin/Ilanlar/Index", new { durum = ListingStatus.PendingReview });
        }
        catch (ArgumentException ex)
        {
            FormError = ex.Message;
            await OnGetAsync(cancellationToken);
            return Page();
        }
        catch (InvalidOperationException)
        {
            FormError = "Reddedilecek ilan bulunamadı veya durumu uygun değil.";
            await OnGetAsync(cancellationToken);
            return Page();
        }
        catch (Exception)
        {
            FormError = "Red işlemi başarısız. Lütfen tekrar dene.";
            await OnGetAsync(cancellationToken);
            return Page();
        }
    }

    private bool TryGetAdminId(out long adminId)
    {
        adminId = 0;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out adminId) && adminId > 0;
    }
}
