using AracParki.Application.Abstractions;
using AracParki.Application.Listings;
using Dapper;
using Npgsql;

namespace AracParki.Infrastructure.Listings;

public sealed class ListingImageCommandStore(IDbConnectionFactory connectionFactory) : IListingImageCommandStore
{
    public async Task<IReadOnlyList<ListingImageRecord>> ListByAdNoForAccountAsync(
        string adNo,
        long accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ListingImageRecord>(
            new CommandDefinition(
                """
                SELECT
                    li.id AS Id,
                    li.url AS Url,
                    li.image_id AS ImageId,
                    li.storage_key AS StorageKey,
                    li.sort_order AS SortOrder,
                    li.is_cover AS IsCover,
                    li.width AS Width,
                    li.height AS Height,
                    li.mime_type AS MimeType,
                    li.version AS Version
                FROM listing_images li
                INNER JOIN listings l ON l.id = li.listing_id
                INNER JOIN sellers s ON s.id = l.seller_id
                WHERE l.ad_no = @AdNo
                  AND s.account_id = @AccountId
                  AND li.deleted_at IS NULL
                  AND li.status = 'ready'
                ORDER BY li.sort_order, li.id
                """,
                new { AdNo = adNo, AccountId = accountId },
                cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<bool> SoftDeleteAsync(
        string adNo,
        long accountId,
        long imageId,
        TimeSpan gracePeriod,
        CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);

        var listingId = await ResolveOwnedListingIdAsync(connection, tx, adNo, accountId, cancellationToken);
        if (listingId is null)
        {
            return false;
        }

        var remaining = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)::int
                FROM listing_images
                WHERE listing_id = @ListingId AND deleted_at IS NULL AND status = 'ready'
                """,
                new { ListingId = listingId },
                transaction: tx,
                cancellationToken: cancellationToken));

        if (remaining <= 1)
        {
            await tx.RollbackAsync(cancellationToken);
            return false;
        }

        var wasCover = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT is_cover
                FROM listing_images
                WHERE id = @ImageId AND listing_id = @ListingId AND deleted_at IS NULL
                """,
                new { ImageId = imageId, ListingId = listingId },
                transaction: tx,
                cancellationToken: cancellationToken));

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE listing_images
                SET status = 'soft_deleted',
                    is_cover = FALSE,
                    deleted_at = NOW(),
                    purge_after = NOW() + @Grace,
                    updated_at = NOW()
                WHERE id = @ImageId
                  AND listing_id = @ListingId
                  AND deleted_at IS NULL
                """,
                new { ImageId = imageId, ListingId = listingId, Grace = gracePeriod },
                transaction: tx,
                cancellationToken: cancellationToken));

        if (affected == 0)
        {
            await tx.RollbackAsync(cancellationToken);
            return false;
        }

        if (wasCover)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    WITH next_cover AS (
                        SELECT id, url
                        FROM listing_images
                        WHERE listing_id = @ListingId AND deleted_at IS NULL AND status = 'ready'
                        ORDER BY sort_order, id
                        LIMIT 1
                    )
                    UPDATE listing_images li
                    SET is_cover = TRUE, updated_at = NOW()
                    FROM next_cover
                    WHERE li.id = next_cover.id;

                    UPDATE listings l
                    SET cover_image_url = li.url, updated_at = NOW()
                    FROM listing_images li
                    WHERE l.id = @ListingId
                      AND li.listing_id = l.id
                      AND li.is_cover = TRUE
                      AND li.deleted_at IS NULL;
                    """,
                    new { ListingId = listingId },
                    transaction: tx,
                    cancellationToken: cancellationToken));
        }

        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetCoverAsync(
        string adNo,
        long accountId,
        long imageId,
        CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);

        var listingId = await ResolveOwnedListingIdAsync(connection, tx, adNo, accountId, cancellationToken);
        if (listingId is null)
        {
            return false;
        }

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT EXISTS(
                    SELECT 1 FROM listing_images
                    WHERE id = @ImageId AND listing_id = @ListingId
                      AND deleted_at IS NULL AND status = 'ready'
                )
                """,
                new { ImageId = imageId, ListingId = listingId },
                transaction: tx,
                cancellationToken: cancellationToken));

        if (!exists)
        {
            await tx.RollbackAsync(cancellationToken);
            return false;
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE listing_images
                SET is_cover = FALSE, updated_at = NOW()
                WHERE listing_id = @ListingId AND deleted_at IS NULL AND is_cover = TRUE;

                UPDATE listing_images
                SET is_cover = TRUE, sort_order = 0, updated_at = NOW()
                WHERE id = @ImageId AND listing_id = @ListingId;

                UPDATE listings
                SET cover_image_url = (SELECT url FROM listing_images WHERE id = @ImageId),
                    updated_at = NOW()
                WHERE id = @ListingId;
                """,
                new { ImageId = imageId, ListingId = listingId },
                transaction: tx,
                cancellationToken: cancellationToken));

        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReorderAsync(
        string adNo,
        long accountId,
        IReadOnlyList<long> imageIdsInOrder,
        CancellationToken cancellationToken)
    {
        if (imageIdsInOrder.Count == 0)
        {
            return false;
        }

        await using var connection = (NpgsqlConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);

        var listingId = await ResolveOwnedListingIdAsync(connection, tx, adNo, accountId, cancellationToken);
        if (listingId is null)
        {
            return false;
        }

        var existing = (await connection.QueryAsync<long>(
            new CommandDefinition(
                """
                SELECT id FROM listing_images
                WHERE listing_id = @ListingId AND deleted_at IS NULL AND status = 'ready'
                ORDER BY sort_order, id
                """,
                new { ListingId = listingId },
                transaction: tx,
                cancellationToken: cancellationToken))).ToArray();

        if (existing.Length != imageIdsInOrder.Count
            || existing.ToHashSet().SetEquals(imageIdsInOrder) == false)
        {
            await tx.RollbackAsync(cancellationToken);
            return false;
        }

        for (var i = 0; i < imageIdsInOrder.Count; i++)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE listing_images
                    SET sort_order = @SortOrder,
                        is_cover = FALSE,
                        updated_at = NOW()
                    WHERE id = @Id AND listing_id = @ListingId
                    """,
                    new
                    {
                        Id = imageIdsInOrder[i],
                        ListingId = listingId,
                        SortOrder = i
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE listing_images
                SET is_cover = TRUE, updated_at = NOW()
                WHERE id = @CoverId AND listing_id = @ListingId;

                UPDATE listings
                SET cover_image_url = (
                        SELECT url FROM listing_images WHERE id = @CoverId
                    ),
                    updated_at = NOW()
                WHERE id = @ListingId
                """,
                new { CoverId = imageIdsInOrder[0], ListingId = listingId },
                transaction: tx,
                cancellationToken: cancellationToken));

        await tx.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task<long?> ResolveOwnedListingIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction tx,
        string adNo,
        long accountId,
        CancellationToken cancellationToken)
    {
        return await connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(
                """
                SELECT l.id
                FROM listings l
                INNER JOIN sellers s ON s.id = l.seller_id
                WHERE l.ad_no = @AdNo
                  AND s.account_id = @AccountId
                  AND l.status IN ('pending_review', 'rejected')
                """,
                new { AdNo = adNo, AccountId = accountId },
                transaction: tx,
                cancellationToken: cancellationToken));
    }
}
