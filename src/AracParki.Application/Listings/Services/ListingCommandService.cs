using AracParki.Application.Listings.Commands;
using FluentValidation;

namespace AracParki.Application.Listings.Services;

public sealed class ListingCommandService(
    IListingStore store,
    IValidator<CreatePublishedListingCommand> validator)
{
    public async Task<string> CreatePublishedAsync(
        CreatePublishedListingCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await store.CreatePublishedAsync(Normalize(command), cancellationToken);
    }

    private static CreatePublishedListingCommand Normalize(CreatePublishedListingCommand command)
    {
        var intents = command.Intents
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var images = command.ImageUrls
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();

        var attachmentIds = command.AttachmentIds
            .Where(id => id > 0)
            .Distinct()
            .Take(20)
            .ToArray();

        var priceUnit = string.IsNullOrWhiteSpace(command.PriceUnit) ? null : command.PriceUnit.Trim();
        if (!intents.Contains(Domain.Listings.ListingIntent.Kiralik, StringComparer.Ordinal))
        {
            priceUnit = null;
        }

        return new CreatePublishedListingCommand
        {
            AccountId = command.AccountId,
            SellerDisplayName = command.SellerDisplayName.Trim(),
            Phone = command.Phone.Trim(),
            CategoryId = command.CategoryId,
            BrandId = command.BrandId,
            ModelId = command.ModelId is > 0 ? command.ModelId : null,
            ModelName = command.ModelName.Trim(),
            SerialNo = string.IsNullOrWhiteSpace(command.SerialNo) ? null : command.SerialNo.Trim(),
            CityId = command.CityId,
            DistrictId = command.DistrictId,
            PrimaryIntent = command.PrimaryIntent.Trim(),
            Intents = intents,
            Condition = command.Condition.Trim(),
            ModelYear = command.ModelYear,
            Hours = command.Hours,
            Tons = command.Tons,
            CapacityKg = command.CapacityKg is > 0 ? command.CapacityKg : null,
            Horsepower = command.Horsepower,
            Price = command.Price,
            PriceUnit = priceUnit,
            IncludesOperator = command.IncludesOperator && intents.Contains(Domain.Listings.ListingIntent.Kiralik),
            Title = command.Title.Trim(),
            Description = command.Description.Trim(),
            SpecsJson = string.IsNullOrWhiteSpace(command.SpecsJson) ? "{}" : command.SpecsJson.Trim(),
            ImageUrls = images,
            AttachmentIds = attachmentIds
        };
    }
}
