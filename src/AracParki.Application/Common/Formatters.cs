using System.Globalization;
using AracParki.Domain.Listings;

namespace AracParki.Application.Common;

public static class Formatters
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static string Price(decimal price, string? priceUnit)
    {
        var formatted = price.ToString("N0", Tr) + " ₺";
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

    public static string ListedAt(DateTimeOffset listedAt) => listedAt.ToString("yyyy-MM-dd", Tr);

    public static string Count(int count) => count.ToString("N0", Tr);
}
