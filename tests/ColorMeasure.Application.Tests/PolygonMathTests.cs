using Xunit;
using ColorMeasure.Application.Analysis;
using ColorMeasure.Domain;
using FluentAssertions;

namespace ColorMeasure.Application.Tests;

public class PolygonMathTests
{
    [Fact]
    public void Area_UnitTriangle_IsHalf()
    {
        var pts = new (double X, double Y)[] { (0, 0), (1, 0), (0, 1) };
        PolygonMath.AbsoluteArea(pts).Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void Area_Square_IsOne()
    {
        var pts = new (double X, double Y)[] { (0, 0), (1, 0), (1, 1), (0, 1) };
        PolygonMath.AbsoluteArea(pts).Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void Area_LessThanThreePoints_IsZero()
    {
        PolygonMath.AbsoluteArea(new (double X, double Y)[] { (0, 0), (1, 0) }).Should().Be(0);
    }

    [Fact]
    public void Area_SrgbTriangle_HasKnownArea()
    {
        var srgb = ColorSpaceCatalog.Srgb;
        var pts = new (double X, double Y)[]
        {
            (srgb.Red.X, srgb.Red.Y),
            (srgb.Green.X, srgb.Green.Y),
            (srgb.Blue.X, srgb.Blue.Y),
        };
        // Shoelace area of the sRGB primaries triangle in CIE 1931 xy.
        // Computed analytically: 0.5*|(x_R*(y_G - y_B) + x_G*(y_B - y_R) + x_B*(y_R - y_G))|
        //   = 0.5*|0.64*(0.6-0.06) + 0.3*(0.06-0.33) + 0.15*(0.33-0.6)|
        //   = 0.5*|0.3456 - 0.081 - 0.0405| = 0.5*0.2241 = 0.11205
        PolygonMath.AbsoluteArea(pts).Should().BeApproximately(0.11205, 1e-3);
    }
}
