using ColorMeasure.Application.Plans;
using ColorMeasure.Domain;
using ColorMeasure.Domain.Entities;

namespace ColorMeasure.Application.Analysis;

/// <summary>
/// Produces gamut reports from measured samples and validates against pass criteria.
/// All math is deterministic and dependency-free for easy unit testing.
/// </summary>
public static class ColorAnalyzer
{
    // CIE D65 reference white in XYZ (Y normalized to 1.0).
    private const double Xn = 0.95047;
    private const double Yn = 1.00000;
    private const double Zn = 1.08883;

    public static Result<IReadOnlyList<GamutReport>> Analyze(
        IReadOnlyList<ColorSample> samples,
        IReadOnlyList<ColorSpaceSpec> targets,
        PassCriteria pass)
    {
        if (samples.Count == 0)
            return Result<IReadOnlyList<GamutReport>>.Fail(new Error("NO_SAMPLES", "No usable samples were collected"));

        if (TryGetPrimary(samples, "red") is not { } red ||
            TryGetPrimary(samples, "green") is not { } green ||
            TryGetPrimary(samples, "blue") is not { } blue ||
            TryGetPrimary(samples, "white") is not { } white)
        {
            var missing = new List<string>();
            if (TryGetPrimary(samples, "red") is null) missing.Add("red");
            if (TryGetPrimary(samples, "green") is null) missing.Add("green");
            if (TryGetPrimary(samples, "blue") is null) missing.Add("blue");
            if (TryGetPrimary(samples, "white") is null) missing.Add("white");
            return Result<IReadOnlyList<GamutReport>>.Fail(new Error("MISSING_PRIMARIES",
                $"Cannot analyze gamut: missing required primary samples: {string.Join(", ", missing)}"));
        }

        var reports = new List<GamutReport>(targets.Count);
        foreach (var target in targets)
        {
            var measuredArea = PolygonMath.AbsoluteArea(new[]
            {
                ToTuple(red.Chromaticity),
                ToTuple(green.Chromaticity),
                ToTuple(blue.Chromaticity),
            });
            var targetArea = PolygonMath.AbsoluteArea(new[]
            {
                ToTuple(target.Red),
                ToTuple(target.Green),
                ToTuple(target.Blue),
            });
            var coverage = targetArea > 0 ? Math.Min(1.0, measuredArea / targetArea) : 0.0;

            var dE = new List<DeltaEPoint>(3)
            {
                new("R", PrimaryDeltaE(red, target.Red)),
                new("G", PrimaryDeltaE(green, target.Green)),
                new("B", PrimaryDeltaE(blue, target.Blue)),
            };
            var maxDe = dE.Max(p => p.DeltaE2000);
            var duv = DeltaUvPrime(white.Chromaticity, target.White);

            bool passed =
                (pass.MaxDeltaE2000 is null || maxDe <= pass.MaxDeltaE2000.Value) &&
                (pass.MinCoverage is null || coverage >= pass.MinCoverage.Value);

            reports.Add(new GamutReport(
                Target: target,
                MeasuredRed: red.Chromaticity,
                MeasuredGreen: green.Chromaticity,
                MeasuredBlue: blue.Chromaticity,
                MeasuredWhite: white.Chromaticity,
                CoverageRatio: coverage,
                WhiteDeltaUv: duv,
                MaxPrimaryDeltaE2000: maxDe,
                Passed: passed,
                PrimaryDeltaEs: dE));
        }

        return reports;
    }

    private static double PrimaryDeltaE(ColorSample measured, ChromaticityPoint targetXy)
    {
        // Compare chromaticity only (same Y for both) so dE2000 reflects hue/gamut error
        // independent of absolute luminance, which is tracked separately via Lv.
        var measuredLab = XyzToLab(XyzFromChromaticity(measured.Chromaticity, Yn));
        var targetLab = XyzToLab(XyzFromChromaticity(targetXy, Yn));
        return Ciede2000.Compute(measuredLab.L, measuredLab.A, measuredLab.B,
                                 targetLab.L, targetLab.A, targetLab.B);
    }

    private static double DeltaUvPrime(ChromaticityPoint measured, ChromaticityPoint target)
    {
        var (mu, mv) = measured.ToUvPrime1976();
        var (tu, tv) = target.ToUvPrime1976();
        if (double.IsNaN(mu) || double.IsNaN(tu)) return double.NaN;
        var du = mu - tu;
        var dv = mv - tv;
        return Math.Sqrt(du * du + dv * dv);
    }

    public static (double X, double Y, double Z) XyzFromChromaticity(ChromaticityPoint xy, double y)
    {
        if (double.IsNaN(xy.X) || double.IsNaN(xy.Y) || xy.Y == 0) return (double.NaN, double.NaN, double.NaN);
        var x = xy.X * y / xy.Y;
        var z = (1 - xy.X - xy.Y) * y / xy.Y;
        return (x, y, z);
    }

    public static (double L, double A, double B) XyzToLab((double X, double Y, double Z) xyz)
    {
        var fx = F(xyz.X / Xn);
        var fy = F(xyz.Y / Yn);
        var fz = F(xyz.Z / Zn);
        return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
    }

    private static double F(double t)
    {
        const double delta = 6.0 / 29.0;
        return t > delta * delta * delta
            ? Math.Pow(t, 1.0 / 3.0)
            : t / (3 * delta * delta) + 4.0 / 29.0;
    }

    private static ColorSample? TryGetPrimary(IReadOnlyList<ColorSample> samples, string id) =>
        samples.FirstOrDefault(s => string.Equals(s.PatchId, id, StringComparison.OrdinalIgnoreCase));

    private static (double X, double Y) ToTuple(ChromaticityPoint p) => (p.X, p.Y);

    /// <summary>
    /// McCamy approximation of correlated color temperature from CIE 1931 xy.
    /// </summary>
    public static double McCamyCCT(ChromaticityPoint xy)
    {
        if (double.IsNaN(xy.X) || double.IsNaN(xy.Y)) return double.NaN;
        var n = (xy.X - 0.3320) / (xy.Y - 0.1858);
        return 449 * n * n * n + 3525 * n * n + 6823.3 * n + 5520.33;
    }
}
