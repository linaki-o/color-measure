namespace ColorMeasure.Domain.Entities;

/// <summary>
/// One physical measurement returned by the probe for a given patch.
/// Units: Lv in cd/m^2; X/Y/Z in cd/m^2 (Y == Lv); Tcp in Kelvin.
/// </summary>
public sealed record ColorSample(
    string PatchId,
    ChromaticityPoint Chromaticity,
    double Lv,
    double X,
    double Y,
    double Z,
    double DuV,
    double Tcp,
    string ProbeSerialNo,
    DateTimeOffset MeasuredAt);
