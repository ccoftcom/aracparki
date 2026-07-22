using AracParki.Application.Catalog.Dtos;
using AracParki.Web.Pages.IlanVer;

namespace AracParki.UnitTests;

/// <summary>
/// CatalogModelDefaults lives in the Web project; this assembly references Web for these tests.
/// </summary>
public sealed class CatalogModelDefaultsTests
{
    [Fact]
    public void Apply_sets_horsepower_and_locks_from_catalog()
    {
        var draft = new WizardDraft();
        var model = new EquipmentModelOptionDto
        {
            Id = 1,
            Name = "320D",
            Slug = "320d",
            Horsepower = 138,
            TypicalWeightMinT = 20.3m,
            TypicalWeightMaxT = 21.6m,
            DefaultSpecsJson = "{}"
        };

        CatalogModelDefaults.Apply(draft, model, "weight", []);

        Assert.True(draft.HorsepowerFromCatalog);
        Assert.Equal(138, draft.Horsepower);
        Assert.True(draft.TonsFromCatalog);
        Assert.Equal(20.95m, draft.Tons);
        Assert.Equal("138", draft.Specs["engine_power_hp"]);
    }

    [Fact]
    public void Apply_maps_payload_t_to_capacity_kg_attributes()
    {
        var draft = new WizardDraft();
        var model = new EquipmentModelOptionDto
        {
            Id = 20,
            Name = "MT1840",
            Slug = "mt1840",
            CapacityKg = 4000,
            CapacityT = 4.0m,
            DefaultSpecsJson = """{"payload_t": 4.0, "lift_height_m": 17.55}"""
        };
        var attrs = new List<CategoryAttributeDto>
        {
            new() { Id = 1, Key = "lift_capacity_kg", Label = "Kapasite", DataType = "number", Unit = "kg" },
            new() { Id = 2, Key = "lift_height_m", Label = "Kaldırma", DataType = "number", Unit = "m" }
        };

        CatalogModelDefaults.Apply(draft, model, "capacity_kg", attrs);

        Assert.True(draft.CapacityKgFromCatalog);
        Assert.Equal(4000, draft.CapacityKg);
        Assert.Equal("4000", draft.Specs["lift_capacity_kg"]);
        Assert.Equal("17.55", draft.Specs["lift_height_m"]);
        Assert.Contains("lift_capacity_kg", CatalogModelDefaults.LockedSpecKeys(draft));
        Assert.Contains("lift_height_m", CatalogModelDefaults.LockedSpecKeys(draft));
    }

    [Fact]
    public void Apply_platform_model_without_weight_leaves_capacity_for_manual_entry()
    {
        var draft = new WizardDraft();
        var model = new EquipmentModelOptionDto
        {
            Id = 230,
            Name = "170 AETJ",
            Slug = "170-aetj",
            DefaultSpecsJson = """{"platform_height_m": 17.0}"""
        };
        var attrs = new List<CategoryAttributeDto>
        {
            new() { Id = 1, Key = "working_height_m", Label = "Çalışma yüksekliği", DataType = "number", Unit = "m" },
            new() { Id = 2, Key = "platform_capacity_kg", Label = "Platform kapasitesi", DataType = "number", Unit = "kg", IsRequired = true }
        };

        CatalogModelDefaults.Apply(draft, model, "capacity_kg", attrs);

        Assert.False(draft.TonsFromCatalog);
        Assert.Equal(0, draft.Tons);
        Assert.False(draft.CapacityKgFromCatalog);
        Assert.Null(draft.CapacityKg);
        Assert.Equal("17.0", draft.Specs["working_height_m"]);
        Assert.False(draft.Specs.ContainsKey("platform_capacity_kg"));
    }

    [Fact]
    public void ApplyCapacityKgToSpecs_fills_platform_capacity_and_derives_tons()
    {
        var draft = new WizardDraft { CapacityMetric = "capacity_kg" };
        var specs = new Dictionary<string, string>(StringComparer.Ordinal);
        var attrs = new List<CategoryAttributeDto>
        {
            new() { Id = 1, Key = "platform_capacity_kg", Label = "Platform kapasitesi", DataType = "number", Unit = "kg", IsRequired = true }
        };

        CatalogModelDefaults.ApplyCapacityKgToSpecs(draft, 230, specs, attrs);

        Assert.Equal(230, draft.CapacityKg);
        Assert.Equal("230", specs["platform_capacity_kg"]);
        Assert.Equal(0.23m, CatalogModelDefaults.DeriveTonsFromCapacityKg(230));
    }

    [Fact]
    public void PruneSpecs_removes_keys_outside_category()
    {
        var draft = new WizardDraft
        {
            Specs = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["fuel"] = "diesel",
                ["orphan"] = "x"
            }
        };
        var attrs = new List<CategoryAttributeDto>
        {
            new() { Id = 1, Key = "fuel", Label = "Yakıt", DataType = "enum" }
        };

        CatalogModelDefaults.PruneSpecs(draft, attrs);

        Assert.True(draft.Specs.ContainsKey("fuel"));
        Assert.False(draft.Specs.ContainsKey("orphan"));
    }
}
