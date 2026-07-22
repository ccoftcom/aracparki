using AracParki.Application.Listings;
using AracParki.Application.Listings.Commands;
using AracParki.Application.Listings.Validation;
using AracParki.Application.Media;
using AracParki.Domain.Listings;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;

namespace AracParki.UnitTests;

public sealed class CreatePublishedListingValidatorTests
{
    private readonly CreatePublishedListingValidator _validator = new(
        new ListingImageUrlPolicy(Options.Create(new CloudflareMediaSettings())));

    private static CreatePublishedListingCommand ValidCommand() => new()
    {
        AccountId = 1,
        SellerDisplayName = "Test User",
        Phone = "905551112233",
        SellerType = SellerType.Owner,
        CategoryId = 1,
        BrandId = 1,
        ModelName = "320D",
        CityId = 34,
        DistrictId = 1,
        PrimaryIntent = ListingIntent.Satilik,
        Intents = [ListingIntent.Satilik],
        Condition = EquipmentCondition.Used,
        ModelYear = 2018,
        Hours = 4500,
        Tons = 22.5m,
        Horsepower = 150,
        Price = 1_250_000m,
        Currency = Currency.Try,
        Title = "CAT 320D",
        Description = "Bakımlı makine.",
        SpecsJson = "{}",
        ImageUrls = ["/uploads/listings/1/abc.jpg"]
    };

    [Fact]
    public void Valid_command_passes()
    {
        var result = _validator.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Accepts_usd_and_eur()
    {
        var baseCmd = ValidCommand();
        var usd = Clone(baseCmd, Currency.Usd);
        _validator.TestValidate(usd).ShouldNotHaveAnyValidationErrors();

        var eur = Clone(baseCmd, Currency.Eur);
        _validator.TestValidate(eur).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Rejects_unknown_currency()
    {
        var result = _validator.TestValidate(Clone(ValidCommand(), "GBP"));
        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    private static CreatePublishedListingCommand Clone(CreatePublishedListingCommand source, string currency) => new()
    {
        AccountId = source.AccountId,
        SellerDisplayName = source.SellerDisplayName,
        Phone = source.Phone,
        SellerType = source.SellerType,
        CategoryId = source.CategoryId,
        BrandId = source.BrandId,
        ModelName = source.ModelName,
        CityId = source.CityId,
        DistrictId = source.DistrictId,
        PrimaryIntent = source.PrimaryIntent,
        Intents = source.Intents,
        Condition = source.Condition,
        ModelYear = source.ModelYear,
        Hours = source.Hours,
        Tons = source.Tons,
        Horsepower = source.Horsepower,
        Price = source.Price,
        Currency = currency,
        PriceUnit = source.PriceUnit,
        Title = source.Title,
        Description = source.Description,
        SpecsJson = source.SpecsJson,
        ImageUrls = source.ImageUrls
    };

    [Fact]
    public void Rejects_dual_intent()
    {
        var baseCmd = ValidCommand();
        var cmd = new CreatePublishedListingCommand
        {
            AccountId = baseCmd.AccountId,
            SellerDisplayName = baseCmd.SellerDisplayName,
            Phone = baseCmd.Phone,
            SellerType = baseCmd.SellerType,
            CategoryId = baseCmd.CategoryId,
            BrandId = baseCmd.BrandId,
            ModelName = baseCmd.ModelName,
            CityId = baseCmd.CityId,
            DistrictId = baseCmd.DistrictId,
            PrimaryIntent = ListingIntent.Satilik,
            Intents = [ListingIntent.Satilik, ListingIntent.Kiralik],
            Condition = baseCmd.Condition,
            ModelYear = baseCmd.ModelYear,
            Hours = baseCmd.Hours,
            Tons = baseCmd.Tons,
            Horsepower = baseCmd.Horsepower,
            Price = baseCmd.Price,
            PriceUnit = PriceUnit.Day,
            Title = baseCmd.Title,
            Description = baseCmd.Description,
            SpecsJson = baseCmd.SpecsJson,
            ImageUrls = baseCmd.ImageUrls
        };

        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Intents);
    }

    [Fact]
    public void Rent_requires_price_unit()
    {
        var baseCmd = ValidCommand();
        var cmd = new CreatePublishedListingCommand
        {
            AccountId = baseCmd.AccountId,
            SellerDisplayName = baseCmd.SellerDisplayName,
            Phone = baseCmd.Phone,
            SellerType = baseCmd.SellerType,
            CategoryId = baseCmd.CategoryId,
            BrandId = baseCmd.BrandId,
            ModelName = baseCmd.ModelName,
            CityId = baseCmd.CityId,
            DistrictId = baseCmd.DistrictId,
            PrimaryIntent = ListingIntent.Kiralik,
            Intents = [ListingIntent.Kiralik],
            Condition = baseCmd.Condition,
            ModelYear = baseCmd.ModelYear,
            Hours = baseCmd.Hours,
            Tons = baseCmd.Tons,
            Horsepower = baseCmd.Horsepower,
            Price = baseCmd.Price,
            PriceUnit = null,
            Title = baseCmd.Title,
            Description = baseCmd.Description,
            SpecsJson = baseCmd.SpecsJson,
            ImageUrls = baseCmd.ImageUrls
        };

        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.PriceUnit);
    }

    [Fact]
    public void Rejects_http_image_url()
    {
        var baseCmd = ValidCommand();
        var cmd = new CreatePublishedListingCommand
        {
            AccountId = baseCmd.AccountId,
            SellerDisplayName = baseCmd.SellerDisplayName,
            Phone = baseCmd.Phone,
            SellerType = baseCmd.SellerType,
            CategoryId = baseCmd.CategoryId,
            BrandId = baseCmd.BrandId,
            ModelName = baseCmd.ModelName,
            CityId = baseCmd.CityId,
            DistrictId = baseCmd.DistrictId,
            PrimaryIntent = baseCmd.PrimaryIntent,
            Intents = baseCmd.Intents,
            Condition = baseCmd.Condition,
            ModelYear = baseCmd.ModelYear,
            Hours = baseCmd.Hours,
            Tons = baseCmd.Tons,
            Horsepower = baseCmd.Horsepower,
            Price = baseCmd.Price,
            Title = baseCmd.Title,
            Description = baseCmd.Description,
            SpecsJson = baseCmd.SpecsJson,
            ImageUrls = ["http://evil.example/a.jpg"]
        };

        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor("ImageUrls[0]");
    }

    [Fact]
    public void Rejects_localhost_https_image()
    {
        Assert.False(ListingImageUrl.IsAllowed("https://127.0.0.1/x.jpg"));
        Assert.False(ListingImageUrl.IsAllowed("https://localhost/x.jpg"));
        Assert.True(ListingImageUrl.IsAllowed("/uploads/listings/1/a.jpg"));
        Assert.False(ListingImageUrl.IsAllowed("https://cdn.example.com/a.jpg"));
    }

    [Fact]
    public void Hours_null_allowed()
    {
        var baseCmd = ValidCommand();
        var cmd = new CreatePublishedListingCommand
        {
            AccountId = baseCmd.AccountId,
            SellerDisplayName = baseCmd.SellerDisplayName,
            Phone = baseCmd.Phone,
            SellerType = baseCmd.SellerType,
            CategoryId = baseCmd.CategoryId,
            BrandId = baseCmd.BrandId,
            ModelName = baseCmd.ModelName,
            CityId = baseCmd.CityId,
            DistrictId = baseCmd.DistrictId,
            PrimaryIntent = baseCmd.PrimaryIntent,
            Intents = baseCmd.Intents,
            Condition = baseCmd.Condition,
            ModelYear = baseCmd.ModelYear,
            Hours = null,
            Tons = baseCmd.Tons,
            Horsepower = null,
            Price = baseCmd.Price,
            Title = baseCmd.Title,
            Description = baseCmd.Description,
            SpecsJson = baseCmd.SpecsJson,
            ImageUrls = baseCmd.ImageUrls
        };

        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Hours);
        result.ShouldNotHaveValidationErrorFor(x => x.Horsepower);
    }

    [Fact]
    public void Capacity_kg_required_when_metric_is_capacity_kg()
    {
        var baseCmd = ValidCommand();
        var cmd = new CreatePublishedListingCommand
        {
            AccountId = baseCmd.AccountId,
            SellerDisplayName = baseCmd.SellerDisplayName,
            Phone = baseCmd.Phone,
            SellerType = baseCmd.SellerType,
            CategoryId = baseCmd.CategoryId,
            BrandId = baseCmd.BrandId,
            ModelName = baseCmd.ModelName,
            CityId = baseCmd.CityId,
            DistrictId = baseCmd.DistrictId,
            PrimaryIntent = baseCmd.PrimaryIntent,
            Intents = baseCmd.Intents,
            Condition = baseCmd.Condition,
            ModelYear = baseCmd.ModelYear,
            Hours = baseCmd.Hours,
            Tons = baseCmd.Tons,
            CapacityKg = null,
            CapacityMetric = "capacity_kg",
            Horsepower = baseCmd.Horsepower,
            Price = baseCmd.Price,
            Title = baseCmd.Title,
            Description = baseCmd.Description,
            SpecsJson = baseCmd.SpecsJson,
            ImageUrls = baseCmd.ImageUrls
        };

        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.CapacityKg);
    }

    [Fact]
    public void Capacity_kg_metric_allows_zero_tons_when_capacity_kg_present()
    {
        var baseCmd = ValidCommand();
        var cmd = new CreatePublishedListingCommand
        {
            AccountId = baseCmd.AccountId,
            SellerDisplayName = baseCmd.SellerDisplayName,
            Phone = baseCmd.Phone,
            SellerType = baseCmd.SellerType,
            CategoryId = baseCmd.CategoryId,
            BrandId = baseCmd.BrandId,
            ModelName = baseCmd.ModelName,
            CityId = baseCmd.CityId,
            DistrictId = baseCmd.DistrictId,
            PrimaryIntent = baseCmd.PrimaryIntent,
            Intents = baseCmd.Intents,
            Condition = baseCmd.Condition,
            ModelYear = baseCmd.ModelYear,
            Hours = baseCmd.Hours,
            Tons = 0,
            CapacityKg = 230,
            CapacityMetric = "capacity_kg",
            Horsepower = baseCmd.Horsepower,
            Price = baseCmd.Price,
            Title = baseCmd.Title,
            Description = baseCmd.Description,
            SpecsJson = baseCmd.SpecsJson,
            ImageUrls = baseCmd.ImageUrls
        };

        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Tons);
        result.ShouldNotHaveValidationErrorFor(x => x.CapacityKg);
    }

    [Fact]
    public void Sale_rejects_includes_operator()
    {
        var baseCmd = ValidCommand();
        var cmd = new CreatePublishedListingCommand
        {
            AccountId = baseCmd.AccountId,
            SellerDisplayName = baseCmd.SellerDisplayName,
            Phone = baseCmd.Phone,
            SellerType = baseCmd.SellerType,
            CategoryId = baseCmd.CategoryId,
            BrandId = baseCmd.BrandId,
            ModelName = baseCmd.ModelName,
            CityId = baseCmd.CityId,
            DistrictId = baseCmd.DistrictId,
            PrimaryIntent = ListingIntent.Satilik,
            Intents = [ListingIntent.Satilik],
            Condition = baseCmd.Condition,
            ModelYear = baseCmd.ModelYear,
            Hours = baseCmd.Hours,
            Tons = baseCmd.Tons,
            Horsepower = baseCmd.Horsepower,
            Price = baseCmd.Price,
            IncludesOperator = true,
            Title = baseCmd.Title,
            Description = baseCmd.Description,
            SpecsJson = baseCmd.SpecsJson,
            ImageUrls = baseCmd.ImageUrls
        };

        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.IncludesOperator);
    }
}
