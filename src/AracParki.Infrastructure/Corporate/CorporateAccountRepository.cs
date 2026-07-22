using AracParki.Application.Abstractions;
using AracParki.Application.Common;
using AracParki.Application.Corporate;
using AracParki.Application.Corporate.Dtos;
using AracParki.Domain.Corporate;
using Dapper;

namespace AracParki.Infrastructure.Corporate;

public sealed class CorporateAccountRepository(IDbConnectionFactory connectionFactory) : ICorporateAccountStore
{
    private const string AccountSelect = """
        SELECT ca.id,
               ca.account_id AS AccountId,
               ca.company_type AS CompanyType,
               ca.trade_name AS TradeName,
               ca.display_name AS DisplayName,
               ca.slug,
               ca.tax_office AS TaxOffice,
               ca.tax_number AS TaxNumber,
               ca.mersis_no AS MersisNo,
               ca.trade_registry_no AS TradeRegistryNo,
               ca.kep_address AS KepAddress,
               ca.authorized_name AS AuthorizedName,
               ca.phone,
               ca.email,
               ca.website,
               ca.city_id AS CityId,
               ca.district_id AS DistrictId,
               ca.address_line AS AddressLine,
               ca.logo_url AS LogoUrl,
               ca.status,
               ca.rejection_reason AS RejectionReason,
               ca.submitted_at AS SubmittedAt,
               ca.reviewed_at AS ReviewedAt,
               ca.reviewed_by_account_id AS ReviewedByAccountId,
               ca.created_at AS CreatedAt,
               ca.updated_at AS UpdatedAt,
               c.name AS CityName,
               d.name AS DistrictName,
               a.email AS OwnerEmail,
               (a.first_name || ' ' || a.last_name) AS OwnerName
        FROM corporate_accounts ca
        JOIN cities c ON c.id = ca.city_id
        JOIN districts d ON d.id = ca.district_id
        JOIN accounts a ON a.id = ca.account_id
        """;

    public async Task<long> CreateAsync(long accountId, CorporateProfileData data, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var id = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                """
                INSERT INTO corporate_accounts (
                    account_id, company_type, trade_name, display_name, slug,
                    tax_office, tax_number, mersis_no, trade_registry_no, kep_address,
                    authorized_name, phone, email, website,
                    city_id, district_id, address_line, status)
                VALUES (
                    @AccountId, @CompanyType, @TradeName, @DisplayName, @Slug,
                    @TaxOffice, @TaxNumber, @MersisNo, @TradeRegistryNo, @KepAddress,
                    @AuthorizedName, @Phone, @Email, @Website,
                    @CityId, @DistrictId, @AddressLine, 'draft')
                RETURNING id
                """,
                BuildProfileParams(accountId, null, data, temporarySlug: true),
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE corporate_accounts SET slug = @Slug WHERE id = @Id",
                new { Id = id, Slug = SeoSlug.Unique(data.DisplayName, id) },
                cancellationToken: cancellationToken));
        return id;
    }

    public async Task<bool> UpdateProfileAsync(long id, long accountId, CorporateProfileData data, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE corporate_accounts
                SET company_type = @CompanyType,
                    trade_name = @TradeName,
                    display_name = @DisplayName,
                    slug = @Slug,
                    tax_office = @TaxOffice,
                    tax_number = @TaxNumber,
                    mersis_no = @MersisNo,
                    trade_registry_no = @TradeRegistryNo,
                    kep_address = @KepAddress,
                    authorized_name = @AuthorizedName,
                    phone = @Phone,
                    email = @Email,
                    website = @Website,
                    city_id = @CityId,
                    district_id = @DistrictId,
                    address_line = @AddressLine
                WHERE id = @Id
                  AND account_id = @AccountId
                  AND status IN ('draft', 'rejected')
                """,
                BuildProfileParams(accountId, id, data),
                cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<PublicDealerDto?> GetApprovedPublicBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<PublicDealerDto>(
            new CommandDefinition(
                """
                SELECT ca.id,
                       ca.slug,
                       ca.display_name AS DisplayName,
                       ca.company_type AS CompanyType,
                       ca.phone,
                       ca.email,
                       ca.website,
                       ca.logo_url AS LogoUrl,
                       c.name AS CityName,
                       d.name AS DistrictName,
                       ca.address_line AS AddressLine,
                       ca.updated_at AS UpdatedAt
                FROM corporate_accounts ca
                JOIN cities c ON c.id = ca.city_id
                JOIN districts d ON d.id = ca.district_id
                WHERE ca.status = 'approved'
                  AND ca.slug = @Slug
                LIMIT 1
                """,
                new { Slug = slug },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PublicDealerSitemapEntry>> ListApprovedForSitemapAsync(CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PublicDealerSitemapEntry>(
            new CommandDefinition(
                """
                SELECT slug, updated_at AS LastModified
                FROM corporate_accounts
                WHERE status = 'approved'
                ORDER BY id
                """,
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<CorporateAccountDto?> GetAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<CorporateAccountDto>(
            new CommandDefinition(
                AccountSelect + """
                 WHERE ca.id = @Id
                LIMIT 1
                """,
                new { Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CorporateAccountDto>> ListByAccountAsync(long accountId, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CorporateAccountDto>(
            new CommandDefinition(
                AccountSelect + """
                 WHERE ca.account_id = @AccountId
                ORDER BY ca.created_at DESC
                """,
                new { AccountId = accountId },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<CorporateOptionDto>> ListApprovedByAccountAsync(long accountId, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CorporateOptionDto>(
            new CommandDefinition(
                """
                SELECT id,
                       display_name AS DisplayName,
                       trade_name AS TradeName,
                       company_type AS CompanyType,
                       phone AS Phone
                FROM corporate_accounts
                WHERE account_id = @AccountId
                  AND status = 'approved'
                ORDER BY created_at DESC
                """,
                new { AccountId = accountId },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<CorporateOptionDto?> GetApprovedOptionAsync(long id, long accountId, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<CorporateOptionDto>(
            new CommandDefinition(
                """
                SELECT id,
                       display_name AS DisplayName,
                       trade_name AS TradeName,
                       company_type AS CompanyType,
                       phone AS Phone
                FROM corporate_accounts
                WHERE id = @Id
                  AND account_id = @AccountId
                  AND status = 'approved'
                LIMIT 1
                """,
                new { Id = id, AccountId = accountId },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> SubmitAsync(long id, long accountId, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE corporate_accounts
                SET status = 'pending',
                    rejection_reason = NULL,
                    submitted_at = NOW(),
                    reviewed_at = NULL,
                    reviewed_by_account_id = NULL
                WHERE id = @Id
                  AND account_id = @AccountId
                  AND status IN ('draft', 'rejected')
                """,
                new { Id = id, AccountId = accountId },
                cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<long> AddDocumentAsync(
        long corporateAccountId,
        string docType,
        string fileName,
        string storageKey,
        string contentType,
        long byteSize,
        CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                """
                INSERT INTO corporate_documents (
                    corporate_account_id, doc_type, file_name, storage_key, content_type, byte_size)
                VALUES (@CorporateAccountId, @DocType, @FileName, @StorageKey, @ContentType, @ByteSize)
                RETURNING id
                """,
                new
                {
                    CorporateAccountId = corporateAccountId,
                    DocType = docType,
                    FileName = fileName,
                    StorageKey = storageKey,
                    ContentType = contentType,
                    ByteSize = byteSize
                },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CorporateDocumentDto>> ListDocumentsAsync(long corporateAccountId, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CorporateDocumentDto>(
            new CommandDefinition(
                """
                SELECT id,
                       corporate_account_id AS CorporateAccountId,
                       doc_type AS DocType,
                       file_name AS FileName,
                       storage_key AS StorageKey,
                       content_type AS ContentType,
                       byte_size AS ByteSize,
                       uploaded_at AS UploadedAt
                FROM corporate_documents
                WHERE corporate_account_id = @CorporateAccountId
                  AND deleted_at IS NULL
                ORDER BY doc_type, uploaded_at DESC
                """,
                new { CorporateAccountId = corporateAccountId },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<CorporateDocumentDto?> GetDocumentAsync(long documentId, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<CorporateDocumentDto>(
            new CommandDefinition(
                """
                SELECT id,
                       corporate_account_id AS CorporateAccountId,
                       doc_type AS DocType,
                       file_name AS FileName,
                       storage_key AS StorageKey,
                       content_type AS ContentType,
                       byte_size AS ByteSize,
                       uploaded_at AS UploadedAt
                FROM corporate_documents
                WHERE id = @Id
                  AND deleted_at IS NULL
                LIMIT 1
                """,
                new { Id = documentId },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> SoftDeleteDocumentAsync(long documentId, long corporateAccountId, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE corporate_documents
                SET deleted_at = NOW()
                WHERE id = @Id
                  AND corporate_account_id = @CorporateAccountId
                  AND deleted_at IS NULL
                """,
                new { Id = documentId, CorporateAccountId = corporateAccountId },
                cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task SoftDeleteDocumentsByTypeAsync(
        long corporateAccountId,
        string docType,
        CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE corporate_documents
                SET deleted_at = NOW()
                WHERE corporate_account_id = @CorporateAccountId
                  AND doc_type = @DocType
                  AND deleted_at IS NULL
                """,
                new { CorporateAccountId = corporateAccountId, DocType = docType },
                cancellationToken: cancellationToken));
    }

    public async Task<CorporateModerationCountsDto> GetModerationCountsAsync(CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<CorporateModerationCountsDto>(
            new CommandDefinition(
                """
                SELECT
                    COUNT(*) FILTER (WHERE status = 'pending')  AS Pending,
                    COUNT(*) FILTER (WHERE status = 'approved') AS Approved,
                    COUNT(*) FILTER (WHERE status = 'rejected') AS Rejected
                FROM corporate_accounts
                """,
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CorporateAccountDto>> ListForModerationAsync(string status, int take, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CorporateAccountDto>(
            new CommandDefinition(
                AccountSelect + """
                 WHERE ca.status = @Status
                ORDER BY ca.submitted_at DESC NULLS LAST, ca.id DESC
                LIMIT @Take
                """,
                new { Status = status, Take = take },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<bool> ApproveAsync(long id, long adminAccountId, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE corporate_accounts
                SET status = 'approved',
                    rejection_reason = NULL,
                    reviewed_at = NOW(),
                    reviewed_by_account_id = @AdminAccountId
                WHERE id = @Id
                  AND status = 'pending'
                """,
                new { Id = id, AdminAccountId = adminAccountId },
                cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<bool> RejectAsync(long id, long adminAccountId, string reason, CancellationToken cancellationToken)
    {
        await using var connection = (System.Data.Common.DbConnection)await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE corporate_accounts
                SET status = 'rejected',
                    rejection_reason = @Reason,
                    reviewed_at = NOW(),
                    reviewed_by_account_id = @AdminAccountId
                WHERE id = @Id
                  AND status = 'pending'
                """,
                new { Id = id, AdminAccountId = adminAccountId, Reason = reason },
                cancellationToken: cancellationToken));
        return affected > 0;
    }

    private static object BuildProfileParams(
        long accountId,
        long? id,
        CorporateProfileData data,
        bool temporarySlug = false)
    {
        var slug = temporarySlug
            ? "tmp-" + Guid.NewGuid().ToString("N")
            : SeoSlug.Unique(data.DisplayName, id ?? 0);

        return new
        {
            Id = id,
            AccountId = accountId,
            data.CompanyType,
            data.TradeName,
            data.DisplayName,
            Slug = slug,
            data.TaxOffice,
            data.TaxNumber,
            data.MersisNo,
            data.TradeRegistryNo,
            data.KepAddress,
            data.AuthorizedName,
            data.Phone,
            data.Email,
            data.Website,
            data.CityId,
            data.DistrictId,
            data.AddressLine
        };
    }
}
