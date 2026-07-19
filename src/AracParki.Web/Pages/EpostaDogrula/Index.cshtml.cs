using System.ComponentModel.DataAnnotations;
using AracParki.Application.Accounts.Services;
using AracParki.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace AracParki.Web.Pages.EpostaDogrula;

[EnableRateLimiting("auth-sensitive")]
public sealed class IndexModel(AccountService accounts, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Ok { get; set; }

    [BindProperty]
    public ResendInput Input { get; set; } = new();

    public bool Confirmed { get; private set; }
    public bool ResendSubmitted { get; private set; }
    public string? FormError { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? email, CancellationToken cancellationToken)
    {
        SetMeta();

        // PRG landing: token already consumed; avoid leaving it in the address bar.
        if (Ok)
        {
            Confirmed = true;
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(Token))
        {
            Input.Email = email.Trim();
        }

        if (string.IsNullOrWhiteSpace(Token))
        {
            return Page();
        }

        var (ok, error, account, _) = await accounts.ConfirmEmailAsync(Token, cancellationToken);
        if (!ok || account is null)
        {
            FormError = error;
            return Page();
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AuthCookie.CreatePrincipal(account),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });

        return RedirectToPage(new { ok = true });
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        SetMeta();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await accounts.ResendEmailVerificationAsync(Input.Email, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Resend verification failed");
            FormError = "Doğrulama e-postası şu an gönderilemedi. Biraz sonra tekrar dene.";
            return Page();
        }

        ResendSubmitted = true;
        return Page();
    }

    private void SetMeta()
    {
        ViewData["PageKey"] = "auth";
        ViewData["Title"] = "E-posta Doğrulama | Araç Parkı";
        ViewData["Description"] = "Araç Parkı hesap e-posta doğrulama.";
    }

    public sealed class ResendInput
    {
        [Required(ErrorMessage = "E-posta gerekli.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
        [Display(Name = "E-posta")]
        public string Email { get; set; } = string.Empty;
    }
}
