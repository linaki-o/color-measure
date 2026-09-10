using ColorMeasure.Domain.Entities;

namespace ColorMeasure.Domain;

/// <summary>
/// Catalog of well-known color spaces used for coverage analysis.
/// Primaries are CIE 1931 xy.
/// </summary>
public static class ColorSpaceCatalog
{
    public static readonly ColorSpaceSpec Srgb = new(
        "sRGB",
        Red: new ChromaticityPoint(0.6400, 0.3300),
        Green: new ChromaticityPoint(0.3000, 0.6000),
        Blue: new ChromaticityPoint(0.1500, 0.0600),
        White: ChromaticityPoint.D65);

    public static readonly ColorSpaceSpec DciP3 = new(
        "DCI-P3",
        Red: new ChromaticityPoint(0.6800, 0.3200),
        Green: new ChromaticityPoint(0.2650, 0.6900),
        Blue: new ChromaticityPoint(0.1500, 0.0600),
        White: ChromaticityPoint.D65);

    public static readonly ColorSpaceSpec AdobeRgb = new(
        "AdobeRGB",
        Red: new ChromaticityPoint(0.6400, 0.3300),
        Green: new ChromaticityPoint(0.2100, 0.7100),
        Blue: new ChromaticityPoint(0.1500, 0.0600),
        White: ChromaticityPoint.D65);

    public static readonly ColorSpaceSpec Rec2020 = new(
        "Rec.2020",
        Red: new ChromaticityPoint(0.7080, 0.2920),
        Green: new ChromaticityPoint(0.1700, 0.7970),
        Blue: new ChromaticityPoint(0.1310, 0.0460),
        White: ChromaticityPoint.D65);

    public static readonly IReadOnlyList<ColorSpaceSpec> All = new[] { Srgb, DciP3, AdobeRgb, Rec2020 };

    public static ColorSpaceSpec? Find(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
}
