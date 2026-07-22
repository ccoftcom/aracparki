namespace AracParki.Application.Common;

/// <summary>Magic-byte sniffing for upload validation (does not trust client Content-Type).</summary>
public static class FileSignatures
{
    public const int MinHeaderBytes = 12;

    public static bool TryDetectImage(ReadOnlySpan<byte> header, out string contentType)
    {
        contentType = "";
        if (header.Length < 3)
        {
            return false;
        }

        // JPEG
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            contentType = "image/jpeg";
            return true;
        }

        // PNG
        if (header.Length >= 8
            && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
            && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            contentType = "image/png";
            return true;
        }

        // WebP: RIFF....WEBP
        if (header.Length >= 12
            && header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F'
            && header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
        {
            contentType = "image/webp";
            return true;
        }

        // HEIC/HEIF: ftyp box with heic/heif/mif1 brands
        if (header.Length >= 12
            && header[4] == (byte)'f' && header[5] == (byte)'t' && header[6] == (byte)'y' && header[7] == (byte)'p')
        {
            var brand = System.Text.Encoding.ASCII.GetString(header.Slice(8, 4));
            if (brand is "heic" or "heif" or "mif1" or "msf1" or "heim" or "heis")
            {
                contentType = brand.StartsWith("hei", StringComparison.Ordinal) ? "image/heic" : "image/heif";
                return true;
            }
        }

        return false;
    }

    public static bool IsPdf(ReadOnlySpan<byte> header)
        => header.Length >= 5
           && header[0] == (byte)'%'
           && header[1] == (byte)'P'
           && header[2] == (byte)'D'
           && header[3] == (byte)'F'
           && header[4] == (byte)'-';

    public static async Task<(bool Ok, string? ContentType, string? Error)> DetectImageAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[MinHeaderBytes];
        var read = await ReadExactAsync(stream, header, cancellationToken);
        if (read < 3 || !TryDetectImage(header.AsSpan(0, read), out var detected))
        {
            return (false, null, "Dosya geçerli bir görsel değil (JPEG, PNG, WebP veya HEIC).");
        }

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return (true, detected, null);
    }

    public static async Task<(bool Ok, string? ContentType, string? Error)> DetectDocumentAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[MinHeaderBytes];
        var read = await ReadExactAsync(stream, header, cancellationToken);
        if (read < 5)
        {
            return (false, null, "Dosya içeriği doğrulanamadı.");
        }

        var span = header.AsSpan(0, read);
        if (IsPdf(span))
        {
            Rewind(stream);
            return (true, "application/pdf", null);
        }

        if (TryDetectImage(span, out var imageType)
            && (imageType is "image/jpeg" or "image/png"))
        {
            Rewind(stream);
            return (true, imageType, null);
        }

        return (false, null, "Yalnızca PDF, JPG veya PNG yükleyebilirsin.");
    }

    private static void Rewind(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);
            if (n == 0)
            {
                break;
            }

            total += n;
        }

        return total;
    }
}
