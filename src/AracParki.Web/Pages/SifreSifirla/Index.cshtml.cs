using System.ComponentModel.DataAnnotations;
using AracParki.Application.Accounts;
using AracParki.Application.Accounts.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace AracParki.Web.Pages.SifreSifirla;

public sealed class IndexModel(AccountService accounts) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    [BindProperty]
    public ResetInput Input { get; set; } = new();

    public bool Done { get; private set; }
    public string? FormError { get; private set; }

    public IActionResult OnGet()
    {
        ViewData["PageKey"] = "auth";
        ViewData["Title"] = "Yeni Şifre | Araç Parkı";
        if (string.IsNullOrWhiteSpace(Token))
        {
            FormError = "Geçersiz bağlantı.";
        }

        return Page();
    }

    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        ViewData["PageKey"] = "auth";
        ViewData["Title"] = "Yeni Şifre | Araç Parkı";

        if (string.IsNullOrWhiteSpace(Token))
        {
            FormError = "Geçersiz bağlantı.";
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var (ok, error) = await accounts.ResetPasswordAsync(Token, Input.Password, cancellationToken);
        if (!ok)
        {
            FormError = error;
            return Page();
        }

        // Invalidate current browser session; other devices drop via security_stamp.
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Done = true;
        return Page();
    }

    public sealed class ResetInput : IValidatableObject
    {
        [Required(ErrorMessage = "Şifre gerekli.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalı.")]
        [DataType(DataType.Password)]
        [Display(Name = "Yeni şifre")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre tekrarı gerekli.")]
        [Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre tekrar")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            foreach (var error in PasswordRules.Validate(Password, displayName: null, email: null))
            {
                yield return new ValidationResult(error, [nameof(Password)]);
            }
        }
    }
}
