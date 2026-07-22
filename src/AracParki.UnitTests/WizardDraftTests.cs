using AracParki.Web.Pages.IlanVer;

namespace AracParki.UnitTests;

public sealed class WizardDraftTests
{
    [Fact]
    public void IsMeaningful_false_for_empty_draft()
    {
        Assert.False(new WizardDraft().IsMeaningful);
    }

    [Fact]
    public void IsMeaningful_true_when_step_advanced()
    {
        Assert.True(new WizardDraft { Step = 2 }.IsMeaningful);
    }

    [Fact]
    public void IsMeaningful_true_when_category_or_images()
    {
        Assert.True(new WizardDraft { CategoryId = 3 }.IsMeaningful);
        Assert.True(new WizardDraft { ImageUrls = ["https://cdn.example.com/a.jpg"] }.IsMeaningful);
        Assert.True(new WizardDraft { Title = "CAT 320" }.IsMeaningful);
    }

    [Fact]
    public void HasIntent_false_for_fresh_draft()
    {
        Assert.False(new WizardDraft().HasIntent);
    }

    [Fact]
    public void HasMachine_requires_capacity_kg_when_metric_is_capacity_kg()
    {
        var draft = new WizardDraft
        {
            CategoryId = 1,
            PrimaryIntent = Domain.Listings.ListingIntent.Satilik,
            Intents = [Domain.Listings.ListingIntent.Satilik],
            BrandId = 1,
            ModelName = "MT1840",
            ModelYear = 2020,
            HoursUnknown = true,
            Tons = 0,
            CapacityMetric = "capacity_kg",
            CapacityKg = null,
            HorsepowerUnknown = true,
            Title = "Test",
            Description = "Desc",
            Condition = Domain.Listings.EquipmentCondition.Used
        };

        Assert.False(draft.HasMachine);
        draft.CapacityKg = 4000;
        Assert.True(draft.HasMachine);
        Assert.True(draft.HasPrimaryCapacity);
    }

    [Fact]
    public void HasMachine_requires_tons_when_metric_is_weight()
    {
        var draft = new WizardDraft
        {
            CategoryId = 1,
            PrimaryIntent = Domain.Listings.ListingIntent.Satilik,
            Intents = [Domain.Listings.ListingIntent.Satilik],
            BrandId = 1,
            ModelName = "320D",
            ModelYear = 2020,
            HoursUnknown = true,
            Tons = 0,
            CapacityMetric = "weight",
            HorsepowerUnknown = true,
            Title = "Test",
            Description = "Desc",
            Condition = Domain.Listings.EquipmentCondition.Used
        };

        Assert.False(draft.HasMachine);
        draft.Tons = 21.5m;
        Assert.True(draft.HasMachine);
    }
}
