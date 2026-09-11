using ColorMeasure.Application.Analysis;
using ColorMeasure.Domain;
using ColorMeasure.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace ColorMeasure.Application.Tests;

public class GammaAnalyzerTests
{
    private static ColorSample Gray(int v255, double lv)
    {
        // PatchId uses hex so the analyzer can parse input as v/255.
        var hex = $"#{v255:X2}{v255:X2}{v255:X2}";
        return new ColorSample(hex, new ChromaticityPoint(0.3127, 0.3290),
            lv, lv * 0.95, lv, lv * 1.09, 0, 6504, "TEST-SN", DateTimeOffset.UtcNow);
    }

    public static IEnumerable<object[]> PerfectGammaCases()
    {
        // (gamma, a, input levels as v/255 in 0..1)
        yield return new object[] { 2.2, 1.0, new[] { 0.0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0 } };
        yield return new object[] { 1.8, 1.0, new[] { 0.0, 0.25, 0.5, 0.75, 1.0 } };
        yield return new object[] { 2.4, 1.0, new[] { 0.0, 0.1, 0.3, 0.5, 0.7, 0.9, 1.0 } };
    }

    [Theory]
    [MemberData(nameof(PerfectGammaCases))]
    public void RecoversExactGammaFromCleanData(double trueGamma, double a, double[] inputs)
    {
        var samples = inputs
            .Select(v => v == 0
                ? Gray(0, 0.5)
                : Gray((int)Math.Round(v * 255), 100.0 * a * Math.Pow(v, trueGamma)))
            .ToList();
        var result = GammaAnalyzer.Analyze(samples);
        result.IsSuccess.Should().BeTrue();
        result.Value!.Gamma.Should().BeApproximately(trueGamma, 0.05);
        result.Value.RSquared.Should().BeGreaterThan(0.99);
    }

    [Fact]
    public void FailsWithTooFewGrayscaleSamples()
    {
        var samples = new[] { Gray(128, 50.0) };  // only 1 grayscale sample
        var result = GammaAnalyzer.Analyze(samples);
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("GAMMA_TOO_FEW");
    }

    [Fact]
    public void SupportsLegacyGrayN_PatchIdConvention()
    {
        // The "gray-25" / "gray-50" / "gray-75" convention from older recipes.
        // Synthesized to follow Lv = 100 * V^2.0 exactly (clean test data).
        double Lv(double v) => 100.0 * Math.Pow(v, 2.0);
        var samples = new[]
        {
            new ColorSample("gray-0",   new ChromaticityPoint(0.3127, 0.3290), Lv(0.00), 0,  Lv(0.00), 0,  0, 6504, "SN", DateTimeOffset.UtcNow),
            new ColorSample("gray-25",  new ChromaticityPoint(0.3127, 0.3290), Lv(0.25), 0,  Lv(0.25), 0,  0, 6504, "SN", DateTimeOffset.UtcNow),
            new ColorSample("gray-50",  new ChromaticityPoint(0.3127, 0.3290), Lv(0.50), 0,  Lv(0.50), 0,  0, 6504, "SN", DateTimeOffset.UtcNow),
            new ColorSample("gray-75",  new ChromaticityPoint(0.3127, 0.3290), Lv(0.75), 0,  Lv(0.75), 0,  0, 6504, "SN", DateTimeOffset.UtcNow),
            new ColorSample("gray-100", new ChromaticityPoint(0.3127, 0.3290), Lv(1.00), 0,  Lv(1.00), 0,  0, 6504, "SN", DateTimeOffset.UtcNow),
        };
        var result = GammaAnalyzer.Analyze(samples);
        result.IsSuccess.Should().BeTrue();
        result.Value!.Gamma.Should().BeApproximately(2.0, 0.01);
        result.Value.RSquared.Should().BeGreaterThan(0.999);
    }

    [Fact]
    public void IgnoresColorPrimaries()
    {
        // Mixed list: some grayscale, some R/G/B primaries. Only grayscale contributes.
        var samples = new List<ColorSample>
        {
            Gray(0, 0.5),
            Gray(64, 5.0),
            Gray(128, 20.0),
            Gray(192, 60.0),
            Gray(255, 100.0),
            new ColorSample("red",   new ChromaticityPoint(0.64, 0.33), 30, 80, 30, 5, 0, 6504, "SN", DateTimeOffset.UtcNow),
            new ColorSample("green", new ChromaticityPoint(0.30, 0.60), 60, 20, 60, 10, 0, 6504, "SN", DateTimeOffset.UtcNow),
        };
        var result = GammaAnalyzer.Analyze(samples);
        result.IsSuccess.Should().BeTrue();
        result.Value!.SampleCount.Should().Be(5);
    }

    [Fact]
    public void ReturnsCurvePointsForPlotting()
    {
        var samples = new[] { Gray(0, 0.5), Gray(128, 30.0), Gray(255, 100.0) };
        var result = GammaAnalyzer.Analyze(samples);
        result.IsSuccess.Should().BeTrue();
        result.Value!.Curve.Should().HaveCount(3);
        result.Value.Curve.First().InputNormalized.Should().Be(0.0);
        result.Value.Curve.Last().InputNormalized.Should().Be(1.0);
    }
}
