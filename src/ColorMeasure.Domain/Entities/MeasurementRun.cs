namespace ColorMeasure.Domain.Entities;

/// <summary>
/// Aggregated analysis result for one measurement run vs. a target color space.
/// </summary>
public sealed record GamutReport(
    ColorSpaceSpec Target,
    ChromaticityPoint MeasuredRed,
    ChromaticityPoint MeasuredGreen,
    ChromaticityPoint MeasuredBlue,
    ChromaticityPoint MeasuredWhite,
    double CoverageRatio,
    double WhiteDeltaUv,
    double MaxPrimaryDeltaE2000,
    bool Passed,
    IReadOnlyList<DeltaEPoint> PrimaryDeltaEs);

public sealed record DeltaEPoint(string Channel, double DeltaE2000);

/// <summary>
/// One full execution of a test plan: the patches requested, the samples collected, and the analysis outputs.
/// </summary>
public sealed record MeasurementRun(
    string PlanName,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string ProbeSerialNo,
    IReadOnlyList<ColorSample> Samples,
    IReadOnlyList<GamutReport> GamutReports);
