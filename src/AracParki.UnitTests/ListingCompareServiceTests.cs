using AracParki.Application.Listings.Services;

namespace AracParki.UnitTests;

public sealed class ListingCompareServiceTests
{
    [Fact]
    public void ParseAdNos_normalizes_dedupes_and_caps()
    {
        var parsed = ListingCompareService.ParseAdNos("ap-100021, AP-100015, bad, AP-100021, AP-100020, AP-100014, AP-100013");
        Assert.Equal(["AP-100021", "AP-100015", "AP-100020", "AP-100014"], parsed);
    }

    [Fact]
    public void ParseAdNos_empty_and_invalid()
    {
        Assert.Empty(ListingCompareService.ParseAdNos(null));
        Assert.Empty(ListingCompareService.ParseAdNos(""));
        Assert.Empty(ListingCompareService.ParseAdNos("foo,bar"));
    }

    [Fact]
    public void TryNormalizeAdNo_accepts_ap_prefix()
    {
        Assert.True(ListingCompareService.TryNormalizeAdNo("ap-9", out var adNo));
        Assert.Equal("AP-9", adNo);
        Assert.False(ListingCompareService.TryNormalizeAdNo("XX-1", out _));
    }

    [Fact]
    public void AreDifferent_ignores_empty_and_whitespace_case()
    {
        Assert.False(ListingCompareService.AreDifferent(["A", "a", " A "]));
        Assert.True(ListingCompareService.AreDifferent(["A", "B"]));
        Assert.True(ListingCompareService.AreDifferent(["—", "Var"]));
        Assert.False(ListingCompareService.AreDifferent(["—", ""]));
    }

    [Fact]
    public void Row_marks_difference()
    {
        var same = ListingCompareService.Row("k", "L", ["10", "10"]);
        Assert.False(same.IsDifferent);
        var diff = ListingCompareService.Row("k", "L", ["10", "20"]);
        Assert.True(diff.IsDifferent);
    }

    [Fact]
    public void BuildQueryValue_joins()
    {
        Assert.Equal("AP-1,AP-2", ListingCompareService.BuildQueryValue(["AP-1", "AP-2"]));
    }
}
