namespace ColorMeasure.Domain.Entities;

/// <summary>
/// Signal-chain state declared by the operator (recorded in the recipe / CLI).
/// The tool never auto-detects this — it is a *claim* about the setup, which the
/// link-health check then tries to corroborate or contradict.
/// </summary>
public enum DisplayState
{
    /// <summary>Not specified / unknown.</summary>
    Unknown = 0,

    /// <summary>Windows HDR off + display OSD HDR off → plain 8-bit sRGB (BT.709) SDR path.</summary>
    Sdr = 1,

    /// <summary>Windows HDR on and/or display OSD HDR on → SDR→HDR conversion active.</summary>
    Hdr = 2,
}

/// <summary>
/// Overall verdict of the signal-chain health check.
/// </summary>
public enum LinkVerdict
{
    /// <summary>Not enough data to judge (no grayscale ramp or no gamut report).</summary>
    Unknown = 0,

    /// <summary>All measured signals are consistent with a clean SDR sRGB path.</summary>
    SdrLikely = 1,

    /// <summary>One signal is off — worth a look, but could be a legitimate display characteristic.</summary>
    Suspicious = 2,

    /// <summary>Multiple signals off, or one critical — the numbers probably do not represent the panel's native colorimetry.</summary>
    NonSdrLikely = 3,
}

public enum LinkSignalSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2,
}

/// <summary>
/// One measured observation about the signal chain, with the observed value and the
/// threshold it was compared against.
/// </summary>
public sealed record LinkSignal(
    string Id,
    string Title,
    string Detail,
    double Value,
    double Threshold,
    LinkSignalSeverity Severity);

/// <summary>
/// Result of the signal-chain health check. Produced by
/// <c>ColorMeasure.Application.Analysis.LinkHealthAnalyzer</c>.
/// </summary>
public sealed record LinkHealthReport(
    LinkVerdict Verdict,
    string Summary,
    DisplayState DeclaredState,
    IReadOnlyList<LinkSignal> Signals)
{
    public bool HasWarning => Signals.Any(s => s.Severity >= LinkSignalSeverity.Warning);
    public bool HasCritical => Signals.Any(s => s.Severity == LinkSignalSeverity.Critical);
}
