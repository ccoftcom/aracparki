using System.Globalization;
using AracParki.Domain.Listings;

namespace AracParki.Application.Common;

public static class Formatters
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Active UI culture from the request (Accept-Language), falling back to tr-TR.</summary>
    private static CultureInfo Ui
    {
        get
        {
            var current = CultureInfo.CurrentUICulture;
            return string.IsNullOrWhiteSpace(current.Name) || current.Name == CultureInfo.InvariantCulture.Name
                ? Tr
                : current;
        }
    }

    public static string Price(decimal price, string? priceUnit, string? currency = null)
    {
        var formatted = price.ToString("N0", Tr) + " " + Currency.Label(currency);
        if (string.IsNullOrWhiteSpace(priceUnit))
        {
            return formatted;
        }

        var unitLabel = PriceUnit.Known.Contains(priceUnit)
            ? PriceUnit.Label(priceUnit)
            : priceUnit;
        return $"{formatted} / {unitLabel}";
    }

    public static string Hours(int hours) => $"{hours.ToString("N0", Tr)} saat";

    public static string Tons(decimal tons) => $"{tons.ToString("0.##", Tr)} ton";

    public static string Horsepower(int hp) => $"{hp} HP";

    public static string ListedAt(DateTimeOffset listedAt) =>
        listedAt.ToLocalTime().ToString("dd MMMM yyyy", Ui);

    public static string DateTime(DateTimeOffset value) =>
        value.ToLocalTime().ToString("d MMM yyyy · HH:mm", Ui);

    public static string Count(int count) => count.ToString("N0", Tr);
}
