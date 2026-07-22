using AracParki.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AracParki.Web.Pages;

[Authorize]
public abstract class AccountPageModel : PageModel
{
    protected void SetAccountMeta(
        string title,
        string description,
        IReadOnlyList<BreadcrumbItem>? breadcrumbs = null)
    {
        ViewData["PageKey"] = "account";
        ViewData["Title"] = title + " | Araç Parkı";
        ViewData["Description"] = description;
        ViewData["Robots"] = "noindex, nofollow";

        if (breadcrumbs is { Count: > 0 })
        {
            ViewData[Breadcrumbs.ViewDataKey] = breadcrumbs;
            return;
        }

        // Default trail: Panel › current (skip on Panel itself).
        if (!string.Equals(title, "Panel", StringComparison.Ordinal))
        {
            ViewData[Breadcrumbs.ViewDataKey] = Breadcrumbs.Create(
                new BreadcrumbItem("Panel", "/panel"),
                new BreadcrumbItem(title));
        }
    }
}
