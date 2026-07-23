using System.Globalization;
using System.Text.RegularExpressions;
using AracParki.Application.Catalog.Services;
using AracParki.Application.Common;
using AracParki.Application.Listings.Dtos;
using AracParki.Domain.Listings;

namespace AracParki.Application.Listings.Services;

public sealed partial class ListingCompareService(
    IListingQuery listingQuery,
    CatalogService catalog)
{
    public const int MaxListings = 4;
    public const string EmptyValue = "—";

    public static IReadOnlyList<string> ParseAdNos(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(MaxListings);
        foreach (var part in raw.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryNormalizeAdNo(part, out var adNo))
            {
                continue;
            }

            if (!seen.Add(adNo))
            {
                continue;
            }

            result.Add(adNo);
            if (result.Count >= MaxListings)
            {
                break;
            }
        }

        return result;
    }

    public static bool TryNormalizeAdNo(string? raw, out string adNo)
    {
        adNo = "";
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim().ToUpperInvariant();
        if (!AdNoRegex().IsMatch(trimmed))
        {
            return false;
        }

        adNo = trimmed;
        return true;
    }

    public static string BuildQueryValue(IEnumerable<string> adNos) =>
        string.Join(",", adNos.Where(a => !string.IsNullOrWhiteSpace(a)));

    public async Task<CompareMatrixDto> BuildAsync(
        IReadOnlyList<string> requestedAdNos,
        CancellationToken cancellationToken)
    {
        var ordered = requestedAdNos
            .Select(a => TryNormalizeAdNo(a, out var n) ? n : null)
            .Where(a => a is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxListings)
            .ToArray();

        if (ordered.Length == 0)
        {
            return new CompareMatrixDto
            {
                Columns = [],
                Sections = [],
                RequestedAdNos = [],
                MissingAdNos = []
            };
        }

        var listings = await listingQuery.GetPublishedByAdNosAsync(ordered, cancellationToken);
        var byAdNo = listings.ToDictionary(l => l.AdNo, StringComparer.OrdinalIgnoreCase);
        var found = new List<ListingDetailDto>(ordered.Length);
        var missing = new List<string>();
        foreach (var adNo in ordered)
        {
            if (byAdNo.TryGetValue(adNo, out var listing))
            {
                found.Add(listing);
            }
            else
            {
                missing.Add(adNo);
            }
        }

        if (found.Count == 0)
        {
            return new CompareMatrixDto
            {
                Columns = [],
                Sections = [],
                RequestedAdNos = ordered,
                MissingAdNos = missing
            };
        }

        var attrsByCategory = new Dictionary<int, IReadOnlyList<Catalog.Dtos.CategoryAttributeDto>>();
        foreach (var categoryId in found.Select(f => f.CategoryId).Distinct())
        {
            attrsByCategory[categoryId] = categoryId > 0
                ? await catalog.GetCategoryAttributesAsync(categoryId, cancellationToken)
                : [];
        }

        var columns = found.Select(MapColumn).ToArray();
        var sections = new List<CompareSectionDto>
        {
            BuildBasicsSection(found),
            BuildTechSection(found, attrsByCategory),
            BuildAttachmentsSection(found)
        };

        return new CompareMatrixDto
        {
            Columns = columns,
            Sections = sections.Where(s => s.Rows.Count > 0).ToArray(),
            RequestedAdNos = ordered,
            MissingAdNos = missing
        };
    }

    private static CompareColumnDto MapColumn(ListingDetailDto listing)
    {
        var sellerName = !string.IsNullOrWhiteSpace(listing.CorporateDisplayName)
            ? listing.CorporateDisplayName!
            : listing.SellerName;
        var sellerType = listing.CorporateAccountId is > 0
            ? "Bayi"
            : SellerType.Label(listing.SellerType);

        return new CompareColumnDto
        {
            AdNo = listing.AdNo,
            Title = listing.Title,
            CoverImageUrl = listing.CoverImageUrl,
            PriceText = FormatPrice(listing),
            Category = listing.Category,
            Brand = listing.Brand,
            ModelName = listing.ModelName,
            DetailUrl = "/ilan/" + Uri.EscapeDataString(listing.AdNo),
            IsVerified = listing.IsVerified,
            SellerLabel = listing.IsVerified ? $"Doğrulanmış · {sellerType}" : $"{sellerType} · {sellerName}"
        };
    }

    private static CompareSectionDto BuildBasicsSection(IReadOnlyList<ListingDetailDto> listings)
    {
        var rows = new List<CompareRowDto>
        {
            Row("price", "Fiyat", listings.Select(FormatPrice).ToArray()),
            Row("intent", "Tip", listings.Select(l => ListingIntent.Label(l.PrimaryIntent)).ToArray()),
            Row("condition", "Durum", listings.Select(l => EquipmentCondition.Label(l.Condition)).ToArray()),
            Row("category", "Kategori", listings.Select(l => l.Category).ToArray()),
            Row("brand", "Marka", listings.Select(l => l.Brand).ToArray()),
            Row("model", "Model", listings.Select(l => l.ModelName).ToArray()),
            Row("year", "Model yılı", listings.Select(l => l.ModelYear.ToString(CultureInfo.InvariantCulture)).ToArray()),
            Row("hours", "Çalışma saati", listings.Select(FormatHours).ToArray()),
            Row("capacity", "Kapasite / ağırlık", listings.Select(FormatCapacity).ToArray()),
            Row("hp", "Motor gücü", listings.Select(FormatHp).ToArray()),
            Row("location", "Konum", listings.Select(l => $"{l.City} / {l.District}").ToArray()),
            Row("seller", "Satıcı", listings.Select(FormatSeller).ToArray()),
        };

        if (listings.Any(l => l.PrimaryIntent == ListingIntent.Kiralik))
        {
            rows.Insert(1, Row("rent", "Kira", listings.Select(FormatRent).ToArray()));
            rows.Insert(2, Row("operator", "Operatör", listings.Select(l =>
                l.PrimaryIntent == ListingIntent.Kiralik
                    ? (l.IncludesOperator ? "Dahil" : "Dahil değil")
                    : EmptyValue).ToArray()));
        }

        return new CompareSectionDto
        {
            Key = "basics",
            Title = "Temel bilgiler",
            Rows = rows
        };
    }

    private static CompareSectionDto BuildTechSection(
        IReadOnlyList<ListingDetailDto> listings,
        IReadOnlyDictionary<int, IReadOnlyList<Catalog.Dtos.CategoryAttributeDto>> attrsByCategory)
    {
        var perListing = listings
            .Select(l => SpecsJsonBuilder.ToDisplayRows(
                l.SpecsJson,
                attrsByCategory.GetValueOrDefault(l.CategoryId) ?? []))
            .Select(rows => rows.ToDictionary(r => r.Label, r => r.Value, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        var labels = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var map in perListing)
        {
            foreach (var label in map.Keys)
            {
                if (seen.Add(label))
                {
                    labels.Add(label);
                }
            }
        }

        var rows = labels.Select(label =>
        {
            var values = perListing
                .Select(map => map.TryGetValue(label, out var v) && !string.IsNullOrWhiteSpace(v) ? v : EmptyValue)
                .ToArray();
            return Row("tech:" + label, label, values);
        }).ToArray();

        return new CompareSectionDto
        {
            Key = "tech",
            Title = "Teknik özellikler",
            Rows = rows
        };
    }

    private static CompareSectionDto BuildAttachmentsSection(IReadOnlyList<ListingDetailDto> listings)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var listing in listings)
        {
            foreach (var att in listing.Attachments)
            {
                if (seen.Add(att.Name))
                {
                    names.Add(att.Name);
                }
            }
        }

        names.Sort(StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), CompareOptions.IgnoreCase));

        var rows = names.Select(name =>
        {
            var values = listings
                .Select(l => l.Attachments.Any(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase))
                    ? "Var"
                    : "Yok")
                .ToArray();
            return Row("att:" + name, name, values);
        }).ToArray();

        return new CompareSectionDto
        {
            Key = "attachments",
            Title = "Ekipmanlar",
            Rows = rows
        };
    }

    public static CompareRowDto Row(string key, string label, IReadOnlyList<string> values) =>
        new()
        {
            Key = key,
            Label = label,
            Values = values,
            IsDifferent = AreDifferent(values)
        };

    public static bool AreDifferent(IReadOnlyList<string> values)
    {
        if (values.Count <= 1)
        {
            return false;
        }

        var normalized = values.Select(NormalizeCompareValue).ToArray();
        return normalized.Distinct(StringComparer.Ordinal).Count() > 1;
    }

    public static string NormalizeCompareValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == EmptyValue)
        {
            return "";
        }

        return string.Join(
            ' ',
            value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string FormatPrice(ListingDetailDto listing) =>
        Formatters.Price(listing.Price, listing.PrimaryIntent == ListingIntent.Kiralik ? listing.PriceUnit : null, listing.Currency);

    private static string FormatRent(ListingDetailDto listing)
    {
        if (listing.PrimaryIntent != ListingIntent.Kiralik || listing.RentPrice is null)
        {
            return EmptyValue;
        }

        return Formatters.Price(listing.RentPrice.Value, listing.PriceUnit, listing.Currency);
    }

    private static string FormatHours(ListingDetailDto listing) =>
        listing.Hours is null ? "Belirtilmedi" : Formatters.Hours(listing.Hours.Value);

    private static string FormatCapacity(ListingDetailDto listing)
    {
        if (listing.CapacityMetric == "capacity_kg" && listing.CapacityKg is not null)
        {
            return listing.CapacityKg.Value.ToString("N0", CultureInfo.GetCultureInfo("tr-TR")) + " kg";
        }

        if (listing.CapacityMetric == "capacity_t")
        {
            return Formatters.Tons(listing.Tons);
        }

        return Formatters.Tons(listing.Tons);
    }

    private static string FormatHp(ListingDetailDto listing) =>
        listing.Horsepower is > 0 ? Formatters.Horsepower(listing.Horsepower.Value) : EmptyValue;

    private static string FormatSeller(ListingDetailDto listing)
    {
        var name = !string.IsNullOrWhiteSpace(listing.CorporateDisplayName)
            ? listing.CorporateDisplayName!
            : listing.SellerName;
        var type = listing.CorporateAccountId is > 0 ? "Bayi" : SellerType.Label(listing.SellerType);
        return listing.IsVerified ? $"{name} ({type}, doğrulanmış)" : $"{name} ({type})";
    }

    [GeneratedRegex(@"^AP-\d{1,12}$", RegexOptions.CultureInvariant)]
    private static partial Regex AdNoRegex();
}
