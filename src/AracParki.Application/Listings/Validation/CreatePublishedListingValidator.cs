using AracParki.Application.Listings.Commands;
using AracParki.Domain.Listings;
using FluentValidation;

namespace AracParki.Application.Listings.Validation;

public sealed class CreatePublishedListingValidator : AbstractValidator<CreatePublishedListingCommand>
{
    public CreatePublishedListingValidator(ListingImageUrlPolicy imageUrlPolicy)
    {
        RuleFor(x => x.AccountId).GreaterThan(0).WithMessage("Hesap geçersiz.");
        RuleFor(x => x.SellerDisplayName).NotEmpty().MaximumLength(120)
            .WithMessage("Satıcı adı gerekli.");
        RuleFor(x => x.SellerType).Must(SellerType.Known.Contains)
            .WithMessage("Satıcı tipi geçersiz.");
        RuleFor(x => x.CorporateAccountId)
            .GreaterThan(0)
            .When(x => x.CorporateAccountId.HasValue)
            .WithMessage("Kurumsal hesap geçersiz.");
        RuleFor(x => x)
            .Must(x => x.SellerType != SellerType.Owner || x.CorporateAccountId is null)
            .WithMessage("Sahibinden ilanda kurumsal hesap seçilemez.");
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Telefon gerekli.")
            .MinimumLength(10).WithMessage("Telefon en az 10 rakam olmalı.")
            .MaximumLength(15).WithMessage("Telefon en fazla 15 rakam olmalı.")
            .Matches(@"^\d+$").WithMessage("Telefon yalnızca rakam içermeli.");

        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Kategori seç.");
        RuleFor(x => x.BrandId).GreaterThan(0).WithMessage("Marka seç.");
        RuleFor(x => x.ModelName).NotEmpty().MaximumLength(120).WithMessage("Model adı gerekli.");
        RuleFor(x => x.SerialNo).MaximumLength(80).When(x => x.SerialNo is not null)
            .WithMessage("Seri no en fazla 80 karakter.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).WithMessage("Başlık gerekli.");
        RuleFor(x => x.Description)
            .Must(d => !ListingDescriptionHtml.IsBlank(d))
            .WithMessage("Açıklama gerekli.")
            .Must(d => ListingDescriptionHtml.Sanitize(d).Length <= ListingDescriptionHtml.MaxLength)
            .WithMessage($"Açıklama en fazla {ListingDescriptionHtml.MaxLength} karakter.");

        RuleFor(x => x.CityId).GreaterThan(0).WithMessage("İl seç.");
        RuleFor(x => x.DistrictId).GreaterThan(0).WithMessage("İlçe seç.");
        RuleFor(x => x.NeighborhoodId).GreaterThan(0).When(x => x.NeighborhoodId.HasValue)
            .WithMessage("Mahalle geçersiz.");

        RuleFor(x => x.PrimaryIntent)
            .Must(i => i is ListingIntent.Satilik or ListingIntent.Kiralik)
            .WithMessage("İlan tipi geçersiz.");

        RuleFor(x => x.Intents)
            .Must(intents => intents.Length == 1
                             && intents[0] is ListingIntent.Satilik or ListingIntent.Kiralik)
            .WithMessage("Tek bir ilan tipi seç (Satılık veya Kiralık).")
            .Must((cmd, intents) => intents.Length == 1 && intents[0] == cmd.PrimaryIntent)
            .WithMessage("İlan tipi tutarsız.");

        RuleFor(x => x.Condition).Must(EquipmentCondition.Known.Contains)
            .WithMessage("Durum geçersiz.");

        RuleFor(x => x.ModelYear).InclusiveBetween(1950, 2100).WithMessage("Model yılı geçersiz.");
        RuleFor(x => x.Hours).GreaterThanOrEqualTo(0).When(x => x.Hours.HasValue)
            .WithMessage("Çalışma saati geçersiz.");
        RuleFor(x => x.Tons)
            .GreaterThan(0)
            .When(x => !(string.Equals(x.CapacityMetric, "capacity_kg", StringComparison.Ordinal)
                         && x.CapacityKg is > 0))
            .WithMessage("Tonaj / kapasite 0'dan büyük olmalı.");
        RuleFor(x => x.Horsepower).GreaterThanOrEqualTo(0).When(x => x.Horsepower.HasValue)
            .WithMessage("Beygir gücü geçersiz.");
        RuleFor(x => x.CapacityKg).GreaterThan(0).When(x => x.CapacityKg.HasValue)
            .WithMessage("Kapasite (kg) geçersiz.");
        RuleFor(x => x.CapacityKg)
            .NotNull()
            .GreaterThan(0)
            .When(x => string.Equals(x.CapacityMetric, "capacity_kg", StringComparison.Ordinal))
            .WithMessage("Bu kategoride kapasite (kg) zorunlu.");

        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Fiyat 0'dan büyük olmalı.");
        RuleFor(x => x.Currency)
            .Must(c => !string.IsNullOrWhiteSpace(c)
                       && Currency.Known.Contains(c.Trim().ToUpperInvariant()))
            .WithMessage("Para birimi geçersiz.");
        RuleFor(x => x.RentPrice)
            .Null()
            .WithMessage("Kira bedeli artık kullanılmıyor; tek fiyat alanını kullan.");
        RuleFor(x => x.PriceUnit)
            .Must(u => u is null || PriceUnit.Known.Contains(u))
            .WithMessage("Fiyat birimi geçersiz.");
        RuleFor(x => x.PriceUnit)
            .NotEmpty()
            .Must(PriceUnit.Known.Contains!)
            .When(x => x.PrimaryIntent == ListingIntent.Kiralik)
            .WithMessage("Kiralık ilanlarda fiyat birimi zorunlu.");
        RuleFor(x => x.PriceUnit)
            .Empty()
            .When(x => x.PrimaryIntent == ListingIntent.Satilik)
            .WithMessage("Satılık ilanlarda fiyat birimi olmamalı.");
        RuleFor(x => x.IncludesOperator)
            .Equal(false)
            .When(x => x.PrimaryIntent == ListingIntent.Satilik)
            .WithMessage("Satılık ilanlarda operatör seçeneği olmamalı.");

        RuleFor(x => x.SpecsJson)
            .Must(BeJsonObject)
            .WithMessage("Özellik verisi geçersiz.")
            .Must(json => System.Text.Encoding.UTF8.GetByteCount(json ?? "") <= SpecsJsonBuilder.MaxJsonBytes)
            .WithMessage("Özellik verisi çok büyük.");

        RuleFor(x => x.ImageUrls)
            .NotEmpty()
            .Must(urls => urls.Count is >= 1 and <= ListingImageUrl.MaxCount)
            .WithMessage($"1–{ListingImageUrl.MaxCount} görsel gerekli.");

        RuleForEach(x => x.ImageUrls)
            .Must(imageUrlPolicy.IsAllowed)
            .WithMessage("Görseller yalnızca yüklenen medya dosyaları olabilir.");

        RuleForEach(x => x.AttachmentIds).GreaterThan(0).WithMessage("Ekipman seçimi geçersiz.");
        RuleFor(x => x.AttachmentIds).Must(ids => ids.Count <= 20)
            .WithMessage("En fazla 20 ekipman seçilebilir.");
    }

    private static bool BeJsonObject(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
