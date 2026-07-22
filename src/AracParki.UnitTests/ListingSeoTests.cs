using AracParki.Application.Listings;
using AracParki.Application.Listings.Queries;
using AracParki.Domain.Listings;
using AracParki.Web.Infrastructure;

namespace AracParki.UnitTests;

public sealed class ListingSeoTests
{
    [Fact]
    public void IsIndexableList_allows_intent_category_brand_city()
    {
        var filter = new ListingSearchQuery
        {
            Intent = ListingIntent.Satilik,
            CategoryId = 3,
            BrandId = 7,
            CityIds = [34],
            Page = 1,
            Sort = ListingSort.Newest
        };

        Assert.True(ListingSeo.IsIndexableList(filter));
        Assert.Equal("/ilanlar?tip=satilik&kategoriId=3&markaId=7&ilId=34", ListingSeo.BuildCanonicalListPath(filter));
    }

    [Fact]
    public void IsIndexableList_rejects_sort_search_and_pagination()
    {
        Assert.False(ListingSeo.IsIndexableList(new ListingSearchQuery { Sort = ListingSort.PriceAsc, Page = 1 }));
        Assert.False(ListingSeo.IsIndexableList(new ListingSearchQuery { Query = "ekskavatör", Page = 1 }));
        Assert.False(ListingSeo.IsIndexableList(new ListingSearchQuery { Page = 2 }));
    }

    [Fact]
    public void IsIndexableList_rejects_thin_filters()
    {
        Assert.False(ListingSeo.IsIndexableList(new ListingSearchQuery { PriceMin = 100_000 }));
        Assert.False(ListingSeo.IsIndexableList(new ListingSearchQuery { CityIds = [34, 6] }));
        Assert.False(ListingSeo.IsIndexableList(new ListingSearchQuery { Category = "Ekskavatör" }));
    }

    [Fact]
    public void BuildCanonicalListPath_strips_non_allowlisted_params()
    {
        var path = ListingSeo.BuildCanonicalListPath(new ListingSearchQuery
        {
            Intent = ListingIntent.Kiralik,
            CategoryId = 2,
            Sort = ListingSort.PriceDesc,
            Query = "test",
            Page = 3,
            PriceMin = 10
        });

        Assert.Equal("/ilanlar?tip=kiralik&kategoriId=2", path);
    }
}

public sealed class ListingImageUrlVariantsTests
{
    [Fact]
    public void WithVariant_rewrites_media_delivery_url()
    {
        var url = "https://cdn.example/m/listings/abc.jpg?v=og";
        Assert.Equal(
            "https://cdn.example/m/listings/abc.jpg?v=card",
            ListingImageUrlVariants.WithVariant(url, ListingImageVariants.Card));
    }

    [Fact]
    public void WithVariant_leaves_non_media_urls()
    {
        var url = "/assets/hero/banner.jpg";
        Assert.Equal(url, ListingImageUrlVariants.WithVariant(url, ListingImageVariants.Card));
    }
}
