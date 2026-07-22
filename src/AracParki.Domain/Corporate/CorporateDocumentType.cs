namespace AracParki.Domain.Corporate;

public static class CorporateDocumentType
{
    public const string VergiLevhasi = "vergi_levhasi";
    public const string ImzaSirkuleri = "imza_sirkuleri";
    public const string TicaretSicil = "ticaret_sicil";
    public const string FaaliyetBelgesi = "faaliyet_belgesi";
    public const string YetkiBelgesi = "yetki_belgesi";

    public static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.Ordinal)
    {
        VergiLevhasi,
        ImzaSirkuleri,
        TicaretSicil,
        FaaliyetBelgesi,
        YetkiBelgesi
    };

    /// <summary>Şirket türüne göre onaya gönderim için zorunlu evrak seti.</summary>
    public static IReadOnlyList<string> RequiredFor(string companyType) =>
        CompanyType.IsCapitalCompany(companyType)
            ? [VergiLevhasi, ImzaSirkuleri, TicaretSicil, FaaliyetBelgesi]
            : [VergiLevhasi, ImzaSirkuleri];

    public static string Label(string docType) => docType switch
    {
        VergiLevhasi => "Vergi levhası",
        ImzaSirkuleri => "İmza sirküleri / imza beyannamesi",
        TicaretSicil => "Ticaret sicil gazetesi",
        FaaliyetBelgesi => "Faaliyet belgesi / oda kayıt belgesi",
        YetkiBelgesi => "Yetki belgesi (İkinci El Motorlu Kara Taşıtı Ticareti)",
        _ => docType
    };

    public static string Hint(string docType) => docType switch
    {
        VergiLevhasi => "Vergi dairesinden alınmış güncel vergi levhası.",
        ImzaSirkuleri => "Sermaye şirketlerinde noter onaylı imza sirküleri, şahıs şirketinde imza beyannamesi.",
        TicaretSicil => "Şirket kuruluş ve güncel sicil gazetesi.",
        FaaliyetBelgesi => "Ticaret/meslek odasından son 6 ay içinde alınmış belge.",
        YetkiBelgesi => "İETTS üzerinden alınan yetki belgesi (varsa). Kurumsal hesap onaylanınca doğrulanmış bayi rozeti görünür.",
        _ => ""
    };
}
