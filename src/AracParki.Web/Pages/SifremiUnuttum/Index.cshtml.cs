using AracParki.Application.Accounts.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;

namespace AracParki.Web.Pages.SifremiUnuttum;

public sealed class IndexModel(AccountService accounts, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty]
    public ForgotInput Input { get; set; } = new();

    public bool Submitted { get; private set; }
    public string? FormError { get; private set; }

    public IActionResult OnGet()
    {
        ViewData["PageKey"] = "auth";
        ViewData["Title"] = "Şifremi Unuttum | Araç Parkı";
        ViewData["Description"] = "Araç Parkı hesap şifreni sıfırla.";
        return Page();
    }

    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        ViewData["PageKey"] = "auth";
        ViewData["Title"] = "Şifremi Unuttum | Araç Parkı";

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await accounts.RequestPasswordResetAsync(Input.Email, cancellationToken);
            Submitted = true;
            return Page();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Password reset email failed for {Email}", Input.Email);
            // Anti-enumeration: still show success copy when possible; only surface infra failure
            FormError = "E-posta şu an gönderilemedi. Biraz sonra tekrar dene.";
            return Page();
        }
    }

    public sealed class ForgotInput
    {
        [Required(ErrorMessage = "E-posta gerekli.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
        [Display(Name = "E-posta")]
        public string Email { get; set; } = string.Empty;
    }
}
