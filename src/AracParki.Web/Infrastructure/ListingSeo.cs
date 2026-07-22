using System.Globalization;
using System.Text;
using AracParki.Application.Listings.Queries;
using AracParki.Domain.Listings;
using Microsoft.AspNetCore.WebUtilities;

namespace AracParki.Web.Infrastructure;

/// <summary>
/// List-page indexing policy: allowlist tip/kategoriId/markaId/ilId;
/// noindex sort, search, pagination, and thin multi-facet URLs.
/// </summary>
public static class ListingSeo
{
    public const string IndexRobots = "index, follow, max-image-preview:large";
    public const string NoIndexRobots = "noindex, follow";

    public static bool IsIndexableList(ListingSearchQuery filter)
    {
        if (filter.Page > 1)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.Sort) && filter.Sort != ListingSort.Newest)
        {
            return false;
        }

        if (HasNonAllowlistedFilters(filter))
        {
            return false;
        }

        // Name-only filters without IDs create duplicate/ambiguous URLs.
        if (filter.CategoryId is null && !string.IsNullOrWhiteSpace(filter.Category))
        {
            return false;
        }

        if (filter.CityIds.Count == 0 && !string.IsNullOrWhiteSpace(filter.City))
        {
            return false;
        }

        // Multi-city combos are thin facet permutations.
        if (filter.CityIds.Count > 1)
        {
            return false;
        }

        return true;
    }

    public static string BuildCanonicalListPath(ListingSearchQuery filter)
    {
        var dict = new Dictionary<string, string?>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(filter.Intent) && filter.Intent != ListingIntent.All)
        {
            dict["tip"] = filter.Intent;
        }

        if (filter.CategoryId is > 0)
        {
            dict["kategoriId"] = filter.CategoryId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (filter.BrandId is > 0)
        {
            dict["markaId"] = filter.BrandId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (filter.CityIds.Count == 1 && filter.CityIds[0] > 0)
        {
            dict["ilId"] = filter.CityIds[0].ToString(CultureInfo.InvariantCulture);
        }

        return dict.Count == 0
            ? ListingRoutes.List
            : QueryHelpers.AddQueryString(ListingRoutes.List, dict);
    }

    public static (string Title, string Description, string Heading) BuildListMeta(
        ListingSearchQuery filter,
        string? categoryName,
        string? brandName,
        string? cityName,
        int totalCount)
    {
        var intentLabel = filter.Intent is ListingIntent.Satilik or ListingIntent.Kiralik
            ? ListingIntent.Label(filter.Intent)
            : null;

        var parts = new List<string>(4);
        if (intentLabel is not null)
        {
            parts.Add(intentLabel);
        }

        if (!string.IsNullOrWhiteSpace(brandName))
        {
            parts.Add(brandName);
        }

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            parts.Add(categoryName);
        }
        else if (parts.Count == 0 || intentLabel is not null)
        {
            parts.Add("İş Makinesi");
        }

        if (!string.IsNullOrWhiteSpace(cityName))
        {
            parts.Add(cityName);
        }

        var heading = string.Join(' ', parts) + " İlanları";
        var title = heading + " | Araç Parkı";

        var countPhrase = totalCount > 0
            ? $"{totalCount.ToString("N0", CultureInfo.GetCultureInfo("tr-TR"))} ilan"
            : "Güncel ilanlar";

        var scope = new StringBuilder();
        if (intentLabel is not null)
        {
            scope.Append(intentLabel.ToLowerInvariant());
            scope.Append(' ');
        }
        else
        {
            scope.Append("satılık ve kiralık ");
        }

        if (!string.IsNullOrWhiteSpace(brandName))
        {
            scope.Append(brandName);
            scope.Append(' ');
        }

        scope.Append(string.IsNullOrWhiteSpace(categoryName) ? "iş makinesi" : categoryName.ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(cityName))
        {
            scope.Append(" — ");
            scope.Append(cityName);
        }

        var description =
            $"{heading}: {countPhrase}. {scope} ilanlarını Araç Parkı’nda karşılaştırın.";

        if (description.Length > 160)
        {
            description = description[..157].TrimEnd() + "…";
        }

        return (title, description, heading);
    }

    public static (string Title, string Description) BuildDetailMeta(
        string title,
        int modelYear,
        int? hours,
        string city,
        string district,
        decimal price,
        string currency,
        string? priceUnit,
        string intent)
    {
        var hoursPart = hours is null ? string.Empty : $", {hours.Value.ToString("N0", CultureInfo.GetCultureInfo("tr-TR"))} saat";
        var location = string.IsNullOrWhiteSpace(district) ? city : $"{city} / {district}";
        var priceText = FormatPricePlain(price, priceUnit, currency, intent);
        var pageTitle = $"{title} | Araç Parkı";
        var description =
            $"{title} — {modelYear}{hoursPart}, {location}. {priceText}. İş makinesi ilanı Araç Parkı’nda.";

        if (description.Length > 160)
        {
            description = description[..157].TrimEnd() + "…";
        }

        return (pageTitle, description);
    }

    private static bool HasNonAllowlistedFilters(ListingSearchQuery filter)
        => filter.ModelId is > 0
           || filter.DistrictIds.Count > 0
           || !string.IsNullOrWhiteSpace(filter.Condition)
           || !string.IsNullOrWhiteSpace(filter.SellerType)
           || filter.YearMin is not null
           || filter.YearMax is not null
           || filter.HoursMin is not null
           || filter.HoursMax is not null
           || filter.WeightMin is not null
           || filter.WeightMax is not null
           || filter.PriceMin is not null
           || filter.PriceMax is not null
           || filter.HorsepowerMin is not null
           || filter.HorsepowerMax is not null
           || filter.CapacityKgMin is not null
           || filter.CapacityKgMax is not null
           || filter.IncludesOperator is not null
           || !string.IsNullOrWhiteSpace(filter.PriceUnit)
           || filter.VerifiedOnly
           || filter.AttachmentIds.Count > 0
           || filter.SpecValues.Count > 0;

    private static string FormatPricePlain(decimal price, string? priceUnit, string currency, string intent)
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        var amount = price.ToString("N0", tr);
        var cur = string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) ? "$"
            : string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase) ? "€"
            : "₺";

        if (intent == ListingIntent.Kiralik && !string.IsNullOrWhiteSpace(priceUnit))
        {
            var unit = priceUnit switch
            {
                "day" => "gün",
                "week" => "hafta",
                "month" => "ay",
                "hour" => "saat",
                _ => priceUnit
            };
            return $"{amount} {cur}/{unit}";
        }

        return $"{amount} {cur}";
    }
}
