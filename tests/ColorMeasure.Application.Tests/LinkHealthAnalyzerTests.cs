using ColorMeasure.Application.Analysis;
using ColorMeasure.Application.Plans;
using ColorMeasure.Domain;
using ColorMeasure.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace ColorMeasure.Application.Tests;

/// <summary>
/// Tests for the signal-chain health check. The synthetic data below reproduces the
/// measured fingerprints of a real panel measured in SDR mode vs. in HDR mode
/// (Windows HDR on → the 8-bit sRGB signal passes through an SDR→HDR conversion
/// that is neither chromaticity- nor gamma-preserving).
/// </summary>
public class LinkHealthAnalyzerTests
{
    // sRGB standard primaries
    private static readonly (double X, double Y) SRgbR = (0.6400, 0.3300);
    private static readonly (double X, double Y) SRgbG = (0.3000, 0.6000);
    private static readonly (double X, double Y) SRgbB = (0.1500, 0.0600);

    /// <summary>
    /// Build a run that mimics a measured panel.
    /// </summary>
    /// <param name="primaryPull">
    /// Fraction of the sRGB→white path each primary is moved along.
    /// Positive = toward white (saturation compression), negative = away (wider gamut).
    /// </param>
    /// <param name="midtoneGamma">Effective EOTF exponent in the core midtone range.</param>
    private static MeasurementRun BuildRun(
        double primaryPull = 0.0,
        double midtoneGamma = 2.15,
        double blackCdM2 = 0.05,
        double whiteCdM2 = 250.0,
        double whiteX = 0.3127,
        double whiteY = 0.3290)
    {
        var samples = new List<ColorSample>();
        var grays = new (int Byte, double V)[]
        {
            (0, 0.00), (25, 0.098), (51, 0.20), (77, 0.302), (102, 0.40),
            (128, 0.502), (153, 0.60), (179, 0.702), (204, 0.80), (230, 0.902), (255, 1.0),
        };
        foreach (var (b, v) in grays)
        {
            // Synthesize a power-law response with the requested core exponent.
            // Use a mild toe so the full-range fit stays close to the core fit.
            var y = v <= 0 ? 0 : Math.Pow(v, midtoneGamma) * (1 + 0.04 * (1 - v));
            var lv = blackCdM2 + (whiteCdM2 - blackCdM2) * y;
            var hex = $"#{b:X2}{b:X2}{b:X2}";
            samples.Add(new ColorSample(hex, new ChromaticityPoint(whiteX, whiteY),
                lv, lv, lv, lv, 0, 6500, "TEST", DateTimeOffset.UtcNow));
        }

        // Primaries, pulled toward (or away from) the white point.
        var (mx, my) = Pull(SRgbR, (whiteX, whiteY), primaryPull);
        samples.Add(new ColorSample("red", new ChromaticityPoint(mx, my), 60, 80, 60, 5, 0, 6504, "TEST", DateTimeOffset.UtcNow));
        var (gx, gy) = Pull(SRgbG, (whiteX, whiteY), primaryPull);
        samples.Add(new ColorSample("green", new ChromaticityPoint(gx, gy), 120, 40, 120, 10, 0, 6504, "TEST", DateTimeOffset.UtcNow));
        var (bx, by) = Pull(SRgbB, (whiteX, whiteY), primaryPull);
        samples.Add(new ColorSample("blue", new ChromaticityPoint(bx, by), 20, 5, 20, 60, 0, 6504, "TEST", DateTimeOffset.UtcNow));
        samples.Add(new ColorSample("white", new ChromaticityPoint(whiteX, whiteY),
            whiteCdM2, whiteCdM2, whiteCdM2, whiteCdM2, 0, 6504, "TEST", DateTimeOffset.UtcNow));

        var gamuts = ColorAnalyzer.Analyze(samples, new[] { ColorSpaceCatalog.Srgb }, new PassCriteria(null, null)).Value!;
        var gamma = GammaAnalyzer.Analyze(samples).Value!;
        return new MeasurementRun("t", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "TEST",
            samples, gamuts, gamma);
    }

    private static (double X, double Y) Pull((double X, double Y) p, (double X, double Y) white, double f)
    {
        var dx = white.X - p.X;
        var dy = white.Y - p.Y;
        return (p.X + dx * f, p.Y + dy * f);
    }

    [Fact]
    public void CleanSdrRun_IsSdrLikely()
    {
        var run = BuildRun(primaryPull: 0.001, midtoneGamma: 2.15, blackCdM2: 0.05);
        var health = LinkHealthAnalyzer.Analyze(run.Samples, run.GamutReports, run.Gamma, DisplayState.Sdr);

        health.Verdict.Should().Be(LinkVerdict.SdrLikely);
        health.HasWarning.Should().BeFalse();
        health.HasCritical.Should().BeFalse();
    }

    [Fact]
    public void HdrFingerprint_IsDetectedAsNonSdrLikely()
    {
        // The real HDR run measured: all three primaries pulled 1-10% toward white,
        // core gamma flattened to 1.91, black lifted to 0.34 cd/m^2.
        var run = BuildRun(primaryPull: 0.059, midtoneGamma: 1.91, blackCdM2: 0.34);
        var health = LinkHealthAnalyzer.Analyze(run.Samples, run.GamutReports, run.Gamma, DisplayState.Sdr);

        health.Verdict.Should().Be(LinkVerdict.NonSdrLikely);

        var compression = health.Signals.Single(s => s.Id == "saturation-compression");
        compression.Severity.Should().Be(LinkSignalSeverity.Critical);

        var midtone = health.Signals.Single(s => s.Id == "midtone-gamma");
        midtone.Severity.Should().Be(LinkSignalSeverity.Warning);
    }

    [Fact]
    public void WiderThanSrgbPrimary_IsNotFlaggedAsCompression()
    {
        // A genuinely wide-gamut display has primaries pushed AWAY from white.
        // That is a real display property, not a link artifact, so it must not warn.
        var run = BuildRun(primaryPull: -0.10, midtoneGamma: 2.15, blackCdM2: 0.05);
        var health = LinkHealthAnalyzer.Analyze(run.Samples, run.GamutReports, run.Gamma, DisplayState.Sdr);

        var compression = health.Signals.Single(s => s.Id == "saturation-compression");
        compression.Severity.Should().Be(LinkSignalSeverity.Info);
    }

    [Fact]
    public void MissingGammaData_VerdictIsUnknown()
    {
        var run = BuildRun();
        var health = LinkHealthAnalyzer.Analyze(run.Samples, run.GamutReports, null, DisplayState.Unknown);
        health.Verdict.Should().Be(LinkVerdict.Unknown);
    }

    [Fact]
    public void DeclaredSdrButMeasuredNonSdr_SummaryFlagsTheMismatch()
    {
        var run = BuildRun(primaryPull: 0.059, midtoneGamma: 1.91, blackCdM2: 0.34);
        var health = LinkHealthAnalyzer.Analyze(run.Samples, run.GamutReports, run.Gamma, DisplayState.Sdr);

        health.Verdict.Should().Be(LinkVerdict.NonSdrLikely);
        health.Summary.Should().Contain("声明为 SDR");
        health.Summary.Should().Contain("矛盾");
    }

    [Fact]
    public void DeclaredHdrButMeasuredClean_SummaryNotesTheMismatch()
    {
        var run = BuildRun(primaryPull: 0.001, midtoneGamma: 2.15, blackCdM2: 0.05);
        var health = LinkHealthAnalyzer.Analyze(run.Samples, run.GamutReports, run.Gamma, DisplayState.Hdr);

        health.Verdict.Should().Be(LinkVerdict.SdrLikely);
        health.Summary.Should().Contain("声明为 HDR");
    }

    [Fact]
    public void BlackLiftIsDetected()
    {
        var run = BuildRun(primaryPull: 0.001, midtoneGamma: 2.15, blackCdM2: 1.8);
        var health = LinkHealthAnalyzer.Analyze(run.Samples, run.GamutReports, run.Gamma, DisplayState.Unknown);

        var black = health.Signals.Single(s => s.Id == "black-lift");
        black.Severity.Should().Be(LinkSignalSeverity.Critical);
        health.Verdict.Should().Be(LinkVerdict.NonSdrLikely);
    }

    [Fact]
    public void SingleWarning_VerdictIsSuspiciousNotNonSdr()
    {
        // Only the midtone gamma is off; everything else clean.
        var run = BuildRun(primaryPull: 0.001, midtoneGamma: 1.95, blackCdM2: 0.05);
        var health = LinkHealthAnalyzer.Analyze(run.Samples, run.GamutReports, run.Gamma, DisplayState.Sdr);

        health.Verdict.Should().Be(LinkVerdict.Suspicious);
    }
}
