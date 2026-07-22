using AracParki.Application.Common;

namespace AracParki.UnitTests;

public sealed class FileSignaturesTests
{
    [Fact]
    public void TryDetectImage_jpeg()
    {
        ReadOnlySpan<byte> header = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0];
        Assert.True(FileSignatures.TryDetectImage(header, out var type));
        Assert.Equal("image/jpeg", type);
    }

    [Fact]
    public void TryDetectImage_png()
    {
        ReadOnlySpan<byte> header =
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0, 0, 0, 0
        ];
        Assert.True(FileSignatures.TryDetectImage(header, out var type));
        Assert.Equal("image/png", type);
    }

    [Fact]
    public void IsPdf()
    {
        ReadOnlySpan<byte> header = [(byte)'%', (byte)'P', (byte)'D', (byte)'F', (byte)'-', (byte)'1'];
        Assert.True(FileSignatures.IsPdf(header));
    }

    [Fact]
    public void TryDetectImage_rejects_random()
    {
        ReadOnlySpan<byte> header = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
        Assert.False(FileSignatures.TryDetectImage(header, out _));
    }
}
