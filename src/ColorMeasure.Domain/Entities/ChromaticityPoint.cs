namespace ColorMeasure.Domain.Entities;

/// <summary>
/// CIE 1931 chromaticity coordinate.
/// </summary>
public readonly record struct ChromaticityPoint(double X, double Y)
{
    public static ChromaticityPoint FromXYZ(double x, double y, double z)
    {
        var sum = x + y + z;
        if (sum <= 0) return new ChromaticityPoint(double.NaN, double.NaN);
        return new ChromaticityPoint(x / sum, y / sum);
    }

    public (double Up, double Vp) ToUvPrime1976()
    {
        if (double.IsNaN(X) || double.IsNaN(Y) || Y == 0) return (double.NaN, double.NaN);
        var denom = -2 * X + 12 * Y + 3;
        if (denom == 0) return (double.NaN, double.NaN);
        var u = 4 * X / denom;
        var v = 9 * Y / denom;
        return (u, v);
    }

    public static ChromaticityPoint D65 => new(0.3127, 0.3290);
}
