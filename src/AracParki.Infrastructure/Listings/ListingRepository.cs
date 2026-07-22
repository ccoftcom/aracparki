using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AracParki.Application.Abstractions;
using AracParki.Application.Common;
using AracParki.Application.Listings;
using AracParki.Application.Listings.Dtos;
using AracParki.Application.Listings.Queries;
using AracParki.Domain.Listings;
using Dapper;
using Microsoft.Extensions.Caching.Distributed;

namespace AracParki.Infrastructure.Listings;

public sealed class ListingRepository(
    IDbConnectionFactory connectionFactory,
    ISqlQueryLoader sql,
    IDistributedCache cache) : IListingQuery
{
    private static readonly TimeSpan CountCacheTtl = TimeSpan.FromSeconds(30);
    private const int MaxOffsetRows = 5_000;

    public async Task<ListingSearchResult> SearchAsync(ListingSearchQuery query, CancellationToken cancellationToken)
    {
        var parameters = BuildFilter(query);
        var useKeyset = query is { Sort: ListingSort.Newest, CursorListedAt: not null, CursorId: not null };

        var skip = useKeyset ? 0 : Math.Min((query.Page - 1) * query.PageSize, MaxOffsetRows);
        parameters.Add("Take", query.PageSize);
        parameters.Add("Skip", skip);
        parameters.Add("CursorListedAt", query.CursorListedAt, DbType.DateTimeOffset);
        parameters.Add("CursorId", query.CursorId, DbType.Int64);

        var sqlPath = ResolveSearchSql(query.Sort);

        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var items = (await connection.QueryAsync<ListingCardDto>(
            new CommandDefinition(sql.Get(sqlPath), parameters, cancellationToken: cancellationToken))).AsList();

        var countKey = "listings:count:" + FilterCacheKey(query);
        var cachedTotal = await cache.GetJsonAsync<int?>(countKey, cancellationToken);
        int total;
        if (cachedTotal is int hit)
        {
            total = hit;
        }
        else
        {
            total = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(sql.Get("Listings/CountSearch.sql"), parameters, cancellationToken: cancellationToken));
            await cache.SetJsonAsync(countKey, total, CountCacheTtl, cancellationToken);
        }

        var last = items.Count > 0 ? items[^1] : null;

        return new ListingSearchResult
        {
            Items = items,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
            HasMore = query.Page * query.PageSize < total,
            NextCursorListedAt = last?.ListedAt,
            NextCursorId = last?.Id
        };
    }

    public async Task<IReadOnlyList<ListingCardDto>> GetFeaturedAsync(
        ListingSearchQuery query,
        int take,
        CancellationToken cancellationToken)
    {
        var parameters = BuildFilter(query);
        parameters.Add("Take", take);

        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = await connection.QueryAsync<ListingCardDto>(
            new CommandDefinition(sql.Get("Listings/Featured.sql"), parameters, cancellationToken: cancellationToken));
        return items.AsList();
    }

    public async Task<IReadOnlyList<ListingCardDto>> GetByAccountIdAsync(
        long accountId,
        int take,
        CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = await connection.QueryAsync<ListingCardDto>(
            new CommandDefinition(
                sql.Get("Listings/GetByAccountId.sql"),
                new { AccountId = accountId, Take = take },
                cancellationToken: cancellationToken));
        return items.AsList();
    }

    public async Task<ListingEditDto?> GetOwnedForEditAsync(
        string adNo,
        long accountId,
        CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ListingEditDto>(
            new CommandDefinition(
                sql.Get("Listings/GetOwnedForEdit.sql"),
                new { AdNo = adNo, AccountId = accountId },
                cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        var images = (await connection.QueryAsync<string>(
            new CommandDefinition(
                sql.Get("Listings/GetImages.sql"),
                new { ListingId = row.Id },
                cancellationToken: cancellationToken))).AsList();

        var attachmentIds = (await connection.QueryAsync<int>(
            new CommandDefinition(
                """
                SELECT attachment_id
                FROM listing_attachments
                WHERE listing_id = @ListingId
                ORDER BY attachment_id
                """,
                new { ListingId = row.Id },
                cancellationToken: cancellationToken))).AsList();

        return new ListingEditDto
        {
            Id = row.Id,
            AdNo = row.AdNo,
            Title = row.Title,
            Description = row.Description,
            CategoryId = row.CategoryId,
            CategoryName = row.CategoryName,
            CapacityMetric = row.CapacityMetric,
            GroupId = row.GroupId,
            GroupName = row.GroupName,
            BrandId = row.BrandId,
            BrandName = row.BrandName,
            ModelId = row.ModelId,
            ModelName = row.ModelName,
            SerialNo = row.SerialNo,
            Condition = row.Condition,
            ModelYear = row.ModelYear,
            Hours = row.Hours,
            Tons = row.Tons,
            CapacityKg = row.CapacityKg,
            Horsepower = row.Horsepower,
            PrimaryIntent = row.PrimaryIntent,
            Price = row.Price,
            Currency = row.Currency,
            PriceUnit = row.PriceUnit,
            IncludesOperator = row.IncludesOperator,
            SellerType = row.SellerType,
            CorporateAccountId = row.CorporateAccountId,
            CorporateName = row.CorporateName,
            CityId = row.CityId,
            CityName = row.CityName,
            DistrictId = row.DistrictId,
            DistrictName = row.DistrictName,
            NeighborhoodId = row.NeighborhoodId,
            NeighborhoodName = row.NeighborhoodName,
            SpecsJson = row.SpecsJson,
            Status = row.Status,
            RejectionReason = row.RejectionReason,
            ImageUrls = images,
            AttachmentIds = attachmentIds
        };
    }

    public async Task<int> CountPublishedAsync(CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql.Get("Listings/CountPublished.sql"), cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SitemapListingEntry>> ListPublishedForSitemapAsync(
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = await connection.QueryAsync<SitemapListingEntry>(
            new CommandDefinition(
                sql.Get("Listings/SitemapPublished.sql"),
                new { Skip = Math.Max(0, skip), Take = Math.Clamp(take, 1, 50_000) },
                cancellationToken: cancellationToken));
        return items.AsList();
    }

    public async Task<ListingDetailDto?> GetByAdNoAsync(
        string adNo,
        ListingAccessContext access,
        CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<ListingDetailRow>(
            new CommandDefinition(
                sql.Get("Listings/GetByAdNo.sql"),
                new
                {
                    AdNo = adNo,
                    ViewerAccountId = access.AccountId,
                    IsAdmin = access.IsAdmin
                },
                cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        var images = (await connection.QueryAsync<string>(
            new CommandDefinition(
                sql.Get("Listings/GetImages.sql"),
                new { ListingId = row.Id },
                cancellationToken: cancellationToken))).AsList();

        if (images.Count == 0 && !string.IsNullOrWhiteSpace(row.CoverImageUrl))
        {
            images.Add(row.CoverImageUrl);
        }

        var attachments = (await connection.QueryAsync<AttachmentItemDto>(
            new CommandDefinition(
                sql.Get("Listings/GetAttachments.sql"),
                new { ListingId = row.Id },
                cancellationToken: cancellationToken))).AsList();

        return new ListingDetailDto
        {
            Id = row.Id,
            AdNo = row.AdNo,
            Title = row.Title,
            Description = row.Description,
            Category = row.Category,
            CategorySlug = row.CategorySlug,
            CategoryId = row.CategoryId,
            CapacityMetric = row.CapacityMetric,
            Brand = row.Brand,
            ModelName = row.ModelName,
            SerialNo = row.SerialNo,
            PrimaryIntent = row.PrimaryIntent,
            Intents = row.Intents,
            Condition = row.Condition,
            ModelYear = row.ModelYear,
            Hours = row.Hours,
            Tons = row.Tons,
            CapacityKg = row.CapacityKg,
            Horsepower = row.Horsepower,
            City = row.City,
            District = row.District,
            Neighborhood = row.Neighborhood,
            Price = row.Price,
            RentPrice = row.RentPrice,
            Currency = Currency.Normalize(row.Currency),
            PriceUnit = row.PriceUnit,
            IncludesOperator = row.IncludesOperator,
            SpecsJson = row.SpecsJson,
            CoverImageUrl = row.CoverImageUrl,
            ImageUrls = images,
            Attachments = attachments,
            SellerName = row.SellerName,
            SellerType = row.SellerType,
            IsVerified = row.IsVerified,
            CorporateAccountId = row.CorporateAccountId,
            CorporateDisplayName = row.CorporateDisplayName,
            CorporateSlug = row.CorporateSlug,
            ListedAt = row.ListedAt,
            Status = row.Status,
            RejectionReason = row.RejectionReason,
            SubmittedAt = row.SubmittedAt,
            OwnerAccountId = row.OwnerAccountId
        };
    }

    public async Task<string?> GetPhoneByAdNoAsync(string adNo, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                sql.Get("Listings/GetPhone.sql"),
                new { AdNo = adNo },
                cancellationToken: cancellationToken));
    }

    private static string ResolveSearchSql(string sort) => sort switch
    {
        ListingSort.PriceAsc => "Listings/SearchPriceAsc.sql",
        ListingSort.PriceDesc => "Listings/SearchPriceDesc.sql",
        ListingSort.HoursAsc => "Listings/SearchHoursAsc.sql",
        _ => "Listings/SearchNewest.sql"
    };

    private static string FilterCacheKey(ListingSearchQuery query)
    {
        var payload = JsonSerializer.Serialize(new
        {
            query.Intent,
            query.CategoryId,
            query.Category,
            query.BrandId,
            query.ModelId,
            query.CityIds,
            query.City,
            query.DistrictIds,
            query.Condition,
            query.SellerType,
            query.YearMin,
            query.YearMax,
            query.HoursMin,
            query.HoursMax,
            query.WeightMin,
            query.WeightMax,
            query.PriceMin,
            query.PriceMax,
            query.HorsepowerMin,
            query.HorsepowerMax,
            query.CapacityKgMin,
            query.CapacityKgMax,
            query.IncludesOperator,
            query.PriceUnit,
            query.VerifiedOnly,
            query.AttachmentIds,
            query.Query,
            query.CorporateAccountId,
            query.SpecsFilterJson,
            query.SpecMinJson
        });
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash.AsSpan(0, 16));
    }

    private static DynamicParameters BuildFilter(ListingSearchQuery query)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Intent", query.Intent, DbType.String);
        parameters.Add("CategoryId", query.CategoryId, DbType.Int32);
        parameters.Add("Category", query.Category, DbType.String);
        parameters.Add("BrandId", query.BrandId, DbType.Int32);
        parameters.Add("ModelId", query.ModelId, DbType.Int32);
        parameters.Add("CorporateAccountId", query.CorporateAccountId, DbType.Int64);
        var cityIds = query.CityIds.Count == 0
            ? Array.Empty<int>()
            : query.CityIds.Where(id => id > 0).Distinct().ToArray();
        var districtIds = query.DistrictIds.Count == 0
            ? Array.Empty<int>()
            : query.DistrictIds.Where(id => id > 0).Distinct().ToArray();
        parameters.Add("HasCityFilter", cityIds.Length > 0, DbType.Boolean);
        parameters.Add("CityIds", cityIds);
        parameters.Add("City", query.City, DbType.String);
        parameters.Add("HasDistrictFilter", districtIds.Length > 0, DbType.Boolean);
        parameters.Add("DistrictIds", districtIds);
        parameters.Add("Condition", query.Condition, DbType.String);
        parameters.Add("SellerType", query.SellerType, DbType.String);
        parameters.Add("YearMin", query.YearMin, DbType.Int32);
        parameters.Add("YearMax", query.YearMax, DbType.Int32);
        parameters.Add("HoursMin", query.HoursMin, DbType.Int32);
        parameters.Add("HoursMax", query.HoursMax, DbType.Int32);
        parameters.Add("WeightMin", query.WeightMin, DbType.Decimal);
        parameters.Add("WeightMax", query.WeightMax, DbType.Decimal);
        parameters.Add("PriceMin", query.PriceMin, DbType.Decimal);
        parameters.Add("PriceMax", query.PriceMax, DbType.Decimal);
        parameters.Add("HorsepowerMin", query.HorsepowerMin, DbType.Int32);
        parameters.Add("HorsepowerMax", query.HorsepowerMax, DbType.Int32);
        parameters.Add("CapacityKgMin", query.CapacityKgMin, DbType.Int32);
        parameters.Add("CapacityKgMax", query.CapacityKgMax, DbType.Int32);
        parameters.Add("IncludesOperator", query.IncludesOperator, DbType.Boolean);
        parameters.Add("PriceUnit", query.PriceUnit, DbType.String);
        parameters.Add("VerifiedOnly", query.VerifiedOnly, DbType.Boolean);
        var attachmentIds = query.AttachmentIds.Count == 0
            ? Array.Empty<int>()
            : query.AttachmentIds.ToArray();
        parameters.Add("HasAttachments", attachmentIds.Length > 0, DbType.Boolean);
        // Always send int[] so Npgsql can type the parameter (null arrays → 42P08).
        parameters.Add("AttachmentIds", attachmentIds);
        parameters.Add("SpecsFilterJson", query.SpecsFilterJson, DbType.String);
        parameters.Add("SpecMinJson", query.SpecMinJson, DbType.String);
        parameters.Add("Query", query.Query, DbType.String);
        return parameters;
    }

    private sealed class ListingDetailRow
    {
        public long Id { get; init; }
        public required string AdNo { get; init; }
        public required string Title { get; init; }
        public required string Description { get; init; }
        public required string Category { get; init; }
        public required string CategorySlug { get; init; }
        public int CategoryId { get; init; }
        public required string CapacityMetric { get; init; }
        public required string Brand { get; init; }
        public required string ModelName { get; init; }
        public string? SerialNo { get; init; }
        public required string PrimaryIntent { get; init; }
        public required string[] Intents { get; init; }
        public required string Condition { get; init; }
        public int ModelYear { get; init; }
        public int? Hours { get; init; }
        public decimal Tons { get; init; }
        public int? CapacityKg { get; init; }
        public int? Horsepower { get; init; }
        public required string City { get; init; }
        public required string District { get; init; }
        public string? Neighborhood { get; init; }
        public decimal Price { get; init; }
        public decimal? RentPrice { get; init; }
        public string? Currency { get; init; }
        public string? PriceUnit { get; init; }
        public bool IncludesOperator { get; init; }
        public required string SpecsJson { get; init; }
        public required string CoverImageUrl { get; init; }
        public required string SellerName { get; init; }
        public required string SellerType { get; init; }
        public bool IsVerified { get; init; }
        public long? CorporateAccountId { get; init; }
        public string? CorporateDisplayName { get; init; }
        public string? CorporateSlug { get; init; }
        public DateTimeOffset ListedAt { get; init; }
        public string Status { get; init; } = ListingStatus.Published;
        public string? RejectionReason { get; init; }
        public DateTimeOffset? SubmittedAt { get; init; }
        public long? OwnerAccountId { get; init; }
    }
}
