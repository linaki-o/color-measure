using System.Text.Json;
using ColorMeasure.Application.Plans;
using ColorMeasure.Domain;
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
        var dto = JsonSerializer.Deserialize<TestPlanParser.PlanDto>(json, Options)!;
        var result = TestPlanParser.FromDto(dto);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var plan = result.Value!;
        plan.Targets.Should().BeEquivalentTo(new[] { "sRGB", "DCI-P3", "Rec.2020" });
        plan.Pass.MaxDeltaE2000.Should().Be(3.0);
        plan.Pass.MinCoverage.Should().Be(0.9);
        plan.Patches.Single(p => p.Id == "red").Avg.Should().Be(16);
    }
}
