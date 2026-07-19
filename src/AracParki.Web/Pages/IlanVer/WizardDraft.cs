using AracParki.Application.Accounts.Services;
using AracParki.Domain.Listings;

namespace AracParki.Web.Pages.IlanVer;

public sealed class WizardDraft
{
    public int Step { get; set; } = 1;

    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CapacityMetric { get; set; }
    public int GroupId { get; set; }
    public string? GroupName { get; set; }

    public int BrandId { get; set; }
    public string? BrandName { get; set; }
    public int? ModelId { get; set; }
    public string ModelName { get; set; } = "";
    public string Condition { get; set; } = EquipmentCondition.Used;
    public int ModelYear { get; set; } = DateTime.UtcNow.Year;
    public int Hours { get; set; }
    public decimal Tons { get; set; }
    public int? CapacityKg { get; set; }
    public int Horsepower { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public Dictionary<string, string> Specs { get; set; } = new(StringComparer.Ordinal);
    public string SpecsJson { get; set; } = "{}";
    public List<int> AttachmentIds { get; set; } = [];

    public string PrimaryIntent { get; set; } = ListingIntent.Satilik;
    public List<string> Intents { get; set; } = [ListingIntent.Satilik];
    public decimal Price { get; set; }
    public string? PriceUnit { get; set; }
    public bool IncludesOperator { get; set; }
    public int CityId { get; set; }
    public string? CityName { get; set; }
    public int DistrictId { get; set; }
    public string? DistrictName { get; set; }
    public string Phone { get; set; } = "";

    public List<string> ImageUrls { get; set; } = [];

    public bool HasCategory => CategoryId > 0;
    public bool HasMachine => HasCategory
                              && BrandId > 0
                              && !string.IsNullOrWhiteSpace(ModelName)
                              && ModelYear is >= 1950 and <= 2100
                              && Hours >= 0
                              && Tons > 0
                              && Horsepower >= 0
                              && !string.IsNullOrWhiteSpace(Title)
                              && !string.IsNullOrWhiteSpace(Description)
                              && EquipmentCondition.Known.Contains(Condition);

    public bool HasSaleInfo(bool requirePhone)
    {
        var intentsOk = Intents.Count > 0
                        && Intents.Contains(PrimaryIntent)
                        && Intents.All(i => i is ListingIntent.Satilik or ListingIntent.Kiralik);
        var rentOk = !Intents.Contains(ListingIntent.Kiralik)
                     || (!string.IsNullOrWhiteSpace(PriceUnit)
                         && Domain.Listings.PriceUnit.Known.Contains(PriceUnit));
        var phoneOk = !requirePhone || AccountService.NormalizePhone(Phone) is not null;
        return intentsOk && rentOk && phoneOk && Price > 0 && CityId > 0 && DistrictId > 0;
    }

    public bool HasImages
    {
        get
        {
            var urls = ImageUrls.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).ToArray();
            return urls.Length is >= 1 and <= 8
                   && urls.All(u => Uri.TryCreate(u, UriKind.Absolute, out var uri)
                                    && uri.Scheme == Uri.UriSchemeHttps);
        }
    }

    public string SuggestedTitle()
    {
        var brand = BrandName?.Trim() ?? "";
        var model = ModelName.Trim();
        var year = ModelYear > 0 ? ModelYear.ToString() : "";
        var ton = Tons > 0 ? $"{Tons:0.##}t" : "";
        return string.Join(" · ", new[] { $"{brand} {model}".Trim(), year, ton }
            .Where(static part => !string.IsNullOrWhiteSpace(part)));
    }
}
