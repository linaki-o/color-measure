using System.Text.Json;
using ColorMeasure.Application.Plans;
using ColorMeasure.Domain;
using ColorMeasure.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace ColorMeasure.Application.Tests;

public class TestPlanParserTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    [Fact]
    public void Parse_MinimalPlan_AppliesDefaults()
    {
        var json = "{\"name\":\"demo\",\"patches\":[" +
            "{\"id\":\"red\",\"hex\":\"#FF0000\"}," +
            "{\"id\":\"green\",\"hex\":\"#00FF00\"}," +
            "{\"id\":\"blue\",\"hex\":\"#0000FF\"}," +
            "{\"id\":\"white\",\"hex\":\"#FFFFFF\"}" +
            "]}";
        var dto = JsonSerializer.Deserialize<TestPlanParser.PlanDto>(json, Options)!;
        var result = TestPlanParser.FromDto(dto);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var plan = result.Value!;
        plan.SettleMs.Should().Be(1500);
        plan.Targets.Should().BeEquivalentTo(new[] { "sRGB" });
        plan.Pass.MaxDeltaE2000.Should().BeNull();
    }

    [Fact]
    public void Parse_UnknownTarget_Fails()
    {
        var json = "{\"name\":\"demo\",\"targets\":[\"ProPhotoRGB\"]," +
            "\"patches\":[{\"id\":\"red\",\"hex\":\"#FF0000\"}]}";
        var dto = JsonSerializer.Deserialize<TestPlanParser.PlanDto>(json, Options)!;
        var result = TestPlanParser.FromDto(dto);
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("UNKNOWN_TARGET");
    }

    [Fact]
    public void Parse_BadHex_Fails()
    {
        var json = "{\"name\":\"demo\",\"patches\":[{\"id\":\"red\",\"hex\":\"not-a-color\"}]}";
        var dto = JsonSerializer.Deserialize<TestPlanParser.PlanDto>(json, Options)!;
        var result = TestPlanParser.FromDto(dto);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Parse_ExplicitTargets_AreKept()
    {
        var json = "{\"name\":\"demo\",\"targets\":[\"sRGB\",\"DCI-P3\",\"Rec.2020\"]," +
            "\"patches\":[" +
            "{\"id\":\"red\",\"hex\":\"#FF0000\",\"avg\":16}," +
            "{\"id\":\"green\",\"hex\":\"#00FF00\"}," +
            "{\"id\":\"blue\",\"hex\":\"#0000FF\"}," +
            "{\"id\":\"white\",\"hex\":\"#FFFFFF\"}]," +
            "\"pass\":{\"max_delta_e2000\":3.0,\"min_coverage\":0.9}}";
        var dto = JsonSerializer.Deserialize<TestPlanParser.PlanDto>(json)!;
        var result = TestPlanParser.FromDto(dto);
        result.IsSuccess.Should().BeTrue();
        var plan = result.Value!;
        plan.Targets.Should().BeEquivalentTo(new[] { "sRGB", "DCI-P3", "Rec.2020" });
        plan.Pass.MaxDeltaE2000.Should().Be(3.0);
        plan.Pass.MinCoverage.Should().Be(0.9);
        plan.Patches.Single(p => p.Id == "red").Avg.Should().Be(16);
    }

    /// <summary>
    /// Regression: the parser used to carry a global JsonNamingPolicy.SnakeCaseLower,
    /// which rewrote the *expected* key of every camelCase property to snake_case.
    /// The recipe's "calibrateBeforeStart" / "settleMs" / "displayState" therefore
    /// never matched and silently deserialised as their defaults — meaning
    /// "calibrateBeforeStart": true never enabled ZeroCal in any recipe.
    /// </summary>
    [Fact]
    public void Parse_CamelCaseKeysAreHonoured()
    {
        var json = "{\"name\":\"demo\",\"calibrateBeforeStart\":true,\"settleMs\":4200," +
            "\"displayState\":\"hdr\"," +
            "\"patches\":[" +
            "{\"id\":\"red\",\"hex\":\"#FF0000\",\"avg\":16}," +
            "{\"id\":\"green\",\"hex\":\"#00FF00\"}," +
            "{\"id\":\"blue\",\"hex\":\"#0000FF\"}," +
            "{\"id\":\"white\",\"hex\":\"#FFFFFF\"}]}";
        var dto = JsonSerializer.Deserialize<TestPlanParser.PlanDto>(json)!;
        var result = TestPlanParser.FromDto(dto);
        result.IsSuccess.Should().BeTrue();
        var plan = result.Value!;
        plan.CalibrateBeforeStart.Should().Be(1);
        plan.SettleMs.Should().Be(4200);
        plan.DisplayState.Should().Be(DisplayState.Hdr);
        plan.Patches.Single(p => p.Id == "red").Avg.Should().Be(16);
    }

    [Fact]
    public void Parse_DisplayState_DefaultsToUnknown_AndAcceptsSdrHdr()
    {
        string Build(string? ds)
        {
            var tail = ds is null ? "" : $",\"displayState\":\"{ds}\"";
            return "{\"name\":\"d\"" + tail + ",\"patches\":[" +
                "{\"id\":\"red\",\"hex\":\"#FF0000\"},{\"id\":\"green\",\"hex\":\"#00FF00\"}," +
                "{\"id\":\"blue\",\"hex\":\"#0000FF\"},{\"id\":\"white\",\"hex\":\"#FFFFFF\"}]}";
        }

        var dto1 = JsonSerializer.Deserialize<TestPlanParser.PlanDto>(Build(null))!;
        TestPlanParser.FromDto(dto1).Value!.DisplayState.Should().Be(DisplayState.Unknown);

        var dto2 = JsonSerializer.Deserialize<TestPlanParser.PlanDto>(Build("sdr"))!;
        TestPlanParser.FromDto(dto2).Value!.DisplayState.Should().Be(DisplayState.Sdr);

        var dto3 = JsonSerializer.Deserialize<TestPlanParser.PlanDto>(Build("hdr"))!;
        TestPlanParser.FromDto(dto3).Value!.DisplayState.Should().Be(DisplayState.Hdr);

        // An unrecognised value degrades to Unknown rather than failing the plan.
        var dto4 = JsonSerializer.Deserialize<TestPlanParser.PlanDto>(Build("banana"))!;
        TestPlanParser.FromDto(dto4).Value!.DisplayState.Should().Be(DisplayState.Unknown);
    }
}
