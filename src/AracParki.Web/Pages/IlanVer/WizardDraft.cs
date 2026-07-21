using AracParki.Application.Accounts.Services;
using AracParki.Application.Listings;
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
    public string Condition { get; set; } = "";
    public int ModelYear { get; set; } = DateTime.UtcNow.Year;
    public int? Hours { get; set; }
    public bool HoursUnknown { get; set; }
    public decimal Tons { get; set; }
    public bool TonsFromCatalog { get; set; }
    public int? CapacityKg { get; set; }
    public bool CapacityKgFromCatalog { get; set; }
    public int? Horsepower { get; set; }
    public bool HorsepowerUnknown { get; set; }
    public bool HorsepowerFromCatalog { get; set; }
    public bool CatalogLocked { get; set; }
    public string? SerialNo { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public Dictionary<string, string> Specs { get; set; } = new(StringComparer.Ordinal);
    public string SpecsJson { get; set; } = "{}";
    public List<int> AttachmentIds { get; set; } = [];

    public string PrimaryIntent { get; set; } = "";
    public List<string> Intents { get; set; } = [];
    public decimal Price { get; set; }
    public decimal? RentPrice { get; set; }
    public string? PriceUnit { get; set; }
    public string Currency { get; set; } = Domain.Listings.Currency.Try;
    public bool IncludesOperator { get; set; }
    public string SellerType { get; set; } = Domain.Listings.SellerType.Owner;
    public int CityId { get; set; }
    public string? CityName { get; set; }
    public int DistrictId { get; set; }
    public string? DistrictName { get; set; }
    public int? NeighborhoodId { get; set; }
    public string? NeighborhoodName { get; set; }
    public string Phone { get; set; } = "";
    public bool PhoneVerified { get; set; }

    public List<string> ImageUrls { get; set; } = [];

    /// <summary>Rich metadata from media ingest; kept in sync with <see cref="ImageUrls"/> on upload.</summary>
    public List<ListingImageAsset> ImageAssets { get; set; } = [];

    /// <summary>When set, publish updates this ad instead of creating a new listing.</summary>
    public string? EditingAdNo { get; set; }

    /// <summary>True when the draft has enough progress to offer resume vs new.</summary>
    public bool IsMeaningful
        => Step > 1
           || HasCategory
           || HasIntent
           || ImageUrls.Count > 0
           || !string.IsNullOrWhiteSpace(Title);

    public bool HasCategory => CategoryId > 0;

    public bool HasIntent => PrimaryIntent is ListingIntent.Satilik or ListingIntent.Kiralik
                             && Intents.Count == 1
                             && Intents[0] == PrimaryIntent;

    public bool HasMachine => HasCategory
                              && HasIntent
                              && BrandId > 0
                              && !string.IsNullOrWhiteSpace(ModelName)
                              && ModelYear is >= 1950 and <= 2100
                              && (HoursUnknown || Hours is >= 0)
                              && Tons > 0
                              && (CapacityMetric != "capacity_kg" || CapacityKg is > 0)
                              && (HorsepowerUnknown || Horsepower is >= 0)
                              && !string.IsNullOrWhiteSpace(Title)
                              && !ListingDescriptionHtml.IsBlank(Description)
                              && EquipmentCondition.Known.Contains(Condition);

    public bool HasSaleInfo(bool requirePhoneVerification)
    {
        var isRent = PrimaryIntent == ListingIntent.Kiralik;
        var rentOk = !isRent
                     || (!string.IsNullOrWhiteSpace(PriceUnit)
                         && Domain.Listings.PriceUnit.Known.Contains(PriceUnit));
        var sellerOk = Domain.Listings.SellerType.Known.Contains(SellerType);
        var phoneOk = !requirePhoneVerification
                      || (AccountService.NormalizePhone(Phone) is not null && PhoneVerified);
        return HasIntent
               && rentOk
               && sellerOk
               && phoneOk
               && Price > 0
               && CityId > 0
               && DistrictId > 0;
    }

    public bool HasImages
    {
        get
        {
            var urls = ImageUrls.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).ToArray();
            return urls.Length is >= 1 and <= ListingImageUrl.MaxCount
                   && urls.All(u => ListingImageUrl.IsAllowed(u));
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
