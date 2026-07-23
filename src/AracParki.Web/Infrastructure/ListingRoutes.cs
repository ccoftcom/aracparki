using System.Globalization;
using AracParki.Application.Listings;
using AracParki.Application.Listings.Queries;
using AracParki.Domain.Listings;
using Microsoft.AspNetCore.WebUtilities;

namespace AracParki.Web.Infrastructure;

public static class ListingRoutes
{
    public const string Home = "/";
    public const string List = "/ilanlar";
    public const string DetailPrefix = "/ilan";
    public const string Compare = "/karsilastir";
    public const string SpecQueryPrefix = "oz_";

    public static string Detail(string adNo) => $"{DetailPrefix}/{Uri.EscapeDataString(adNo)}";

    public static string CompareUrl(IEnumerable<string> adNos)
    {
        var list = adNos
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
        if (list.Length == 0)
        {
            return Compare;
        }

        return QueryHelpers.AddQueryString(Compare, "ilanlar", string.Join(",", list));
    }

    public static string Dealer(string slug) => $"/satici/{Uri.EscapeDataString(slug)}";

    /// <summary>
    /// Path hub: /ilanlar/{tip}[/kategoriSlug][/sehirSlug].
    /// Intent must be satilik or kiralik.
    /// </summary>
    public static string HubUrl(string intent, string? categorySlug = null, string? citySlug = null)
    {
        if (intent is not (ListingIntent.Satilik or ListingIntent.Kiralik))
        {
            throw new ArgumentOutOfRangeException(nameof(intent), intent, "Hub URLs require satilik or kiralik.");
        }

        var path = $"{List}/{intent}";
        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            path += "/" + Uri.EscapeDataString(categorySlug.Trim());
            if (!string.IsNullOrWhiteSpace(citySlug))
            {
                path += "/" + Uri.EscapeDataString(citySlug.Trim());
            }
        }

        return path;
    }

    public static string ListUrl(ListingSearchQuery query, string? categorySlug = null, string? citySlug = null)
    {
        if (TryHubPath(query, categorySlug, citySlug, out var hubPath))
        {
            var extra = BuildNonHubQuery(query);
            return extra.Count == 0 ? hubPath : QueryHelpers.AddQueryString(hubPath, extra);
        }

        var dict = BuildFullQuery(query);
        return dict.Count == 0 ? List : QueryHelpers.AddQueryString(List, dict);
    }

    /// <summary>True when tip[/kategori][/şehir] can be expressed as a path hub.</summary>
    public static bool TryHubPath(
        ListingSearchQuery query,
        string? categorySlug,
        string? citySlug,
        out string hubPath)
    {
        hubPath = string.Empty;
        if (query.Intent is not (ListingIntent.Satilik or ListingIntent.Kiralik))
        {
            return false;
        }

        // City-only (no category) stays on query URLs.
        if (query.CityIds.Count == 1 && query.CategoryId is null && string.IsNullOrWhiteSpace(categorySlug))
        {
            return false;
        }

        if (query.CityIds.Count > 1)
        {
            return false;
        }

        if (query.CategoryId is > 0 && string.IsNullOrWhiteSpace(categorySlug))
        {
            return false;
        }

        if (query.CityIds.Count == 1 && string.IsNullOrWhiteSpace(citySlug))
        {
            return false;
        }

        // No category + no city → /ilanlar/{tip}
        if (query.CategoryId is null && string.IsNullOrWhiteSpace(categorySlug) && query.CityIds.Count == 0)
        {
            hubPath = HubUrl(query.Intent);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            hubPath = HubUrl(
                query.Intent,
                categorySlug,
                query.CityIds.Count == 1 ? citySlug : null);
            return true;
        }

        return false;
    }

    private static Dictionary<string, string?> BuildNonHubQuery(ListingSearchQuery query)
    {
        var dict = BuildFullQuery(query);
        dict.Remove("tip");
        dict.Remove("kategoriId");
        dict.Remove("kategori");
        dict.Remove("ilId");
        dict.Remove("il");
        return dict;
    }

    private static Dictionary<string, string?> BuildFullQuery(ListingSearchQuery query)
    {
        var dict = new Dictionary<string, string?>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(query.Intent) && query.Intent != ListingIntent.All)
        {
            dict["tip"] = query.Intent;
        }

        if (query.CategoryId is > 0)
        {
            dict["kategoriId"] = query.CategoryId.Value.ToString(CultureInfo.InvariantCulture);
        }
        else if (!string.IsNullOrWhiteSpace(query.Category))
        {
            dict["kategori"] = query.Category;
        }

        if (query.BrandId is > 0)
        {
            dict["markaId"] = query.BrandId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (query.ModelId is > 0)
        {
            dict["modelId"] = query.ModelId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (query.CityIds.Count > 0)
        {
            dict["ilId"] = string.Join(',', query.CityIds.Where(x => x > 0).Distinct());
        }
        else if (!string.IsNullOrWhiteSpace(query.City))
        {
            dict["il"] = query.City;
        }

        if (query.DistrictIds.Count > 0)
        {
            dict["ilceId"] = string.Join(',', query.DistrictIds.Where(x => x > 0).Distinct());
        }

        if (!string.IsNullOrWhiteSpace(query.Condition))
        {
            dict["durum"] = query.Condition;
        }

        if (!string.IsNullOrWhiteSpace(query.SellerType))
        {
            dict["satici"] = query.SellerType;
        }

        if (query.YearMin is not null) dict["yilMin"] = query.YearMin.Value.ToString(CultureInfo.InvariantCulture);
        if (query.YearMax is not null) dict["yilMax"] = query.YearMax.Value.ToString(CultureInfo.InvariantCulture);
        if (query.HoursMin is not null) dict["saatMin"] = query.HoursMin.Value.ToString(CultureInfo.InvariantCulture);
        if (query.HoursMax is not null) dict["saatMax"] = query.HoursMax.Value.ToString(CultureInfo.InvariantCulture);
        if (query.WeightMin is not null) dict["tonMin"] = query.WeightMin.Value.ToString(CultureInfo.InvariantCulture);
        if (query.WeightMax is not null) dict["tonMax"] = query.WeightMax.Value.ToString(CultureInfo.InvariantCulture);
        if (query.PriceMin is not null) dict["fiyatMin"] = query.PriceMin.Value.ToString(CultureInfo.InvariantCulture);
        if (query.PriceMax is not null) dict["fiyatMax"] = query.PriceMax.Value.ToString(CultureInfo.InvariantCulture);
        if (query.HorsepowerMin is not null) dict["hpMin"] = query.HorsepowerMin.Value.ToString(CultureInfo.InvariantCulture);
        if (query.HorsepowerMax is not null) dict["hpMax"] = query.HorsepowerMax.Value.ToString(CultureInfo.InvariantCulture);
        if (query.CapacityKgMin is not null) dict["kgMin"] = query.CapacityKgMin.Value.ToString(CultureInfo.InvariantCulture);
        if (query.CapacityKgMax is not null) dict["kgMax"] = query.CapacityKgMax.Value.ToString(CultureInfo.InvariantCulture);

        if (query.IncludesOperator is true)
        {
            dict["operator"] = "1";
        }

        if (!string.IsNullOrWhiteSpace(query.PriceUnit))
        {
            dict["birim"] = query.PriceUnit;
        }

        if (query.VerifiedOnly)
        {
            dict["dogrulanmis"] = "1";
        }

        if (query.AttachmentIds.Count > 0)
        {
            dict["ekipman"] = string.Join(',', query.AttachmentIds.Where(x => x > 0).Distinct());
        }

        foreach (var (key, value) in query.SpecValues)
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                dict[SpecQueryPrefix + key] = value;
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            dict["q"] = query.Query;
        }

        if (query.CorporateAccountId is > 0)
        {
            dict["kurumsalId"] = query.CorporateAccountId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(query.Sort) && query.Sort != ListingSort.Newest)
        {
            dict["sort"] = query.Sort;
        }

        if (query.Page > 1)
        {
            dict["sayfa"] = query.Page.ToString(CultureInfo.InvariantCulture);
        }

        return dict;
    }


    public static ListingSearchQuery FromRequest(IQueryCollection query)
    {
        var tip = query["tip"].ToString();
        var sort = query["sort"].ToString();
        var pageRaw = query["sayfa"].ToString();
        var condition = NullIfEmpty(query["durum"].ToString());
        var sellerType = NullIfEmpty(query["satici"].ToString());
        var priceUnit = NullIfEmpty(query["birim"].ToString());

        if (!ListingIntent.Known.Contains(tip))
        {
            tip = ListingIntent.All;
        }

        if (!ListingSort.Known.Contains(sort))
        {
            sort = ListingSort.Newest;
        }

        if (condition is not null && !EquipmentCondition.Known.Contains(condition))
        {
            condition = null;
        }

        if (sellerType is not null && !SellerType.Known.Contains(sellerType))
        {
            sellerType = null;
        }

        if (priceUnit is not null && !PriceUnit.Known.Contains(priceUnit))
        {
            priceUnit = null;
        }

        var specValues = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in query.Keys)
        {
            if (!key.StartsWith(SpecQueryPrefix, StringComparison.Ordinal) || key.Length <= SpecQueryPrefix.Length)
            {
                continue;
            }

            var attrKey = key[SpecQueryPrefix.Length..];
            var value = NullIfEmpty(query[key].ToString());
            if (value is not null)
            {
                specValues[attrKey] = value;
            }
        }

        var attachmentIds = new List<int>();
        foreach (var raw in query["ekipman"])
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0)
                {
                    attachmentIds.Add(id);
                }
            }
        }

        var (equalityJson, minJson) = SpecFilterBuilder.Build(specValues);

        return new ListingSearchQuery
        {
            Intent = string.IsNullOrWhiteSpace(tip) ? ListingIntent.All : tip,
            CategoryId = ParseInt(query["kategoriId"]),
            BrandId = ParseInt(query["markaId"]),
            ModelId = ParseInt(query["modelId"]),
            CityIds = ParseIntList(query["ilId"]),
            DistrictIds = ParseIntList(query["ilceId"]),
            Category = NullIfEmpty(query["kategori"].ToString()),
            City = NullIfEmpty(query["il"].ToString()),
            Condition = condition,
            SellerType = sellerType,
            YearMin = ParseInt(query["yilMin"]),
            YearMax = ParseInt(query["yilMax"]),
            HoursMin = ParseInt(query["saatMin"]),
            HoursMax = ParseInt(query["saatMax"]),
            WeightMin = ParseDecimal(query["tonMin"]),
            WeightMax = ParseDecimal(query["tonMax"]),
            PriceMin = ParseDecimal(query["fiyatMin"]),
            PriceMax = ParseDecimal(query["fiyatMax"]),
            HorsepowerMin = ParseInt(query["hpMin"]),
            HorsepowerMax = ParseInt(query["hpMax"]),
            CapacityKgMin = ParseInt(query["kgMin"]),
            CapacityKgMax = ParseInt(query["kgMax"]),
            IncludesOperator = IsTruthy(query["operator"].ToString()) ? true : null,
            PriceUnit = priceUnit,
            VerifiedOnly = IsTruthy(query["dogrulanmis"].ToString()),
            AttachmentIds = attachmentIds.Distinct().ToArray(),
            SpecValues = specValues,
            SpecsFilterJson = equalityJson,
            SpecMinJson = minJson,
            Query = NullIfEmpty(query["q"].ToString()) is { Length: >= 2 } q ? q : null,
            CorporateAccountId = ParseLong(query["kurumsalId"]),
            Sort = string.IsNullOrWhiteSpace(sort) ? ListingSort.Newest : sort,
            Page = int.TryParse(pageRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var page) && page > 0
                ? Math.Min(page, 500)
                : 1,
            PageSize = 24
        };
    }

    public static string SpecOptionLabel(string value) => SpecOptionLabels.For(value);

    private static bool IsTruthy(string? value)
        => value is "1" or "true" or "True" or "on" or "yes";

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int? ParseInt(string? raw)
        => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;

    private static long? ParseLong(string? raw)
        => long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;

    private static IReadOnlyList<int> ParseIntList(Microsoft.Extensions.Primitives.StringValues values)
    {
        var ids = new List<int>();
        foreach (var raw in values)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0)
                {
                    ids.Add(id);
                }
            }
        }

        return ids.Distinct().ToArray();
    }

    private static decimal? ParseDecimal(string? raw)
        => decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v >= 0 ? v : null;
}

/// <summary>
/// Category sidebar glyphs. Prefer free outline sets (Tabler MIT) where a
/// matching machine icon exists; remaining keys use in-house Lucide-style paths.
/// Vendored sources: wwwroot/lib/tabler/ (MIT).
/// </summary>
public static class CategoryIcons
{
    public static string Svg(string iconKey)
    {
        // Tabler Icons (MIT) — backhoe, bulldozer, forklift, crane, car-crane, truck, building-factory
        var body = iconKey switch
        {
            "excavator" => "<path d=\"M4 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M14 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M6 17h10\"/><path d=\"M4 15h12v-3H9.5L7.5 14H4z\"/><path d=\"M11 12l5-7h3\"/><path d=\"M19 5v4\"/>",
            "wheeled" => "<path d=\"M4 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M14 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M6 17h10\"/><path d=\"M4 15h12v-3H9.5L7.5 14H4z\"/><path d=\"M11 12l4.5-5.5h3\"/><circle cx=\"6\" cy=\"17\" r=\"3.2\"/>",
            "backhoe" => "<path d=\"M2 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M11 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M13 19H4\"/><path d=\"M4 15h9\"/><path d=\"M8 12V7h2a3 3 0 0 1 3 3v5\"/><path d=\"M5 15v-2a1 1 0 0 1 1-1h7\"/><path d=\"M21.12 9.88L18 5l-5 5\"/><path d=\"M21.12 9.88a3 3 0 0 1-2.12 5.12a3 3 0 0 1-2.12-.88l4.24-4.24\"/>",
            "loader" => "<path d=\"M3 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M13 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M5 17h10\"/><path d=\"M3 15h14V11H8z\"/><path d=\"M12 11l5-5h3v4\"/>",
            "skid" => "<path d=\"M4 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M14 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M6 17h10\"/><path d=\"M4 15h14v-3H8z\"/><path d=\"M10 12l5-5h3v3\"/>",
            "forklift" => "<path d=\"M3 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M12 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M7 17h5\"/><path d=\"M3 17v-6h13v6\"/><path d=\"M5 11V7h4\"/><path d=\"M9 11V5h4l3 6\"/><path d=\"M22 15h-3V5\"/><path d=\"M16 13h3\"/>",
            "crane" => "<path d=\"M6 21h6\"/><path d=\"M9 21V3L3 9h18\"/><path d=\"M9 3l10 6\"/><path d=\"M17 9v4a2 2 0 1 1-2 2\"/>",
            "dozer" => "<path d=\"M2 17a2 2 0 1 0 4 0a2 2 0 0 0-4 0\"/><path d=\"M12 17a2 2 0 1 0 4 0a2 2 0 0 0-4 0\"/><path d=\"M19 13v4a2 2 0 0 0 2 2h1\"/><path d=\"M14 19H4\"/><path d=\"M4 15h10\"/><path d=\"M9 11V6h2a3 3 0 0 1 3 3v6\"/><path d=\"M5 15v-3a1 1 0 0 1 1-1h8\"/><path d=\"M19 17h-3\"/>",
            "grader" => "<path d=\"M3 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M17 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M5 17h14\"/><path d=\"M3 15h18v-4H10L8 13.5H3z\"/><path d=\"M11 11V7h4\"/><path d=\"M2 20h20\"/>",
            "roller" => "<circle cx=\"7.5\" cy=\"15.5\" r=\"3.4\"/><circle cx=\"16.5\" cy=\"15.8\" r=\"2.8\"/><path d=\"M10.8 14h2.4\"/><path d=\"M16.5 13V9.5h2\"/><path d=\"M6 12.2V9.8h3\"/>",
            "paver" => "<path d=\"M4 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M14 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M6 17h10\"/><path d=\"M3 15h16v-4H9z\"/><path d=\"M12 11V8h5v3\"/><path d=\"M2 20h20\"/>",
            "mini" => "<path d=\"M5 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M13 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M7 17h8\"/><path d=\"M5 15h12v-3H8z\"/><path d=\"M10 12l3.5-4.5h3\"/>",
            "lift" => "<path d=\"M3 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M15 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M7 18h8m4 0h2v-6a5 5 0 0 0-5-5h-1l1.5 5h4.5\"/><path d=\"M12 18V7h3\"/><path d=\"M3 17v-5h9\"/><path d=\"M4 12V6l18-3v2\"/><path d=\"M8 12V8L4 6\"/>",
            "platform" => "<path d=\"M5 19h14\"/><path d=\"M7 19V9h10v10\"/><path d=\"M9 9V5h6v4\"/><path d=\"M8 5h8\"/>",
            "concrete" => "<path d=\"M4 21c1.147-4.02 1.983-8.027 2-12h6c.017 3.973.853 7.98 2 12\"/><path d=\"M12.5 13H17c.025 2.612.894 5.296 2 8\"/><path d=\"M9 5a2.4 2.4 0 0 1 2-1a2.4 2.4 0 0 1 2 1a2.4 2.4 0 0 0 2 1a2.4 2.4 0 0 0 2-1a2.4 2.4 0 0 1 2-1a2.4 2.4 0 0 1 2 1\"/><path d=\"M3 21h19\"/>",
            "mixer" => "<path d=\"M4 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M14 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M6 17h10\"/><path d=\"M3 15h16v-3H9z\"/><ellipse cx=\"14\" cy=\"9\" rx=\"3.5\" ry=\"3\"/><path d=\"M12 7.5l4 3\"/>",
            "pump" => "<path d=\"M3 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M12 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M5 17h9\"/><path d=\"M3 15h13v-4H8z\"/><path d=\"M12 11l7-6\"/><path d=\"M19 5v4\"/>",
            "crusher" => "<path d=\"M4 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M12 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M6 17h8\"/><path d=\"M4 15h12v-4H9z\"/><path d=\"M12 11l5-6\"/><path d=\"M17 5l2.2 1.2-3.5 6\"/>",
            "dump" => "<path d=\"M5 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M15 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M5 17H3V6a1 1 0 0 1 1-1h9v12m-4 0h6m4 0h2v-6h-8m0-5h5l3 5\"/>",
            "milling" => "<path d=\"M4 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M14 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M6 17h10\"/><path d=\"M3 15h16v-4H9z\"/><path d=\"M11 11V7h6v4\"/><path d=\"M2 20h20\"/><path d=\"M8 21l1.2-2.5h5.6L16 21\"/>",
            _ => "<path d=\"M4 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M12 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0\"/><path d=\"M6 17h8\"/><path d=\"M4 15h12v-4H9z\"/><path d=\"M11 11l4-5h3v3\"/>"
        };

        return "<svg class=\"cat-ico\" viewBox=\"0 0 24 24\" aria-hidden=\"true\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.7\" stroke-linecap=\"round\" stroke-linejoin=\"round\">" + body + "</svg>";
    }
}
