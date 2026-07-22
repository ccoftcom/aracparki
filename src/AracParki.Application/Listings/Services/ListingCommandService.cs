using AracParki.Application.Catalog.Services;
using AracParki.Application.Listings;
using AracParki.Application.Listings.Commands;
using AracParki.Domain.Listings;
using FluentValidation;
using FluentValidation.Results;

namespace AracParki.Application.Listings.Services;

public sealed class ListingCommandService(
    IListingStore store,
    IValidator<CreatePublishedListingCommand> validator,
    CatalogService catalog)
{
    public async Task<string> CreatePublishedAsync(
        CreatePublishedListingCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var attributes = await catalog.GetCategoryAttributesAsync(command.CategoryId, cancellationToken);
        var (specsOk, specsError, normalizedSpecs) = SpecsJsonBuilder.TryValidateJson(command.SpecsJson, attributes);
        if (!specsOk)
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(CreatePublishedListingCommand.SpecsJson), specsError ?? "Özellikler geçersiz.")
            ]);
        }

        var normalized = Normalize(command, normalizedSpecs);
        return await store.CreatePublishedAsync(normalized, cancellationToken);
    }

    public async Task UpdateForReviewAsync(
        string adNo,
        CreatePublishedListingCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adNo);
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var attributes = await catalog.GetCategoryAttributesAsync(command.CategoryId, cancellationToken);
        var (specsOk, specsError, normalizedSpecs) = SpecsJsonBuilder.TryValidateJson(command.SpecsJson, attributes);
        if (!specsOk)
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(CreatePublishedListingCommand.SpecsJson), specsError ?? "Özellikler geçersiz.")
            ]);
        }

        var normalized = Normalize(command, normalizedSpecs);
        await store.UpdateForReviewAsync(adNo.Trim(), command.AccountId, normalized, cancellationToken);
    }

    private static CreatePublishedListingCommand Normalize(
        CreatePublishedListingCommand command,
        string specsJson)
    {
        var primary = command.PrimaryIntent.Trim();
        var intents = new[] { primary };

        var assets = (command.ImageAssets.Count > 0
                ? command.ImageAssets
                : command.ImageUrls.Select(ListingImageAsset.FromUrl))
            .Where(a => !string.IsNullOrWhiteSpace(a.DeliveryUrl))
            .GroupBy(a => a.DeliveryUrl.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(ListingImageUrl.MaxCount)
            .ToArray();

        var images = assets.Select(a => a.DeliveryUrl.Trim()).ToArray();

        var attachmentIds = command.AttachmentIds
            .Where(id => id > 0)
            .Distinct()
            .Take(20)
            .ToArray();

        var isRent = primary == ListingIntent.Kiralik;
        var priceUnit = isRent && !string.IsNullOrWhiteSpace(command.PriceUnit)
            ? command.PriceUnit.Trim()
            : null;

        return new CreatePublishedListingCommand
        {
            AccountId = command.AccountId,
            SellerDisplayName = command.SellerDisplayName.Trim(),
            Phone = command.Phone.Trim(),
            SellerType = command.SellerType.Trim(),
            CorporateAccountId = command.CorporateAccountId is > 0 ? command.CorporateAccountId : null,
            CategoryId = command.CategoryId,
            BrandId = command.BrandId,
            ModelId = command.ModelId is > 0 ? command.ModelId : null,
            ModelName = command.ModelName.Trim(),
            SerialNo = string.IsNullOrWhiteSpace(command.SerialNo) ? null : command.SerialNo.Trim(),
            CityId = command.CityId,
            DistrictId = command.DistrictId,
            NeighborhoodId = command.NeighborhoodId is > 0 ? command.NeighborhoodId : null,
            PrimaryIntent = primary,
            Intents = intents,
            Condition = command.Condition.Trim(),
            ModelYear = command.ModelYear,
            Hours = command.Hours is >= 0 ? command.Hours : null,
            Tons = command.Tons > 0
                ? command.Tons
                : command.CapacityKg is > 0
                    ? Math.Max(0.01m, Math.Round(command.CapacityKg.Value / 1000m, 2, MidpointRounding.AwayFromZero))
                    : command.Tons,
            CapacityKg = command.CapacityKg is > 0 ? command.CapacityKg : null,
            Horsepower = command.Horsepower is >= 0 ? command.Horsepower : null,
            CapacityMetric = string.IsNullOrWhiteSpace(command.CapacityMetric)
                ? null
                : command.CapacityMetric.Trim(),
            Price = command.Price,
            RentPrice = null,
            Currency = Currency.Normalize(command.Currency),
            PriceUnit = priceUnit,
            IncludesOperator = command.IncludesOperator && isRent,
            Title = command.Title.Trim(),
            Description = ListingDescriptionHtml.Sanitize(command.Description),
            SpecsJson = string.IsNullOrWhiteSpace(specsJson) ? "{}" : specsJson.Trim(),
            ImageUrls = images,
            ImageAssets = assets,
            AttachmentIds = attachmentIds
        };
    }
}
