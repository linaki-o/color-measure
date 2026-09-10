namespace ColorMeasure.Application.Analysis;

/// <summary>
/// Shoelace area of a polygon given as ordered xy points. Negative result indicates clockwise winding.
/// </summary>
public static class PolygonMath
{
    public static double SignedArea(IReadOnlyList<(double X, double Y)> points)
    {
        if (points.Count < 3) return 0;
        double sum = 0;
        for (int i = 0; i < points.Count; i++)
        {
            var (x1, y1) = points[i];
            var (x2, y2) = points[(i + 1) % points.Count];
            sum += (x1 * y2) - (x2 * y1);
        }
        return sum / 2.0;
    }

    public static double AbsoluteArea(IReadOnlyList<(double X, double Y)> points) => Math.Abs(SignedArea(points));
}
