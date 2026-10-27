namespace ColorMeasure.Domain.Entities;

/// <summary>
/// Result of fitting a power-law (Y = a * V^gamma) to a sequence of grayscale
/// luminance measurements.  Higher R^2 = better fit; for a real display with
/// proper sRGB/BT.1886 OETF, R^2 should be &gt; 0.99.
/// </summary>
public sealed record GammaReport(
    double Gamma,
    double ScaleA,
    double RSquared,
    double BlackLevelCdM2,
    double WhiteLevelCdM2,
    int SampleCount,
    IReadOnlyList<GammaPoint> Curve,
    // Gamma fitted over the "core" midtone range only (V in 0.25..0.75). The full-range
    // fit is pulled down by the sRGB linear toe and by any display-side highlight
    // compression, so the core fit is a cleaner probe of the effective EOTF.
    double? MidtoneGamma = null,
    double? MidtoneRSquared = null);

/// <summary>One measured point in the gamma fit, kept so the report can show the curve.</summary>
public sealed record GammaPoint(double InputNormalized, double LuminanceNormalized);
