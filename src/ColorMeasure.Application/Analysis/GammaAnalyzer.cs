using ColorMeasure.Domain;
using ColorMeasure.Domain.Entities;

namespace ColorMeasure.Application.Analysis;

/// <summary>
/// Fits a power-law transfer function Y = a * V^gamma to a set of grayscale
/// luminance measurements (Lcd = a * (input/255)^gamma). The fit is done in
/// log-log space (Ordinary Least Squares), which is the standard approach used
/// by every colorimeter/calibrator (CalMAN, i1Profiler, HCFR, etc.).
///
/// To be detected as a "grayscale sample" the input RGB values must be equal
/// (R == G == B); the input level is the byte value / 255. Three or more
/// grayscale samples are required for a stable fit; fewer return a failure
/// result.
/// </summary>
public static class GammaAnalyzer
{
    public const int MinSamples = 2;

    public static Result<GammaReport> Analyze(IReadOnlyList<ColorSample> samples)
    {
        var grays = ExtractGrayscale(samples);
        if (grays.Count < MinSamples)
        {
            return Result<GammaReport>.Fail(new Error("GAMMA_TOO_FEW",
                $"Need at least {MinSamples} grayscale samples to fit a curve, got {grays.Count}"));
        }

        // Sort by input value (0..1 ascending). For an 11-step ramp this gives
        // a smooth fit; even 2 points (min) works but with R^2 trivially = 1.
        var sorted = grays.OrderBy(g => g.Input).ToList();
        var whiteLv = sorted[^1].Luminance;
        var blackLv = sorted[0].Luminance;
        if (!IsFinite(whiteLv) || whiteLv <= 0)
        {
            return Result<GammaReport>.Fail(new Error("GAMMA_BAD_LUMINANCE",
                $"White grayscale sample has non-finite or non-positive luminance ({whiteLv})"));
        }

        // Fit the power law Lv = a * V^gamma directly via log-log OLS:
        //   ln(Lv) = gamma * ln(V) + ln(a)
        // The black-level LIFT (display's actual Lv at V=0) is reported in the
        // result for the user to inspect — but subtracting it before the log
        // would warp the fit (because Lv - black_lv is NOT a pure power law of V
        // when black_lv is non-zero). R^2 in the result tells the user how
        // well the display actually obeys a power law.
        var points = new List<GammaPoint>(sorted.Count);
        var xs = new List<double>(sorted.Count);
        var ys = new List<double>(sorted.Count);
        for (int i = 0; i < sorted.Count; i++)
        {
            var g = sorted[i];
            var yNorm = g.Luminance / whiteLv;     // [0, 1] for proper displays
            points.Add(new GammaPoint(g.Input, yNorm));
            // Skip (0, _) because log(0) is undefined; and skip any non-positive Lv
            // (shouldn't happen if whiteLv > 0 and display is functional).
            if (g.Input > 0 && g.Luminance > 0)
            {
                xs.Add(Math.Log(g.Input));
                ys.Add(Math.Log(g.Luminance));
            }
        }

        if (xs.Count < 2)
        {
            return Result<GammaReport>.Fail(new Error("GAMMA_TOO_FEW_VALID",
                $"Only {xs.Count} valid (input>0, Lv>0) grayscale points; need at least 2 for a line fit"));
        }

        var (gamma, lnA, r2) = LinearFit(xs, ys);

        return Result<GammaReport>.Ok(new GammaReport(
            Gamma: gamma,
            ScaleA: Math.Exp(lnA),
            RSquared: r2,
            BlackLevelCdM2: blackLv,
            WhiteLevelCdM2: whiteLv,
            SampleCount: sorted.Count,
            Curve: points));
    }

    /// <summary>
    /// Pulls out the patches whose RGB input values are equal (grayscale) and
    /// returns them paired with (input level, luminance). Non-grayscale patches
    /// (color primaries) are ignored. The original RGB input is recovered from
    /// the <see cref="ColorSample.PatchId"/> convention — either the hex form
    /// "#RRGGBB" with R==G==B, or the legacy "gray-NN" percent form.
    /// </summary>
    public static List<(double Input, double Luminance)> ExtractGrayscale(IReadOnlyList<ColorSample> samples)
    {
        var result = new List<(double, double)>();
        foreach (var s in samples)
        {
            if (s.PatchId is null) continue;
            if (!IsFinite(s.Lv) || s.Lv < 0) continue;
            var input = TryParseInput(s.PatchId);
            if (input is null) continue;        // not a grayscale identifier
            result.Add((input.Value, s.Lv));
        }
        return result;
    }

    private static double? TryParseInput(string patchId)
    {
        // Convention 1: hex literal like "#808080"
        if (patchId.StartsWith('#') && patchId.Length == 7)
        {
            var r = Convert.ToInt32(patchId.Substring(1, 2), 16);
            var g = Convert.ToInt32(patchId.Substring(3, 2), 16);
            var b = Convert.ToInt32(patchId.Substring(5, 2), 16);
            if (r == g && g == b) return r / 255.0;
            return null;
        }
        // Convention 2: "gray-NN" where NN is the percent (0-100)
        if (patchId.StartsWith("gray-", StringComparison.OrdinalIgnoreCase))
        {
            var tail = patchId.Substring(5);
            if (int.TryParse(tail, out var pct) && pct >= 0 && pct <= 100)
                return pct / 100.0;
        }
        return null;
    }

    private static (double Slope, double Intercept, double R2) LinearFit(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        int n = xs.Count;
        double sumX = 0, sumY = 0;
        for (int i = 0; i < n; i++) { sumX += xs[i]; sumY += ys[i]; }
        double meanX = sumX / n, meanY = sumY / n;

        double sxx = 0, syy = 0, sxy = 0;
        for (int i = 0; i < n; i++)
        {
            var dx = xs[i] - meanX;
            var dy = ys[i] - meanY;
            sxx += dx * dx;
            syy += dy * dy;
            sxy += dx * dy;
        }
        if (sxx == 0) return (double.NaN, double.NaN, double.NaN);

        var slope = sxy / sxx;
        var intercept = meanY - slope * meanX;
        var r2 = (syy > 0) ? (sxy * sxy) / (sxx * syy) : 0.0;
        return (slope, intercept, r2);
    }

    private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
}
