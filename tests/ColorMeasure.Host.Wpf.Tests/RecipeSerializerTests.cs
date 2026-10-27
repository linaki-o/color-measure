using System.Text.Json;
using ColorMeasure.Host.Wpf.Models;
using FluentAssertions;
using Xunit;

namespace ColorMeasure.Host.Wpf.Tests;

public class RecipeSerializerTests
{
    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = new Recipe
        {
            Name = "sRGB Coverage",
            Connect = "auto",
            CalibrateBeforeStart = true,
            SettleMs = 2000,
            Targets = new List<string> { "sRGB", "DCI-P3" },
            Patches = new List<RecipePatch>
            {
                new() { Id = "white", Hex = "#FFFFFF", Avg = 8, Label = "white" },
                new() { Id = "red",   Hex = "#FF0000", Avg = 16 },
                new() { Id = "gray-50", Hex = "#808080", Avg = 8, Label = "mid gray" },
            },
            MaxDeltaE2000 = 5.0,
            MinCoverage = 0.90,
        };

        var json = RecipeSerializer.Serialize(original);
        var roundTripped = RecipeSerializer.Deserialize(json);

        roundTripped.Name.Should().Be(original.Name);
        roundTripped.Connect.Should().Be(original.Connect);
        roundTripped.CalibrateBeforeStart.Should().Be(original.CalibrateBeforeStart);
        roundTripped.SettleMs.Should().Be(original.SettleMs);
        roundTripped.Targets.Should().BeEquivalentTo(original.Targets);
        roundTripped.Patches.Should().HaveCount(3);
        roundTripped.Patches[0].Id.Should().Be("white");
        roundTripped.Patches[0].Hex.Should().Be("#FFFFFF");
        roundTripped.Patches[0].Avg.Should().Be(8);
        roundTripped.Patches[0].Label.Should().Be("white");
        roundTripped.Patches[1].Id.Should().Be("red");
        roundTripped.Patches[2].Id.Should().Be("gray-50");
        roundTripped.MaxDeltaE2000.Should().Be(5.0);
        roundTripped.MinCoverage.Should().Be(0.90);
    }

    [Fact]
    public void RoundTrip_EmptyRecipe_AppliesDefaults()
    {
        // Empty Recipe should still produce a valid JSON that round-trips.
        var original = new Recipe();
        var json = RecipeSerializer.Serialize(original);
        var roundTripped = RecipeSerializer.Deserialize(json);

        roundTripped.Name.Should().Be("New Recipe");
        roundTripped.Connect.Should().Be("auto");
        roundTripped.SettleMs.Should().Be(1500);
        roundTripped.Patches.Should().BeEmpty();
        roundTripped.MaxDeltaE2000.Should().Be(5.0);
        roundTripped.MinCoverage.Should().Be(0.90);
        roundTripped.DisplayState.Should().Be("unknown");
    }

    [Fact]
    public void RoundTrip_PreservesDisplayState()
    {
        var original = new Recipe { Name = "hdr", DisplayState = "hdr" };
        var json = RecipeSerializer.Serialize(original);
        json.Should().Contain("\"displayState\": \"hdr\"");

        var roundTripped = RecipeSerializer.Deserialize(json);
        roundTripped.DisplayState.Should().Be("hdr");
    }

    [Fact]
    public void Load_LegacyRecipeWithoutDisplayState_DefaultsToUnknown()
    {
        const string legacy = """
        {
          "name": "legacy",
          "connect": "auto",
          "calibrateBeforeStart": true,
          "settleMs": 1500,
          "targets": ["sRGB"],
          "patches": [{"id":"white","hex":"#FFFFFF","avg":8}]
        }
        """;
        var r = RecipeSerializer.Deserialize(legacy);
        r.DisplayState.Should().Be("unknown");
    }

    [Fact]
    public void RoundTrip_OmitsPassSectionWhenCriteriaAreNull()
    {
        var r = new Recipe
        {
            Name = "all-null",
            MaxDeltaE2000 = null,
            MinCoverage = null,
        };
        var json = RecipeSerializer.Serialize(r);

        // The "pass" object should not be emitted at all when both criteria are null
        // — same schema as the CLI's TestPlanParser. Reader fills in defaults so
        // a freshly-loaded recipe still has a usable pass criteria.
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("pass", out _).Should().BeFalse();

        var roundTripped = RecipeSerializer.Deserialize(json);
        roundTripped.MaxDeltaE2000.Should().Be(5.0);
        roundTripped.MinCoverage.Should().Be(0.90);
    }

    [Fact]
    public void RoundTrip_WritesDefaultsForExistingRecipeFiles()
    {
        // When a recipe was saved by an older version of the tool without a
        // 'pass' section, the loader must apply sensible defaults rather than
        // failing.
        const string legacyJson = """
        {
          "name": "legacy",
          "connect": "auto",
          "calibrateBeforeStart": true,
          "settleMs": 1500,
          "targets": ["sRGB"],
          "patches": [{"id":"white","hex":"#FFFFFF","avg":8}]
        }
        """;
        var r = RecipeSerializer.Deserialize(legacyJson);
        r.Name.Should().Be("legacy");
        r.MaxDeltaE2000.Should().Be(5.0);  // default
        r.MinCoverage.Should().Be(0.90);    // default
    }
}
