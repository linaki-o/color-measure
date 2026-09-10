namespace ColorMeasure.Domain.Entities;

/// <summary>
/// A target color space (primary chromaticities + white point) used as a reference for coverage/ΔE analysis.
/// </summary>
public sealed record ColorSpaceSpec(
    string Name,
    ChromaticityPoint Red,
    ChromaticityPoint Green,
    ChromaticityPoint Blue,
    ChromaticityPoint White)
{
    public IReadOnlyList<ChromaticityPoint> Primaries => new[] { Red, Green, Blue };
}
