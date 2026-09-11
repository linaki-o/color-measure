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
    IReadOnlyList<GammaPoint> Curve);

/// <summary>One measured point in the gamma fit, kept so the report can show the curve.</summary>
public sealed record GammaPoint(double InputNormalized, double LuminanceNormalized);
