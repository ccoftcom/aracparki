using AracParki.Application.Accounts;
using AracParki.Application.Accounts.Services;
using AracParki.Application.Common;
using AracParki.Application.Corporate.Dtos;
using AracParki.Domain.Corporate;

namespace AracParki.Application.Corporate.Services;

public sealed class CorporateAccountService(
    ICorporateAccountStore store,
    ICorporateDocumentStorage documentStorage,
    IAccountStore accounts)
{
    public const int RejectionReasonMaxLength = 1000;
    public const long MaxDocumentBytes = 10 * 1024 * 1024;
    public const int MaxAccountsPerUser = 5;

    private static readonly IReadOnlyDictionary<string, string> AllowedDocumentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = ".pdf",
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png"
        };

    // ── Kullanıcı tarafı ─────────────────────────────────────────────────

    public Task<IReadOnlyList<CorporateAccountDto>> ListMineAsync(long accountId, CancellationToken cancellationToken)
        => store.ListByAccountAsync(accountId, cancellationToken);

    public Task<IReadOnlyList<CorporateOptionDto>> ListApprovedAsync(long accountId, CancellationToken cancellationToken)
        => store.ListApprovedByAccountAsync(accountId, cancellationToken);

    public Task<CorporateOptionDto?> GetApprovedOptionAsync(long id, long accountId, CancellationToken cancellationToken)
        => store.GetApprovedOptionAsync(id, accountId, cancellationToken);

    /// <summary>Sahiplik kontrollü okuma; başka hesabın kaydı için null döner.</summary>
    public async Task<CorporateAccountDto?> GetOwnedAsync(long id, long accountId, CancellationToken cancellationToken)
    {
        var dto = await store.GetAsync(id, cancellationToken);
        return dto is not null && dto.AccountId == accountId ? dto : null;
    }

    /// <summary>Yeni kayıt oluşturur veya draft/rejected kaydı günceller.</summary>
    public async Task<(bool Ok, string? Error, long Id)> SaveAsync(
        long accountId,
        long? id,
        CorporateProfileData input,
        CancellationToken cancellationToken)
    {
        var (data, error) = Normalize(input);
        if (error is not null)
        {
            return (false, error, 0);
        }

        if (id is null or 0)
        {
            var existing = await store.ListByAccountAsync(accountId, cancellationToken);
            if (existing.Count >= MaxAccountsPerUser)
            {
                return (false, $"En fazla {MaxAccountsPerUser} kurumsal hesap oluşturabilirsin.", 0);
            }

            var newId = await store.CreateAsync(accountId, data!, cancellationToken);
            return (true, null, newId);
        }

        var current = await GetOwnedAsync(id.Value, accountId, cancellationToken);
        if (current is null)
        {
            return (false, "Kurumsal hesap bulunamadı.", 0);
        }

        if (current.Status is not (CorporateStatus.Draft or CorporateStatus.Rejected))
        {
            return (false, "Onay sürecindeki veya onaylanmış hesap bilgileri değiştirilemez.", id.Value);
        }

        var updated = await store.UpdateProfileAsync(id.Value, accountId, data!, cancellationToken);
        return updated
            ? (true, null, id.Value)
            : (false, "Kurumsal hesap güncellenemedi.", id.Value);
    }

    /// <summary>Zorunlu evraklar tamamsa draft/rejected → pending.</summary>
    public async Task<(bool Ok, string? Error)> SubmitAsync(long id, long accountId, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            return (false, "Hesap bulunamadı.");
        }

        if (!account.EmailConfirmed)
        {
            return (false, "E-posta adresini doğrulamadan kurumsal başvuru gönderilemez.");
        }

        var current = await GetOwnedAsync(id, accountId, cancellationToken);
        if (current is null)
        {
            return (false, "Kurumsal hesap bulunamadı.");
        }

        if (current.Status == CorporateStatus.Pending)
        {
            return (false, "Başvuru zaten onay bekliyor.");
        }

        if (current.Status == CorporateStatus.Approved)
        {
            return (false, "Bu kurumsal hesap zaten onaylı.");
        }

        var documents = await store.ListDocumentsAsync(id, cancellationToken);
        var presentTypes = documents.Select(static d => d.DocType).ToHashSet(StringComparer.Ordinal);
        var missing = CorporateDocumentType.RequiredFor(current.CompanyType)
            .Where(required => !presentTypes.Contains(required))
            .Select(CorporateDocumentType.Label)
            .ToArray();

        if (missing.Length > 0)
        {
            return (false, $"Eksik evrak: {string.Join(", ", missing)}.");
        }

        var submitted = await store.SubmitAsync(id, accountId, cancellationToken);
        return submitted ? (true, null) : (false, "Başvuru gönderilemedi.");
    }

    public async Task<(bool Ok, string? Error)> UploadDocumentAsync(
        long corporateAccountId,
        long accountId,
        string docType,
        Stream content,
        string contentType,
        string originalFileName,
        long byteSize,
        CancellationToken cancellationToken)
    {
        var current = await GetOwnedAsync(corporateAccountId, accountId, cancellationToken);
        if (current is null)
        {
            return (false, "Kurumsal hesap bulunamadı.");
        }

        if (current.Status == CorporateStatus.Approved)
        {
            return (false, "Onaylı hesabın evrakları değiştirilemez.");
        }

        if (!CorporateDocumentType.Known.Contains(docType))
        {
            return (false, "Evrak türü geçersiz.");
        }

        if (byteSize is <= 0 or > MaxDocumentBytes)
        {
            return (false, "Dosya boyutu en fazla 10 MB olabilir.");
        }

        var (sigOk, detectedType, sigError) =
            await FileSignatures.DetectDocumentAsync(content, cancellationToken);
        if (!sigOk || string.IsNullOrWhiteSpace(detectedType))
        {
            return (false, sigError ?? "Dosya içeriği doğrulanamadı.");
        }

        if (!AllowedDocumentTypes.ContainsKey(detectedType))
        {
            return (false, "Yalnızca PDF, JPG veya PNG yükleyebilirsin.");
        }

        contentType = detectedType;

        if (content.CanSeek)
        {
            content.Position = 0;
        }
        else
        {
            return (false, "Dosya okunamadı. Tekrar dene.");
        }

        var storageKey = await documentStorage.SaveAsync(
            corporateAccountId,
            content,
            contentType,
            originalFileName,
            cancellationToken);

        // Aynı türdeki eski evrak pasife alınır — en güncel olan geçerli.
        await store.SoftDeleteDocumentsByTypeAsync(corporateAccountId, docType, cancellationToken);

        await store.AddDocumentAsync(
            corporateAccountId,
            docType,
            SanitizeFileName(originalFileName, AllowedDocumentTypes[contentType]),
            storageKey,
            contentType,
            byteSize,
            cancellationToken);

        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteDocumentAsync(
        long documentId,
        long corporateAccountId,
        long accountId,
        CancellationToken cancellationToken)
    {
        var current = await GetOwnedAsync(corporateAccountId, accountId, cancellationToken);
        if (current is null)
        {
            return (false, "Kurumsal hesap bulunamadı.");
        }

        if (current.Status == CorporateStatus.Approved)
        {
            return (false, "Onaylı hesabın evrakları değiştirilemez.");
        }

        var deleted = await store.SoftDeleteDocumentAsync(documentId, corporateAccountId, cancellationToken);
        return deleted ? (true, null) : (false, "Evrak bulunamadı.");
    }

    public Task<IReadOnlyList<CorporateDocumentDto>> ListDocumentsAsync(long corporateAccountId, CancellationToken cancellationToken)
        => store.ListDocumentsAsync(corporateAccountId, cancellationToken);

    /// <summary>Sahibi veya admin için evrak stream'i açar; yetkisizse null.</summary>
    public async Task<(Stream Content, CorporateDocumentDto Document)?> OpenDocumentAsync(
        long documentId,
        long requesterAccountId,
        bool requesterIsAdmin,
        CancellationToken cancellationToken)
    {
        var document = await store.GetDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return null;
        }

        if (!requesterIsAdmin)
        {
            var owned = await GetOwnedAsync(document.CorporateAccountId, requesterAccountId, cancellationToken);
            if (owned is null)
            {
                return null;
            }
        }

        var stream = await documentStorage.OpenReadAsync(document.StorageKey, cancellationToken);
        return stream is null ? null : (stream, document);
    }

    // ── Admin tarafı ─────────────────────────────────────────────────────

    public Task<CorporateModerationCountsDto> GetModerationCountsAsync(CancellationToken cancellationToken)
        => store.GetModerationCountsAsync(cancellationToken);

    public Task<IReadOnlyList<CorporateAccountDto>> ListForModerationAsync(
        string? status,
        int take,
        CancellationToken cancellationToken)
    {
        var normalized = string.IsNullOrWhiteSpace(status) ? CorporateStatus.Pending : status.Trim();
        if (!CorporateStatus.Known.Contains(normalized) || normalized == CorporateStatus.Draft)
        {
            normalized = CorporateStatus.Pending;
        }

        return store.ListForModerationAsync(normalized, Math.Clamp(take, 1, 100), cancellationToken);
    }

    public Task<CorporateAccountDto?> GetForModerationAsync(long id, CancellationToken cancellationToken)
        => store.GetAsync(id, cancellationToken);

    public async Task<(bool Ok, string? Error)> ApproveAsync(long id, long adminAccountId, CancellationToken cancellationToken)
    {
        if (adminAccountId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(adminAccountId));
        }

        var approved = await store.ApproveAsync(id, adminAccountId, cancellationToken);
        return approved ? (true, null) : (false, "Başvuru onaylanamadı (durum değişmiş olabilir).");
    }

    public async Task<(bool Ok, string? Error)> RejectAsync(
        long id,
        long adminAccountId,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (adminAccountId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(adminAccountId));
        }

        var trimmed = reason?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            return (false, "Red nedeni zorunlu.");
        }

        if (trimmed.Length > RejectionReasonMaxLength)
        {
            return (false, $"Red nedeni en fazla {RejectionReasonMaxLength} karakter olabilir.");
        }

        var rejected = await store.RejectAsync(id, adminAccountId, trimmed, cancellationToken);
        return rejected ? (true, null) : (false, "Başvuru reddedilemedi (durum değişmiş olabilir).");
    }

    // ── Doğrulama / normalizasyon ────────────────────────────────────────

    public static (CorporateProfileData? Data, string? Error) Normalize(CorporateProfileData input)
    {
        var companyType = input.CompanyType?.Trim() ?? "";
        if (!CompanyType.Known.Contains(companyType))
        {
            return (null, "Şirket türü seç.");
        }

        var tradeName = input.TradeName?.Trim() ?? "";
        if (tradeName.Length is < 3 or > 200)
        {
            return (null, "Ticaret unvanı 3–200 karakter olmalı.");
        }

        var displayName = string.IsNullOrWhiteSpace(input.DisplayName) ? tradeName : input.DisplayName.Trim();
        if (displayName.Length > 120)
        {
            return (null, "Görünen ad en fazla 120 karakter olabilir.");
        }

        var taxOffice = input.TaxOffice?.Trim() ?? "";
        if (taxOffice.Length is < 2 or > 100)
        {
            return (null, "Vergi dairesi gerekli.");
        }

        var taxNumber = DigitsOnly(input.TaxNumber);
        if (companyType == CompanyType.Sahis)
        {
            if (taxNumber.Length != 11)
            {
                return (null, "Şahıs şirketinde TC kimlik numarası 11 hane olmalı.");
            }
        }
        else if (taxNumber.Length is not (10 or 11))
        {
            return (null, "Vergi kimlik numarası 10 hane olmalı.");
        }

        string? mersisNo = DigitsOnly(input.MersisNo);
        if (mersisNo.Length == 0)
        {
            mersisNo = null;
        }
        else if (mersisNo.Length != 16)
        {
            return (null, "MERSİS numarası 16 hane olmalı.");
        }

        var tradeRegistryNo = NullIfEmpty(input.TradeRegistryNo, 50);
        if (tradeRegistryNo is { Length: > 50 })
        {
            return (null, "Ticaret sicil numarası en fazla 50 karakter.");
        }

        if (CompanyType.IsCapitalCompany(companyType) && mersisNo is null)
        {
            return (null, "Limited/Anonim şirketlerde MERSİS numarası zorunlu.");
        }

        var kepAddress = NullIfEmpty(input.KepAddress, 200);
        if (kepAddress is not null && !kepAddress.Contains('@'))
        {
            return (null, "KEP adresi geçersiz.");
        }

        var authorizedName = input.AuthorizedName?.Trim() ?? "";
        if (authorizedName.Length is < 3 or > 120)
        {
            return (null, "Yetkili adı soyadı gerekli.");
        }

        var phone = AccountService.NormalizePhone(input.Phone);
        if (phone is null)
        {
            return (null, "Geçerli bir telefon numarası gir (10–15 rakam).");
        }

        var email = input.Email?.Trim().ToLowerInvariant() ?? "";
        if (email.Length is < 5 or > 200 || !email.Contains('@'))
        {
            return (null, "Geçerli bir e-posta adresi gir.");
        }

        var website = NullIfEmpty(input.Website, 200);
        if (website is not null
            && !website.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !website.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            website = "https://" + website;
        }

        if (input.CityId <= 0)
        {
            return (null, "İl seç.");
        }

        if (input.DistrictId <= 0)
        {
            return (null, "İlçe seç.");
        }

        var addressLine = input.AddressLine?.Trim() ?? "";
        if (addressLine.Length is < 10 or > 500)
        {
            return (null, "Açık adres 10–500 karakter olmalı.");
        }

        return (new CorporateProfileData(
            companyType,
            tradeName,
            displayName,
            taxOffice,
            taxNumber,
            mersisNo,
            tradeRegistryNo,
            kepAddress,
            authorizedName,
            phone,
            email,
            website,
            input.CityId,
            input.DistrictId,
            addressLine), null);
    }

    private static string DigitsOnly(string? value) =>
        new((value ?? "").Where(char.IsDigit).ToArray());

    private static string? NullIfEmpty(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private static string SanitizeFileName(string fileName, string fallbackExtension)
    {
        var name = Path.GetFileName(fileName ?? "").Trim();
        if (name.Length == 0)
        {
            return "evrak" + fallbackExtension;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return cleaned.Length > 150 ? cleaned[^150..] : cleaned;
    }
}
