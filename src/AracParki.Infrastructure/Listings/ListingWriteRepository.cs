using System.Data;
using AracParki.Application.Abstractions;
using AracParki.Application.Listings;
using AracParki.Application.Listings.Commands;
using AracParki.Application.Listings.Dtos;
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
                        city_id, district_id, neighborhood_id, seller_id,
                        primary_intent, intents, condition,
                        model_year, hours, tons, capacity_kg, horsepower,
                        price, rent_price, currency, price_unit, includes_operator, specs,
                        cover_image_url, status, listed_at, submitted_at
                    )
                    VALUES (
                        @AdNo, @Title, @Description,
                        @CategoryId, @BrandId, @ModelId, @ModelName, @SerialNo,
                        @CityId, @DistrictId, @NeighborhoodId, @SellerId,
                        @PrimaryIntent, @Intents, @Condition,
                        @ModelYear, @Hours, @Tons, @CapacityKg, @Horsepower,
                        @Price, @RentPrice, @Currency, @PriceUnit, @IncludesOperator, CAST(@SpecsJson AS jsonb),
                        @CoverImageUrl, @Status, NOW(), NOW()
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
                        command.NeighborhoodId,
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
                        command.RentPrice,
                        command.Currency,
                        command.PriceUnit,
                        command.IncludesOperator,
                        command.SpecsJson,
                        CoverImageUrl = cover,
                        Status = ListingStatus.PendingReview
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            var assets = command.ImageAssets.Count > 0
                ? command.ImageAssets
                : command.ImageUrls.Select(ListingImageAsset.FromUrl).ToArray();

            var imageRows = assets.Select((asset, index) => new
            {
                ListingId = listingId,
                Url = asset.DeliveryUrl.Trim(),
                SortOrder = index,
                ImageId = asset.ImageId,
                StorageKey = asset.StorageKey,
                OriginalFilename = asset.OriginalFilename,
                Version = asset.Version <= 0 ? 1 : asset.Version,
                Width = asset.Width is > 0 ? asset.Width : null,
                Height = asset.Height is > 0 ? asset.Height : null,
                ByteSize = asset.ByteSize is > 0 ? asset.ByteSize : null,
                MimeType = asset.MimeType,
                ChecksumSha256 = asset.ChecksumSha256,
                IsCover = index == 0
            });

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO listing_images (
                        listing_id, url, sort_order,
                        image_id, storage_key, original_filename, version,
                        width, height, byte_size, mime_type, checksum_sha256,
                        is_cover, status
                    )
                    VALUES (
                        @ListingId, @Url, @SortOrder,
                        @ImageId, @StorageKey, @OriginalFilename, @Version,
                        @Width, @Height, @ByteSize, @MimeType, @ChecksumSha256,
                        @IsCover, 'ready'
                    )
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

    public async Task UpdateForReviewAsync(
        string adNo,
        long accountId,
        CreatePublishedListingCommand command,
        CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var sellerId = await EnsureSellerAsync(connection, tx, command, cancellationToken);
            var cover = command.ImageUrls[0];

            var listingId = await connection.ExecuteScalarAsync<long?>(
                new CommandDefinition(
                    """
                    UPDATE listings l
                    SET title = @Title,
                        description = @Description,
                        category_id = @CategoryId,
                        brand_id = @BrandId,
                        model_id = @ModelId,
                        model_name = @ModelName,
                        serial_no = @SerialNo,
                        city_id = @CityId,
                        district_id = @DistrictId,
                        neighborhood_id = @NeighborhoodId,
                        seller_id = @SellerId,
                        primary_intent = @PrimaryIntent,
                        intents = @Intents,
                        condition = @Condition,
                        model_year = @ModelYear,
                        hours = @Hours,
                        tons = @Tons,
                        capacity_kg = @CapacityKg,
                        horsepower = @Horsepower,
                        price = @Price,
                        rent_price = @RentPrice,
                        currency = @Currency,
                        price_unit = @PriceUnit,
                        includes_operator = @IncludesOperator,
                        specs = CAST(@SpecsJson AS jsonb),
                        cover_image_url = @CoverImageUrl,
                        status = @Status,
                        rejection_reason = NULL,
                        reviewed_at = NULL,
                        reviewed_by_account_id = NULL,
                        submitted_at = NOW()
                    FROM sellers s
                    WHERE l.ad_no = @AdNo
                      AND l.seller_id = s.id
                      AND s.account_id = @AccountId
                      AND l.status IN ('pending_review', 'rejected', 'published')
                    RETURNING l.id
                    """,
                    new
                    {
                        AdNo = adNo,
                        AccountId = accountId,
                        command.Title,
                        command.Description,
                        command.CategoryId,
                        command.BrandId,
                        command.ModelId,
                        command.ModelName,
                        command.SerialNo,
                        command.CityId,
                        command.DistrictId,
                        command.NeighborhoodId,
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
                        command.RentPrice,
                        command.Currency,
                        command.PriceUnit,
                        command.IncludesOperator,
                        command.SpecsJson,
                        CoverImageUrl = cover,
                        Status = ListingStatus.PendingReview
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            if (listingId is null or <= 0)
            {
                throw new InvalidOperationException("İlan bulunamadı veya düzenlenemez.");
            }

            await connection.ExecuteAsync(
                new CommandDefinition(
                    "DELETE FROM listing_attachments WHERE listing_id = @ListingId",
                    new { ListingId = listingId.Value },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE listing_images
                    SET status = 'soft_deleted',
                        deleted_at = NOW()
                    WHERE listing_id = @ListingId
                      AND deleted_at IS NULL
                    """,
                    new { ListingId = listingId.Value },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            var assets = command.ImageAssets.Count > 0
                ? command.ImageAssets
                : command.ImageUrls.Select(ListingImageAsset.FromUrl).ToArray();

            var imageRows = assets.Select((asset, index) => new
            {
                ListingId = listingId.Value,
                Url = asset.DeliveryUrl.Trim(),
                SortOrder = index,
                ImageId = asset.ImageId,
                StorageKey = asset.StorageKey,
                OriginalFilename = asset.OriginalFilename,
                Version = asset.Version <= 0 ? 1 : asset.Version,
                Width = asset.Width is > 0 ? asset.Width : null,
                Height = asset.Height is > 0 ? asset.Height : null,
                ByteSize = asset.ByteSize is > 0 ? asset.ByteSize : null,
                MimeType = asset.MimeType,
                ChecksumSha256 = asset.ChecksumSha256,
                IsCover = index == 0
            });

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO listing_images (
                        listing_id, url, sort_order,
                        image_id, storage_key, original_filename, version,
                        width, height, byte_size, mime_type, checksum_sha256,
                        is_cover, status
                    )
                    VALUES (
                        @ListingId, @Url, @SortOrder,
                        @ImageId, @StorageKey, @OriginalFilename, @Version,
                        @Width, @Height, @ByteSize, @MimeType, @ChecksumSha256,
                        @IsCover, 'ready'
                    )
                    """,
                    imageRows,
                    transaction: tx,
                    cancellationToken: cancellationToken));

            if (command.AttachmentIds.Count > 0)
            {
                var attachmentRows = command.AttachmentIds.Select(id => new
                {
                    ListingId = listingId.Value,
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
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task ApproveAsync(string adNo, long adminAccountId, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE listings
                SET status = @Status,
                    rejection_reason = NULL,
                    reviewed_at = NOW(),
                    reviewed_by_account_id = @AdminId,
                    listed_at = NOW()
                WHERE ad_no = @AdNo
                  AND status = @Pending
                """,
                new
                {
                    AdNo = adNo,
                    AdminId = adminAccountId,
                    Status = ListingStatus.Published,
                    Pending = ListingStatus.PendingReview
                },
                cancellationToken: cancellationToken));

        if (rows == 0)
        {
            throw new InvalidOperationException("Onaylanacak ilan bulunamadı.");
        }
    }

    public async Task RejectAsync(string adNo, long adminAccountId, string reason, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE listings
                SET status = @Status,
                    rejection_reason = @Reason,
                    reviewed_at = NOW(),
                    reviewed_by_account_id = @AdminId
                WHERE ad_no = @AdNo
                  AND status = @Pending
                """,
                new
                {
                    AdNo = adNo,
                    AdminId = adminAccountId,
                    Reason = reason,
                    Status = ListingStatus.Rejected,
                    Pending = ListingStatus.PendingReview
                },
                cancellationToken: cancellationToken));

        if (rows == 0)
        {
            throw new InvalidOperationException("Reddedilecek ilan bulunamadı.");
        }
    }

    public async Task<ModerationCountsDto> GetModerationCountsAsync(CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleAsync<(int Pending, int Published, int Rejected)>(
            new CommandDefinition(
                """
                SELECT
                    COUNT(*) FILTER (WHERE status = 'pending_review')::int AS Pending,
                    COUNT(*) FILTER (WHERE status = 'published')::int AS Published,
                    COUNT(*) FILTER (WHERE status = 'rejected')::int AS Rejected
                FROM listings
                """,
                cancellationToken: cancellationToken));

        return new ModerationCountsDto
        {
            PendingReview = row.Pending,
            Published = row.Published,
            Rejected = row.Rejected
        };
    }

    public async Task<IReadOnlyList<ModerationListItemDto>> ListForModerationAsync(
        string status,
        int take,
        CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ModerationListItemDto>(
            new CommandDefinition(
                """
                SELECT
                    l.id,
                    l.ad_no AS AdNo,
                    l.title,
                    l.status,
                    l.cover_image_url AS CoverImageUrl,
                    s.display_name AS SellerName,
                    city.name AS City,
                    l.submitted_at AS SubmittedAt,
                    l.listed_at AS ListedAt,
                    l.rejection_reason AS RejectionReason
                FROM listings l
                JOIN sellers s ON s.id = l.seller_id
                JOIN cities city ON city.id = l.city_id
                WHERE l.status = @Status
                ORDER BY COALESCE(l.submitted_at, l.listed_at) DESC, l.id DESC
                LIMIT @Take
                """,
                new { Status = status, Take = take },
                cancellationToken: cancellationToken));

        return rows.ToList();
    }

    private static async Task<long> EnsureSellerAsync(
        IDbConnection connection,
        IDbTransaction tx,
        CreatePublishedListingCommand command,
        CancellationToken cancellationToken)
    {
        var sellerType = SellerType.Known.Contains(command.SellerType)
            ? command.SellerType
            : SellerType.Owner;

        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                """
                INSERT INTO sellers (display_name, seller_type, phone, account_id)
                VALUES (@DisplayName, @SellerType, @Phone, @AccountId)
                ON CONFLICT (account_id) DO UPDATE
                SET phone = EXCLUDED.phone,
                    seller_type = EXCLUDED.seller_type,
                    display_name = CASE
                        WHEN NULLIF(BTRIM(sellers.display_name), '') IS NULL THEN EXCLUDED.display_name
                        ELSE sellers.display_name
                    END
                RETURNING id
                """,
                new
                {
                    DisplayName = command.SellerDisplayName,
                    SellerType = sellerType,
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
                "SELECT nextval('listing_ad_no_seq')",
                transaction: tx,
                cancellationToken: cancellationToken));

        return $"AP-{next}";
    }
}
