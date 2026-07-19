using System.Data;
using AracParki.Application.Abstractions;
using AracParki.Application.Listings;
using AracParki.Application.Listings.Commands;
using AracParki.Domain.Listings;
using Dapper;
using Npgsql;

namespace AracParki.Infrastructure.Listings;

public sealed class ListingWriteRepository(IDbConnectionFactory connectionFactory) : IListingStore
{
    public async Task<string> CreatePublishedAsync(
        CreatePublishedListingCommand command,
        CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var sellerId = await EnsureSellerAsync(connection, tx, command, cancellationToken);
            var adNo = await NextAdNoAsync(connection, tx, cancellationToken);
            var cover = command.ImageUrls[0];

            var listingId = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    """
                    INSERT INTO listings (
                        ad_no, title, description,
                        category_id, brand_id, model_id, model_name, serial_no,
                        city_id, district_id, seller_id,
                        primary_intent, intents, condition,
                        model_year, hours, tons, capacity_kg, horsepower,
                        price, price_unit, includes_operator, specs,
                        cover_image_url, status, listed_at
                    )
                    VALUES (
                        @AdNo, @Title, @Description,
                        @CategoryId, @BrandId, @ModelId, @ModelName, @SerialNo,
                        @CityId, @DistrictId, @SellerId,
                        @PrimaryIntent, @Intents, @Condition,
                        @ModelYear, @Hours, @Tons, @CapacityKg, @Horsepower,
                        @Price, @PriceUnit, @IncludesOperator, CAST(@SpecsJson AS jsonb),
                        @CoverImageUrl, @Status, NOW()
                    )
                    RETURNING id
                    """,
                    new
                    {
                        AdNo = adNo,
                        command.Title,
                        command.Description,
                        command.CategoryId,
                        command.BrandId,
                        command.ModelId,
                        command.ModelName,
                        command.SerialNo,
                        command.CityId,
                        command.DistrictId,
                        SellerId = sellerId,
                        command.PrimaryIntent,
                        Intents = command.Intents,
                        command.Condition,
                        command.ModelYear,
                        command.Hours,
                        command.Tons,
                        command.CapacityKg,
                        command.Horsepower,
                        command.Price,
                        command.PriceUnit,
                        command.IncludesOperator,
                        command.SpecsJson,
                        CoverImageUrl = cover,
                        Status = ListingStatus.Published
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            var imageRows = command.ImageUrls.Select((url, index) => new
            {
                ListingId = listingId,
                Url = url,
                SortOrder = index
            });

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO listing_images (listing_id, url, sort_order)
                    VALUES (@ListingId, @Url, @SortOrder)
                    """,
                    imageRows,
                    transaction: tx,
                    cancellationToken: cancellationToken));

            if (command.AttachmentIds.Count > 0)
            {
                var attachmentRows = command.AttachmentIds.Select(id => new
                {
                    ListingId = listingId,
                    AttachmentId = id
                });

                await connection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO listing_attachments (listing_id, attachment_id)
                        SELECT @ListingId, a.id
                        FROM attachments a
                        WHERE a.id = @AttachmentId
                        ON CONFLICT DO NOTHING
                        """,
                        attachmentRows,
                        transaction: tx,
                        cancellationToken: cancellationToken));
            }

            await tx.CommitAsync(cancellationToken);
            return adNo;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<long> EnsureSellerAsync(
        IDbConnection connection,
        IDbTransaction tx,
        CreatePublishedListingCommand command,
        CancellationToken cancellationToken)
    {
        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                """
                INSERT INTO sellers (display_name, seller_type, phone, account_id)
                VALUES (@DisplayName, @SellerType, @Phone, @AccountId)
                ON CONFLICT (account_id) DO UPDATE
                SET phone = EXCLUDED.phone,
                    display_name = CASE
                        WHEN NULLIF(BTRIM(sellers.display_name), '') IS NULL THEN EXCLUDED.display_name
                        ELSE sellers.display_name
                    END
                RETURNING id
                """,
                new
                {
                    DisplayName = command.SellerDisplayName,
                    SellerType = SellerType.Owner,
                    command.Phone,
                    command.AccountId
                },
                transaction: tx,
                cancellationToken: cancellationToken));
    }

    private static async Task<string> NextAdNoAsync(
        IDbConnection connection,
        IDbTransaction tx,
        CancellationToken cancellationToken)
    {
        var next = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                """
                SELECT COALESCE(
                    MAX(CAST(substring(ad_no FROM 4) AS BIGINT)),
                    100000
                ) + 1
                FROM listings
                WHERE ad_no ~ '^AP-[0-9]+$'
                """,
                transaction: tx,
                cancellationToken: cancellationToken));

        return $"AP-{next}";
    }
}
