using AracParki.Application.Accounts;
using AracParki.Application.Accounts.Dtos;
using AracParki.Application.Catalog;
using AracParki.Application.Catalog.Dtos;
using AracParki.Application.Catalog.Services;
using AracParki.Application.Corporate;
using AracParki.Application.Corporate.Dtos;
using AracParki.Application.Corporate.Services;
using AracParki.Application.Listings;
using AracParki.Application.Listings.Commands;
using AracParki.Application.Listings.Dtos;
using AracParki.Application.Listings.Services;
using AracParki.Application.Listings.Validation;
using AracParki.Application.Media;
using AracParki.Domain.Corporate;
using AracParki.Domain.Listings;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace AracParki.UnitTests;

public sealed class CorporateAccountServiceTests
{
    [Fact]
    public void Normalize_rejects_invalid_sahis_tax_number()
    {
        var (_, error) = CorporateAccountService.Normalize(ValidProfile() with
        {
            CompanyType = CompanyType.Sahis,
            TaxNumber = "1234567890"
        });

        Assert.Equal("Şahıs şirketinde TC kimlik numarası 11 hane olmalı.", error);
    }

    [Fact]
    public void Normalize_requires_mersis_for_limited()
    {
        var (_, error) = CorporateAccountService.Normalize(ValidProfile() with
        {
            CompanyType = CompanyType.Limited,
            MersisNo = null
        });

        Assert.Equal("Limited/Anonim şirketlerde MERSİS numarası zorunlu.", error);
    }

    [Fact]
    public void Normalize_accepts_valid_sahis_profile()
    {
        var (data, error) = CorporateAccountService.Normalize(ValidProfile());
        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal(CompanyType.Sahis, data!.CompanyType);
        Assert.Equal("12345678901", data.TaxNumber);
    }

    [Fact]
    public async Task Submit_fails_when_required_documents_missing()
    {
        var store = new FakeCorporateStore
        {
            Account = MakeAccount(CorporateStatus.Draft, CompanyType.Sahis)
        };
        var svc = CreateService(store);

        var (ok, error) = await svc.SubmitAsync(1, 10, CancellationToken.None);

        Assert.False(ok);
        Assert.Contains("Eksik evrak", error);
        Assert.Contains("Vergi levhası", error);
    }

    [Fact]
    public async Task Submit_succeeds_with_required_documents()
    {
        var store = new FakeCorporateStore
        {
            Account = MakeAccount(CorporateStatus.Draft, CompanyType.Sahis),
            Documents =
            [
                MakeDoc(CorporateDocumentType.VergiLevhasi),
                MakeDoc(CorporateDocumentType.ImzaSirkuleri)
            ]
        };
        var svc = CreateService(store);

        var (ok, error) = await svc.SubmitAsync(1, 10, CancellationToken.None);

        Assert.True(ok);
        Assert.Null(error);
        Assert.True(store.Submitted);
    }

    [Fact]
    public async Task Submit_fails_when_email_unconfirmed()
    {
        var store = new FakeCorporateStore
        {
            Account = MakeAccount(CorporateStatus.Draft, CompanyType.Sahis),
            Documents =
            [
                MakeDoc(CorporateDocumentType.VergiLevhasi),
                MakeDoc(CorporateDocumentType.ImzaSirkuleri)
            ]
        };
        var svc = CreateService(store, emailConfirmed: false);

        var (ok, error) = await svc.SubmitAsync(1, 10, CancellationToken.None);

        Assert.False(ok);
        Assert.Contains("E-posta", error);
        Assert.False(store.Submitted);
    }

    [Fact]
    public async Task Approve_and_reject_require_pending_status()
    {
        var store = new FakeCorporateStore
        {
            Account = MakeAccount(CorporateStatus.Pending, CompanyType.Sahis)
        };
        var svc = CreateService(store);

        var (okApprove, _) = await svc.ApproveAsync(1, 99, CancellationToken.None);
        Assert.True(okApprove);
        Assert.True(store.Approved);

        store.Approved = false;
        store.RejectResult = true;
        store.Account = MakeAccount(CorporateStatus.Pending, CompanyType.Sahis);
        var (okReject, rejectError) = await svc.RejectAsync(1, 99, "Eksik belge", CancellationToken.None);
        Assert.True(okReject);
        Assert.Null(rejectError);
        Assert.True(store.Rejected);

        var (okEmpty, emptyError) = await svc.RejectAsync(1, 99, "  ", CancellationToken.None);
        Assert.False(okEmpty);
        Assert.Equal("Red nedeni zorunlu.", emptyError);
    }

    [Fact]
    public async Task GetApprovedOption_returns_null_for_other_account()
    {
        var store = new FakeCorporateStore
        {
            ApprovedOption = new CorporateOptionDto
            {
                Id = 5,
                DisplayName = "Firma",
                TradeName = "Firma A.Ş.",
                CompanyType = CompanyType.Limited,
                Phone = "5320000000"
            },
            ApprovedOptionOwnerId = 10
        };
        var svc = CreateService(store);

        var own = await svc.GetApprovedOptionAsync(5, 10, CancellationToken.None);
        Assert.NotNull(own);

        var other = await svc.GetApprovedOptionAsync(5, 99, CancellationToken.None);
        Assert.Null(other);
    }

    [Fact]
    public void CreatePublishedListingValidator_rejects_owner_with_corporate_id()
    {
        var validator = new CreatePublishedListingValidator(
            new ListingImageUrlPolicy(Options.Create(new CloudflareMediaSettings())));
        var baseCmd = MinimalPublishCommand();
        var command = new CreatePublishedListingCommand
        {
            AccountId = baseCmd.AccountId,
            SellerDisplayName = baseCmd.SellerDisplayName,
            Phone = baseCmd.Phone,
            SellerType = SellerType.Owner,
            CorporateAccountId = 3,
            CategoryId = baseCmd.CategoryId,
            BrandId = baseCmd.BrandId,
            ModelName = baseCmd.ModelName,
            CityId = baseCmd.CityId,
            DistrictId = baseCmd.DistrictId,
            PrimaryIntent = baseCmd.PrimaryIntent,
            Intents = baseCmd.Intents,
            Condition = baseCmd.Condition,
            ModelYear = baseCmd.ModelYear,
            Tons = baseCmd.Tons,
            Price = baseCmd.Price,
            Title = baseCmd.Title,
            Description = baseCmd.Description,
            SpecsJson = baseCmd.SpecsJson,
            ImageUrls = baseCmd.ImageUrls
        };

        var result = validator.Validate(command);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Sahibinden"));
    }

    [Fact]
    public void DocumentType_required_set_differs_by_company()
    {
        var sahis = CorporateDocumentType.RequiredFor(CompanyType.Sahis);
        Assert.Contains(CorporateDocumentType.VergiLevhasi, sahis);
        Assert.Contains(CorporateDocumentType.ImzaSirkuleri, sahis);
        Assert.DoesNotContain(CorporateDocumentType.TicaretSicil, sahis);

        var ltd = CorporateDocumentType.RequiredFor(CompanyType.Limited);
        Assert.Contains(CorporateDocumentType.TicaretSicil, ltd);
        Assert.Contains(CorporateDocumentType.FaaliyetBelgesi, ltd);
    }

    [Fact]
    public void CreatePublishedListingValidator_accepts_dealer_with_corporate_id()
    {
        var validator = new CreatePublishedListingValidator(
            new ListingImageUrlPolicy(Options.Create(new CloudflareMediaSettings())));
        var baseCmd = MinimalPublishCommand();
        var command = new CreatePublishedListingCommand
        {
            AccountId = baseCmd.AccountId,
            SellerDisplayName = "Ulutaş Makina",
            Phone = baseCmd.Phone,
            SellerType = SellerType.Dealer,
            CorporateAccountId = 1,
            CategoryId = baseCmd.CategoryId,
            BrandId = baseCmd.BrandId,
            ModelName = baseCmd.ModelName,
            CityId = baseCmd.CityId,
            DistrictId = baseCmd.DistrictId,
            PrimaryIntent = baseCmd.PrimaryIntent,
            Intents = baseCmd.Intents,
            Condition = baseCmd.Condition,
            ModelYear = baseCmd.ModelYear,
            Tons = baseCmd.Tons,
            Price = baseCmd.Price,
            Title = baseCmd.Title,
            Description = baseCmd.Description,
            SpecsJson = baseCmd.SpecsJson,
            ImageUrls = baseCmd.ImageUrls
        };

        var result = validator.Validate(command);
        Assert.DoesNotContain(result.Errors, e => e.PropertyName.Contains("Corporate", StringComparison.OrdinalIgnoreCase)
                                                  || e.ErrorMessage.Contains("Sahibinden", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListingCommandService_preserves_corporate_account_id_on_create()
    {
        var store = new CapturingListingStore();
        var catalog = new CatalogService(
            new EmptyCatalogQuery(),
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        var svc = new ListingCommandService(
            store,
            new CreatePublishedListingValidator(
                new ListingImageUrlPolicy(Options.Create(new CloudflareMediaSettings()))),
            catalog);

        var baseCmd = MinimalPublishCommand();
        await svc.CreatePublishedAsync(
            new CreatePublishedListingCommand
            {
                AccountId = baseCmd.AccountId,
                SellerDisplayName = "Ulutaş Makina",
                Phone = baseCmd.Phone,
                SellerType = SellerType.Dealer,
                CorporateAccountId = 42,
                CategoryId = baseCmd.CategoryId,
                BrandId = baseCmd.BrandId,
                ModelName = baseCmd.ModelName,
                CityId = baseCmd.CityId,
                DistrictId = baseCmd.DistrictId,
                PrimaryIntent = baseCmd.PrimaryIntent,
                Intents = baseCmd.Intents,
                Condition = baseCmd.Condition,
                ModelYear = baseCmd.ModelYear,
                Tons = baseCmd.Tons,
                Price = baseCmd.Price,
                Title = baseCmd.Title,
                Description = baseCmd.Description,
                SpecsJson = baseCmd.SpecsJson,
                ImageUrls = baseCmd.ImageUrls
            },
            CancellationToken.None);

        Assert.NotNull(store.LastCreate);
        Assert.Equal(42, store.LastCreate!.CorporateAccountId);
        Assert.Equal(SellerType.Dealer, store.LastCreate.SellerType);
        Assert.Equal("Ulutaş Makina", store.LastCreate.SellerDisplayName);
    }

    private static CorporateProfileData ValidProfile() => new(
        CompanyType.Sahis,
        "Örnek İş Makinaları",
        "Örnek Galeri",
        "Kadıköy",
        "12345678901",
        null,
        null,
        null,
        "Ali Veli",
        "5320000000",
        "ali@example.com",
        null,
        34,
        1,
        "Caferağa Mah. Örnek Sk. No:1");

    private static CorporateAccountService CreateService(
        ICorporateAccountStore store,
        bool emailConfirmed = true)
    {
        var accounts = Substitute.For<IAccountStore>();
        accounts.FindByIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(new AccountDto
            {
                Id = 10,
                Email = "user@example.com",
                PasswordHash = "x",
                FirstName = "Ada",
                LastName = "Yılmaz",
                SecurityStamp = "stamp",
                EmailConfirmedAt = emailConfirmed ? DateTimeOffset.UtcNow : null
            });
        return new CorporateAccountService(store, new FakeDocStorage(), accounts);
    }

    private static CorporateAccountDto MakeAccount(string status, string companyType) => new()
    {
        Id = 1,
        AccountId = 10,
        CompanyType = companyType,
        TradeName = "Örnek",
        DisplayName = "Örnek",
        Slug = "ornek-1",
        TaxOffice = "Kadıköy",
        TaxNumber = "12345678901",
        AuthorizedName = "Ali Veli",
        Phone = "5320000000",
        Email = "ali@example.com",
        CityId = 34,
        DistrictId = 1,
        AddressLine = "Caferağa Mah. Örnek Sk. No:1",
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static CorporateDocumentDto MakeDoc(string docType) => new()
    {
        Id = Random.Shared.Next(1, 10_000),
        CorporateAccountId = 1,
        DocType = docType,
        FileName = docType + ".pdf",
        StorageKey = "corp-docs/1/" + docType + ".pdf",
        ContentType = "application/pdf",
        ByteSize = 1024,
        UploadedAt = DateTimeOffset.UtcNow
    };

    private static CreatePublishedListingCommand MinimalPublishCommand() => new()
    {
        AccountId = 1,
        SellerDisplayName = "Test",
        Phone = "5320000000",
        SellerType = SellerType.Owner,
        CategoryId = 1,
        BrandId = 1,
        ModelName = "Model",
        CityId = 1,
        DistrictId = 1,
        PrimaryIntent = ListingIntent.Satilik,
        Intents = [ListingIntent.Satilik],
        Condition = EquipmentCondition.Used,
        ModelYear = 2020,
        Tons = 10,
        Price = 100000,
        Title = "Test ilan",
        Description = "<p>Açıklama metni yeterince uzun.</p>",
        SpecsJson = "{}",
        ImageUrls = ["/uploads/listings/1/a.jpg"]
    };

    private sealed class FakeDocStorage : ICorporateDocumentStorage
    {
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
            => Task.FromResult<Stream?>(null);
        public Task<string> SaveAsync(
            long corporateAccountId,
            Stream content,
            string contentType,
            string originalFileName,
            CancellationToken cancellationToken)
            => Task.FromResult($"corp-docs/{corporateAccountId}/x.pdf");
    }

    private sealed class FakeCorporateStore : ICorporateAccountStore
    {
        public CorporateAccountDto? Account { get; set; }
        public IReadOnlyList<CorporateDocumentDto> Documents { get; set; } = [];
        public CorporateOptionDto? ApprovedOption { get; set; }
        public long ApprovedOptionOwnerId { get; set; }
        public bool Submitted { get; set; }
        public bool Approved { get; set; }
        public bool Rejected { get; set; }
        public bool RejectResult { get; set; }

        public Task<long> AddDocumentAsync(long corporateAccountId, string docType, string fileName, string storageKey, string contentType, long byteSize, CancellationToken cancellationToken)
            => Task.FromResult(1L);

        public Task<bool> ApproveAsync(long id, long adminAccountId, CancellationToken cancellationToken)
        {
            Approved = Account?.Status == CorporateStatus.Pending;
            return Task.FromResult(Approved);
        }

        public Task<long> CreateAsync(long accountId, CorporateProfileData data, CancellationToken cancellationToken)
            => Task.FromResult(1L);

        public Task<CorporateAccountDto?> GetAsync(long id, CancellationToken cancellationToken)
            => Task.FromResult(Account is not null && Account.Id == id ? Account : null);

        public Task<CorporateOptionDto?> GetApprovedOptionAsync(long id, long accountId, CancellationToken cancellationToken)
            => Task.FromResult(
                ApprovedOption is not null
                && ApprovedOption.Id == id
                && ApprovedOptionOwnerId == accountId
                    ? ApprovedOption
                    : null);

        public Task<CorporateDocumentDto?> GetDocumentAsync(long documentId, CancellationToken cancellationToken)
            => Task.FromResult(Documents.FirstOrDefault(d => d.Id == documentId));

        public Task<CorporateModerationCountsDto> GetModerationCountsAsync(CancellationToken cancellationToken)
            => Task.FromResult(new CorporateModerationCountsDto());

        public Task<IReadOnlyList<CorporateOptionDto>> ListApprovedByAccountAsync(long accountId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CorporateOptionDto>>([]);

        public Task<IReadOnlyList<CorporateAccountDto>> ListByAccountAsync(long accountId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CorporateAccountDto>>(
                Account is not null && Account.AccountId == accountId ? [Account] : []);

        public Task<IReadOnlyList<CorporateDocumentDto>> ListDocumentsAsync(long corporateAccountId, CancellationToken cancellationToken)
            => Task.FromResult(Documents);

        public Task<IReadOnlyList<CorporateAccountDto>> ListForModerationAsync(string status, int take, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CorporateAccountDto>>([]);

        public Task<bool> RejectAsync(long id, long adminAccountId, string reason, CancellationToken cancellationToken)
        {
            Rejected = RejectResult;
            return Task.FromResult(RejectResult);
        }

        public Task SoftDeleteDocumentsByTypeAsync(long corporateAccountId, string docType, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<bool> SoftDeleteDocumentAsync(long documentId, long corporateAccountId, CancellationToken cancellationToken)
            => Task.FromResult(true);

        public Task<bool> SubmitAsync(long id, long accountId, CancellationToken cancellationToken)
        {
            Submitted = Account is not null
                        && Account.Id == id
                        && Account.AccountId == accountId
                        && Account.Status is CorporateStatus.Draft or CorporateStatus.Rejected;
            return Task.FromResult(Submitted);
        }

        public Task<bool> UpdateProfileAsync(long id, long accountId, CorporateProfileData data, CancellationToken cancellationToken)
            => Task.FromResult(true);

        public Task<PublicDealerDto?> GetApprovedPublicBySlugAsync(string slug, CancellationToken cancellationToken)
            => Task.FromResult<PublicDealerDto?>(null);

        public Task<IReadOnlyList<PublicDealerSitemapEntry>> ListApprovedForSitemapAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PublicDealerSitemapEntry>>([]);
    }

    private sealed class CapturingListingStore : IListingStore
    {
        public CreatePublishedListingCommand? LastCreate { get; private set; }

        public Task ApproveAsync(string adNo, long adminAccountId, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<string> CreatePublishedAsync(
            CreatePublishedListingCommand command,
            CancellationToken cancellationToken)
        {
            LastCreate = command;
            return Task.FromResult("AP-TEST");
        }

        public Task<ModerationCountsDto> GetModerationCountsAsync(CancellationToken cancellationToken)
            => Task.FromResult(new ModerationCountsDto());

        public Task<IReadOnlyList<ModerationListItemDto>> ListForModerationAsync(
            string status,
            int take,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ModerationListItemDto>>([]);

        public Task RejectAsync(string adNo, long adminAccountId, string reason, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task UpdateForReviewAsync(
            string adNo,
            long accountId,
            CreatePublishedListingCommand command,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class EmptyCatalogQuery : ICatalogQuery
    {
        public Task<IReadOnlyList<AttachmentOptionDto>> GetAttachmentsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AttachmentOptionDto>>([]);

        public Task<IReadOnlyList<AttachmentOptionDto>> GetAttachmentsByCategoryAsync(
            int categoryId,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AttachmentOptionDto>>([]);

        public Task<IReadOnlyList<BrandOptionDto>> GetAllBrandsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<BrandOptionDto>>([]);

        public Task<IReadOnlyList<CategoryOptionDto>> GetAllCategoriesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CategoryOptionDto>>([]);

        public Task<IReadOnlyList<CityOptionDto>> GetAllCitiesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CityOptionDto>>([]);

        public Task<IReadOnlyList<FacetCountDto>> GetBrandFacetsAsync(int? categoryId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<FacetCountDto>>([]);

        public Task<IReadOnlyList<BrandOptionDto>> GetBrandsByCategoryAsync(
            int categoryId,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<BrandOptionDto>>([]);

        public Task<IReadOnlyList<CategoryAttributeDto>> GetCategoryAttributesAsync(
            int categoryId,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CategoryAttributeDto>>([]);

        public Task<IReadOnlyList<CategoryGroupDto>> GetCategoryGroupsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CategoryGroupDto>>([]);

        public Task<IReadOnlyList<CategorySummaryDto>> GetCategoriesWithCountsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CategorySummaryDto>>([]);

        public Task<IReadOnlyList<DistrictOptionDto>> GetDistrictsByCitiesAsync(
            IReadOnlyList<int> cityIds,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DistrictOptionDto>>([]);

        public Task<IReadOnlyList<DistrictOptionDto>> GetDistrictsByCityAsync(
            int cityId,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DistrictOptionDto>>([]);

        public Task<EquipmentModelOptionDto?> GetModelByIdAsync(int modelId, CancellationToken cancellationToken)
            => Task.FromResult<EquipmentModelOptionDto?>(null);

        public Task<IReadOnlyList<EquipmentModelOptionDto>> GetModelsByBrandCategoryAsync(
            int brandId,
            int categoryId,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<EquipmentModelOptionDto>>([]);

        public Task<IReadOnlyList<NeighborhoodOptionDto>> GetNeighborhoodsByDistrictAsync(
            int districtId,
            int take,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<NeighborhoodOptionDto>>([]);

        public Task<IReadOnlyList<CitySummaryDto>> GetPopularCitiesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CitySummaryDto>>([]);

        public Task<IReadOnlyList<StreetOptionDto>> GetStreetsByNeighborhoodAsync(
            int neighborhoodId,
            string? query,
            int take,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<StreetOptionDto>>([]);
    }
}
