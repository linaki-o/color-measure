using Xunit;
using ColorMeasure.Application.Analysis;
using ColorMeasure.Application.Plans;
using ColorMeasure.Domain;
using ColorMeasure.Domain.Entities;
using FluentAssertions;

namespace ColorMeasure.Application.Tests;

public class ColorAnalyzerTests
{
    private static ColorSample MakeSample(string id, double x, double y, double yAbs)
    {
        var xyz = ColorAnalyzer.XyzFromChromaticity(new ChromaticityPoint(x, y), yAbs);
        return new ColorSample(
            PatchId: id,
            Chromaticity: new ChromaticityPoint(x, y),
            Lv: xyz.Y,
            X: xyz.X, Y: xyz.Y, Z: xyz.Z,
            DuV: 0, Tcp: 6500,
            ProbeSerialNo: "TEST",
            MeasuredAt: DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Analyze_PerfectSrgbMatch_ReportsFullCoverage()
    {
        var srgb = ColorSpaceCatalog.Srgb;
        var samples = new[]
        {
            MakeSample("white", srgb.White.X, srgb.White.Y, 100),
            MakeSample("red",   srgb.Red.X,   srgb.Red.Y,   21.26),
            MakeSample("green", srgb.Green.X, srgb.Green.Y, 71.52),
            MakeSample("blue",  srgb.Blue.X,  srgb.Blue.Y,  7.22),
        };

        var result = ColorAnalyzer.Analyze(samples, new[] { srgb }, new PassCriteria(null, null));
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        var reports = result.Value!;
        reports.Should().HaveCount(1);
        var r = reports[0];
        r.CoverageRatio.Should().BeApproximately(1.0, 1e-6);
        r.MaxPrimaryDeltaE2000.Should().BeLessThan(0.01);
        r.Passed.Should().BeTrue();
    }

    [Fact]
    public void Analyze_MeasuredNarrowerThanTarget_CoverageLessThanOne()
    {
        var srgb = ColorSpaceCatalog.Srgb;
        static ChromaticityPoint Mid(ChromaticityPoint a, ChromaticityPoint w) =>
            new((a.X + w.X) / 2, (a.Y + w.Y) / 2);

        var samples = new[]
        {
            MakeSample("white", srgb.White.X, srgb.White.Y, 100),
            MakeSample("red",   Mid(srgb.Red,   srgb.White).X, Mid(srgb.Red,   srgb.White).Y, 21.26),
            MakeSample("green", Mid(srgb.Green, srgb.White).X, Mid(srgb.Green, srgb.White).Y, 71.52),
            MakeSample("blue",  Mid(srgb.Blue,  srgb.White).X, Mid(srgb.Blue,  srgb.White).Y, 7.22),
        };

        var result = ColorAnalyzer.Analyze(samples, new[] { srgb }, new PassCriteria(null, null));
        result.IsSuccess.Should().BeTrue();
        result.Value![0].CoverageRatio.Should().BeApproximately(0.25, 0.02);
    }

    [Fact]
    public void Analyze_FailsWhenPrimaryDeltaEExceedsCriterion()
    {
        var srgb = ColorSpaceCatalog.Srgb;
        var samples = new[]
        {
            MakeSample("white", srgb.White.X, srgb.White.Y, 100),
            MakeSample("red",   0.50, 0.30, 21.26),
            MakeSample("green", srgb.Green.X, srgb.Green.Y, 71.52),
            MakeSample("blue",  srgb.Blue.X, srgb.Blue.Y, 7.22),
        };

        var result = ColorAnalyzer.Analyze(samples, new[] { srgb },
            new PassCriteria(MaxDeltaE2000: 3.0, MinCoverage: null));
        result.IsSuccess.Should().BeTrue();
        result.Value![0].Passed.Should().BeFalse();
    }

    [Fact]
    public void Analyze_NoSamples_ReturnsFailure()
    {
        var result = ColorAnalyzer.Analyze(
            Array.Empty<ColorSample>(),
            new[] { ColorSpaceCatalog.Srgb },
            new PassCriteria(null, null));
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("NO_SAMPLES");
    }

    [Fact]
    public void Analyze_MissingPrimaries_ReturnsFailureWithMissingList()
    {
        var samples = new[] { MakeSample("red", 0.64, 0.33, 21.26) };
        var result = ColorAnalyzer.Analyze(samples, new[] { ColorSpaceCatalog.Srgb }, new PassCriteria(null, null));
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("MISSING_PRIMARIES");
        result.Error.Message.Should().Contain("green").And.Contain("blue").And.Contain("white");
    }

    [Fact]
    public void McCamyCCT_D65_IsInPlausibleRange()
    {
        // McCamy approximation is only accurate on the Planckian locus. We assert a wide
        // range; production code should use Robertson/Hernandez for tighter results.
        var cct = ColorAnalyzer.McCamyCCT(new ChromaticityPoint(0.3127, 0.3290));
        cct.Should().BeInRange(3000, 20000);
    }
}
