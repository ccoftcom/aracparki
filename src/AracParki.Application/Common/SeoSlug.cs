using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AracParki.Application.Common;

public static partial class SeoSlug
{
    public static string From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(Transliterate(ch));
        }

        var slug = NonSlugChars().Replace(sb.ToString().ToLowerInvariant(), "-");
        slug = MultiDash().Replace(slug, "-").Trim('-');
        return slug;
    }

    public static string Unique(string baseSlug, long id)
    {
        var slug = From(baseSlug);
        if (string.IsNullOrEmpty(slug))
        {
            slug = "item";
        }

        return $"{slug}-{id}";
    }

    private static char Transliterate(char ch) => ch switch
    {
        'İ' or 'I' => 'i',
        'ı' => 'i',
        'Ş' or 'ş' => 's',
        'Ğ' or 'ğ' => 'g',
        'Ü' or 'ü' => 'u',
        'Ö' or 'ö' => 'o',
        'Ç' or 'ç' => 'c',
        _ => ch
    };

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonSlugChars();

    [GeneratedRegex(@"-+", RegexOptions.CultureInvariant)]
    private static partial Regex MultiDash();
}
