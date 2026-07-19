using AracParki.Application.Accounts.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace AracParki.Web.Pages.Kayit;

[EnableRateLimiting("auth-sensitive")]
public sealed class IndexModel(AccountService accounts) : PageModel
{
    [BindProperty]
    public RegisterInput Input { get; set; } = new();

    public string? FormError { get; private set; }
    public bool Submitted { get; private set; }
    public bool VerificationEmailSent { get; private set; } = true;

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect("/");
        }

        SetMeta();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        SetMeta();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var (ok, error, emailSent) = await accounts.RegisterAsync(
            Input.Email,
            Input.Password,
            Input.FirstName,
            Input.LastName,
            cancellationToken);

        if (!ok)
        {
            FormError = error ?? "Kayıt başarısız.";
            return Page();
        }

        Submitted = true;
        VerificationEmailSent = emailSent;
        ViewData["Title"] = "E-postanı Kontrol Et | Araç Parkı";
        ViewData["Description"] = "Araç Parkı hesap doğrulama e-postasını kontrol et.";
        return Page();
    }

    private void SetMeta()
    {
        ViewData["PageKey"] = "auth";
        ViewData["Title"] = "Hesap Oluştur | Araç Parkı";
        ViewData["Description"] = "Araç Parkı’na üye ol — satılık ve kiralık iş makinesi ilanı ver.";
    }
}
