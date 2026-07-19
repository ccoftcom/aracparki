using System.Security.Claims;
using AracParki.Application.Accounts.Services;
using AracParki.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace AracParki.Web.Pages.Giris;

[EnableRateLimiting("auth-sensitive")]
public sealed class IndexModel(AccountService accounts, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public string? ReturnUrl { get; private set; }
    public string? FormError { get; private set; }
    public bool NeedsEmailConfirm { get; private set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(SafeReturn(returnUrl));
        }

        ReturnUrl = returnUrl;
        SetMeta();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        ReturnUrl = returnUrl;
        SetMeta();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var (ok, error, account) = await accounts.LoginAsync(Input.Email, Input.Password, cancellationToken);
        if (error == "email_unconfirmed" && account is not null)
        {
            NeedsEmailConfirm = true;
            FormError = "E-posta adresini henüz onaylamadın. Gelen kutundaki bağlantıyı kullan veya yeniden gönder.";
            try
            {
                await accounts.ResendEmailVerificationAsync(account.Email, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Resend verification failed");
            }

            return Page();
        }

        if (!ok || account is null)
        {
            FormError = error ?? "Giriş başarısız.";
            return Page();
        }

        var props = new AuthenticationProperties
        {
            IsPersistent = Input.RememberMe,
            ExpiresUtc = Input.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : null
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AuthCookie.CreatePrincipal(account),
            props);

        return LocalRedirect(SafeReturn(returnUrl));
    }

    private void SetMeta()
    {
        ViewData["PageKey"] = "auth";
        ViewData["Title"] = "Giriş Yap | Araç Parkı";
        ViewData["Description"] = "Araç Parkı hesabına giriş yap — ilanlarını yönet, teklifleri takip et.";
    }

    private string SafeReturn(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : "/";
}
