using AracParki.Application.Corporate.Dtos;

namespace AracParki.Application.Corporate;

public interface ICorporateAccountStore
{
    Task<long> CreateAsync(long accountId, CorporateProfileData data, CancellationToken cancellationToken);

    /// <summary>Yalnızca draft/rejected durumundaki hesaplar güncellenir; etkilenmediyse false döner.</summary>
    Task<bool> UpdateProfileAsync(long id, long accountId, CorporateProfileData data, CancellationToken cancellationToken);

    Task<CorporateAccountDto?> GetAsync(long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<CorporateAccountDto>> ListByAccountAsync(long accountId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CorporateOptionDto>> ListApprovedByAccountAsync(long accountId, CancellationToken cancellationToken);

    /// <summary>Onaylı ve hesabın sahibi olduğu kurumsal hesabı döner (sihirbaz doğrulaması).</summary>
    Task<CorporateOptionDto?> GetApprovedOptionAsync(long id, long accountId, CancellationToken cancellationToken);

    /// <summary>draft/rejected → pending geçişi; etkilenmediyse false döner.</summary>
    Task<bool> SubmitAsync(long id, long accountId, CancellationToken cancellationToken);

    Task<long> AddDocumentAsync(
        long corporateAccountId,
        string docType,
        string fileName,
        string storageKey,
        string contentType,
        long byteSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CorporateDocumentDto>> ListDocumentsAsync(long corporateAccountId, CancellationToken cancellationToken);
    Task<CorporateDocumentDto?> GetDocumentAsync(long documentId, CancellationToken cancellationToken);
    Task<bool> SoftDeleteDocumentAsync(long documentId, long corporateAccountId, CancellationToken cancellationToken);
    Task SoftDeleteDocumentsByTypeAsync(long corporateAccountId, string docType, CancellationToken cancellationToken);

    Task<CorporateModerationCountsDto> GetModerationCountsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CorporateAccountDto>> ListForModerationAsync(string status, int take, CancellationToken cancellationToken);

    /// <summary>pending → approved; etkilenmediyse false döner.</summary>
    Task<bool> ApproveAsync(long id, long adminAccountId, CancellationToken cancellationToken);

    /// <summary>pending → rejected; etkilenmediyse false döner.</summary>
    Task<bool> RejectAsync(long id, long adminAccountId, string reason, CancellationToken cancellationToken);
}
