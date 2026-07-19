using System.Globalization;
using System.Text.Json;
using AracParki.Application.Catalog.Dtos;

namespace AracParki.Application.Listings;

/// <summary>
/// Builds typed specs JSON (bool/number/string) whitelisted against category attributes.
/// </summary>
public static class SpecsJsonBuilder
{
    public const int MaxValueLength = 200;
    public const int MaxJsonBytes = 8_192;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    public static (bool Ok, string? Error, string Json, Dictionary<string, string> StoredRaw) TryBuild(
        IReadOnlyDictionary<string, string>? raw,
        IReadOnlyList<CategoryAttributeDto> attributes)
    {
        var allowed = attributes.ToDictionary(a => a.Key, StringComparer.Ordinal);
        var typed = new Dictionary<string, object?>(StringComparer.Ordinal);
        var storedRaw = new Dictionary<string, string>(StringComparer.Ordinal);

        if (raw is null || raw.Count == 0)
        {
            return (true, null, "{}", storedRaw);
        }

        if (raw.Count > allowed.Count + 5)
        {
            return (false, "Çok fazla özellik gönderildi.", "{}", storedRaw);
        }

        foreach (var (keyRaw, valueRaw) in raw)
        {
            if (string.IsNullOrWhiteSpace(keyRaw) || string.IsNullOrWhiteSpace(valueRaw))
            {
                continue;
            }

            var key = keyRaw.Trim();
            var value = valueRaw.Trim();
            if (value.Length > MaxValueLength)
            {
                return (false, $"'{key}' değeri çok uzun.", "{}", storedRaw);
            }

            if (!allowed.TryGetValue(key, out var attr))
            {
                return (false, "Geçersiz özellik alanı.", "{}", storedRaw);
            }

            object? typedValue;
            switch (attr.DataType)
            {
                case "bool":
                    if (!TryParseBool(value, out var flag))
                    {
                        return (false, $"{attr.Label} için Evet/Hayır seç.", "{}", storedRaw);
                    }

                    typedValue = flag;
                    value = flag ? "true" : "false";
                    break;

                case "number":
                    if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                        && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("tr-TR"), out number))
                    {
                        return (false, $"{attr.Label} sayı olmalı.", "{}", storedRaw);
                    }

                    typedValue = number;
                    value = number.ToString(CultureInfo.InvariantCulture);
                    break;

                case "enum":
                    var options = ParseEnumOptions(attr.EnumOptionsJson);
                    if (options.Count > 0 && !options.Contains(value, StringComparer.Ordinal))
                    {
                        return (false, $"{attr.Label} için geçerli bir seçenek seç.", "{}", storedRaw);
                    }

                    typedValue = value;
                    break;

                default:
                    typedValue = value;
                    break;
            }

            typed[key] = typedValue;
            storedRaw[key] = value;
        }

        foreach (var attr in attributes.Where(a => a.IsRequired))
        {
            if (!typed.ContainsKey(attr.Key))
            {
                return (false, $"{attr.Label} zorunlu.", "{}", storedRaw);
            }
        }

        var json = JsonSerializer.Serialize(typed, JsonOptions);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxJsonBytes)
        {
            return (false, "Özellik verisi çok büyük.", "{}", storedRaw);
        }

        return (true, null, json, storedRaw);
    }

    public static IReadOnlyList<SpecDisplayRow> ToDisplayRows(
        string? specsJson,
        IReadOnlyList<CategoryAttributeDto> attributes)
    {
        if (string.IsNullOrWhiteSpace(specsJson) || specsJson is "{}")
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(specsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var byKey = attributes.ToDictionary(a => a.Key, StringComparer.Ordinal);
            var rows = new List<SpecDisplayRow>();

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var label = byKey.TryGetValue(prop.Name, out var attr) ? attr.Label : prop.Name;
                var unit = byKey.TryGetValue(prop.Name, out var a) ? a.Unit : null;
                rows.Add(new SpecDisplayRow(label, FormatValue(prop.Value, unit)));
            }

            return rows;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string FormatValue(JsonElement value, string? unit)
    {
        var text = value.ValueKind switch
        {
            JsonValueKind.True => "Evet",
            JsonValueKind.False => "Hayır",
            JsonValueKind.Number => value.TryGetDecimal(out var d)
                ? d.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR"))
                : value.ToString(),
            JsonValueKind.String => value.GetString() switch
            {
                "true" => "Evet",
                "false" => "Hayır",
                var s => s ?? ""
            },
            _ => value.ToString()
        };

        return string.IsNullOrWhiteSpace(unit) ? text : $"{text} {unit}";
    }

    private static bool TryParseBool(string value, out bool flag)
    {
        if (bool.TryParse(value, out flag))
        {
            return true;
        }

        if (value is "1" or "evet" or "Evet" or "yes")
        {
            flag = true;
            return true;
        }

        if (value is "0" or "hayır" or "Hayır" or "no")
        {
            flag = false;
            return true;
        }

        flag = false;
        return false;
    }

    private static IReadOnlyList<string> ParseEnumOptions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

public readonly record struct SpecDisplayRow(string Label, string Value);
