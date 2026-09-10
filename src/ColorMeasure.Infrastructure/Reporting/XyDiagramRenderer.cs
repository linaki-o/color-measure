using ColorMeasure.Domain.Abstractions;
using SkiaSharp;

namespace ColorMeasure.Infrastructure.Reporting;

public interface IXyDiagramRenderer
{
    byte[] RenderPng(Domain.Entities.MeasurementRun run, int width, int height);
}

/// <summary>
/// Renders a CIE 1931 xy chromaticity diagram with the spectral locus filled in
/// visible-spectrum colors (fan triangulation from D65), the line of purples as a
/// dashed edge, target gamut outlines, and measured primaries as dashed overlays.
/// </summary>
public sealed class XyDiagramRenderer : IXyDiagramRenderer
{
    private static readonly SKColor[] GamutPalette =
    {
        new(31, 119, 180), new(44, 160, 44), new(255, 127, 14),
        new(214, 39, 40),  new(148, 103, 189), new(140, 86, 75),
        new(23, 190, 207),
    };

    // CIE 1931 spectral locus 380-770nm + ITU-R purple limit at (0.7347, 0.2653).
    // The purple limit (nm == 0) is the closing endpoint of the line of purples.
    private static readonly (double X, double Y, double Nm)[] Locus =
    {
        (0.1741, 0.0050, 380), (0.1738, 0.0049, 390), (0.1733, 0.0048, 400), (0.1726, 0.0048, 410),
        (0.1714, 0.0051, 420), (0.1703, 0.0058, 430), (0.1689, 0.0069, 440), (0.1669, 0.0086, 450),
        (0.1644, 0.0109, 460), (0.1611, 0.0138, 470), (0.1566, 0.0177, 480), (0.1510, 0.0227, 490),
        (0.1440, 0.0297, 500), (0.1355, 0.0389, 510), (0.1241, 0.0578, 520), (0.1096, 0.0868, 530),
        (0.0913, 0.1327, 540), (0.0687, 0.2007, 550), (0.0454, 0.2950, 560), (0.0235, 0.4127, 570),
        (0.0082, 0.5384, 580), (0.0039, 0.6548, 590), (0.0139, 0.7502, 600), (0.0389, 0.8120, 610),
        (0.0743, 0.8338, 620), (0.1142, 0.8262, 630), (0.1547, 0.8059, 640), (0.1929, 0.7816, 650),
        (0.2296, 0.7543, 660), (0.2658, 0.7243, 670), (0.3016, 0.6923, 680), (0.3373, 0.6589, 690),
        (0.3731, 0.6245, 700), (0.4087, 0.5896, 710), (0.4441, 0.5547, 720), (0.4788, 0.5202, 730),
        (0.5125, 0.4866, 740), (0.5448, 0.4544, 750), (0.5755, 0.4242, 760), (0.6042, 0.3965, 770),
        (0.7347, 0.2653, 0),
    };

    private static readonly (double X, double Y) WhitePoint = (0.3127, 0.3290);

    public byte[] RenderPng(Domain.Entities.MeasurementRun run, int width, int height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888);
        using var surface = SKSurface.Create(info);
        var c = surface.Canvas;
        c.Clear(SKColors.White);

        const float left = 40, right = 40, top = 30, bottom = 50;
        var plot = SKRect.Create(left, top, width - left - right, height - top - bottom);

        DrawGrid(c, plot);
        DrawColoredHorseshoe(c, plot);
        DrawGamuts(c, plot, run);
        DrawLegend(c, plot, run);

        using (var border = new SKPaint { Color = SKColors.Black, IsStroke = true, StrokeWidth = 1 })
        {
            c.DrawRect(plot, border);
        }
        using var txtFont = new SKFont(SKTypeface.Default, 11);
        using (var txt = new SKPaint { Color = SKColors.Black, IsAntialias = true })
        {
            c.DrawText("0.0", plot.Left - 18, plot.Bottom + 14, SKTextAlign.Left, txtFont, txt);
            c.DrawText("0.4", plot.Left + plot.Width / 2 - 8, plot.Bottom + 14, SKTextAlign.Left, txtFont, txt);
            c.DrawText("0.8", plot.Right - 14, plot.Bottom + 14, SKTextAlign.Left, txtFont, txt);
            c.DrawText("CIE 1931 x", plot.Right - 60, plot.Bottom + 32, SKTextAlign.Left, txtFont, txt);
        }

        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void DrawGrid(SKCanvas c, SKRect plot)
    {
        using var grid = new SKPaint { Color = new SKColor(230, 230, 230), StrokeWidth = 1, IsStroke = true, IsAntialias = true };
        for (double x = 0; x <= 0.8; x += 0.1)
        {
            var px = (float)(plot.Left + x / 0.8 * plot.Width);
            c.DrawLine(px, plot.Top, px, plot.Bottom, grid);
        }
        for (double y = 0; y <= 0.9; y += 0.1)
        {
            var py = (float)(plot.Top + (1 - y / 0.9) * plot.Height);
            c.DrawLine(plot.Left, py, plot.Right, py, grid);
        }
    }

    /// <summary>
    /// Fan triangulation from D65 to consecutive spectral-locus points. Each triangle
    /// is colored by its midpoint wavelength; the closing wedge (last real point → purple
    /// limit) gets a synthetic purple so the line-of-purples area is visually distinct.
    /// </summary>
    private static void DrawColoredHorseshoe(SKCanvas c, SKRect plot)
    {
        var wp = ToPixel(WhitePoint.X, WhitePoint.Y, plot);

        // Pre-compute pixel positions once; reused for fan fill + smooth outline.
        var pixels = new SKPoint[Locus.Length];
        for (int i = 0; i < Locus.Length; i++)
            pixels[i] = ToPixel(Locus[i].X, Locus[i].Y, plot);

        // Fan wedges meeting at midpoints so the outer boundary is C1-continuous — no
        // visible polygon facets even at high zoom. Translucent so target gamut
        // triangles drawn on top remain readable.
        using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(0, 0, 0, 140) };
        for (int i = 0; i < Locus.Length - 1; i++)
        {
            var w1 = Locus[i].Nm;
            var w2 = Locus[i + 1].Nm;
            paint.Color = (w2 == 0)
                ? new SKColor(120, 0, 130, 140)
                : WithAlpha(WavelengthToColor((w1 + w2) / 2.0), 140);

            var m = new SKPoint(
                (pixels[i].X + pixels[i + 1].X) / 2f,
                (pixels[i].Y + pixels[i + 1].Y) / 2f);

            using var path = new SKPath();
            path.MoveTo(wp);
            path.LineTo(pixels[i]);
            path.LineTo(m);
            path.Close();
            c.DrawPath(path, paint);
        }

        // Dashed line of purples on top of the fill.
        using (var dashEffect = SKPathEffect.CreateDash(new float[] { 5, 3 }, 0))
        using (var lopStroke = new SKPaint
        {
            Color = new SKColor(60, 60, 60, 220),
            IsStroke = true, StrokeWidth = 1f, IsAntialias = true, PathEffect = dashEffect,
        })
        {
            c.DrawLine(pixels[0], pixels[^1], lopStroke);
        }

        // Smooth spectral-locus outline (quadratic-Bezier through midpoints → C1 curve).
        using (var locusStroke = new SKPaint
        {
            Color = new SKColor(40, 40, 40),
            IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true,
        })
        {
            c.DrawPath(BuildSmoothLocusPath(pixels, includePurpleLimit: false), locusStroke);
        }

        // Wavelength tick labels at 480 / 580 / 680 for orientation.
        using var labelFont = new SKFont(SKTypeface.Default, 9);
        using var labelPaint = new SKPaint { Color = new SKColor(40, 40, 40), IsAntialias = true };
        foreach (var (x, y, nm) in Locus)
        {
            if (nm != 480 && nm != 580 && nm != 680) continue;
            var pixel = ToPixel(x, y, plot);
            c.DrawText($"{nm}", pixel.X + 4, pixel.Y - 4, SKTextAlign.Left, labelFont, labelPaint);
        }
    }

    /// <summary>
    /// Smooth curve passing through every consecutive midpoint of <paramref name="pixels"/>,
    /// using each input point as a quadratic-Bezier control point. Result is C1-continuous
    /// with one control per vertex — no visible polygon facets. Set
    /// <paramref name="includePurpleLimit"/> to <c>false</c> for the open spectral locus;
    /// the line of purples closes the gap separately.
    /// </summary>
    private static SKPath BuildSmoothLocusPath(SKPoint[] pixels, bool includePurpleLimit)
    {
        var end = includePurpleLimit ? pixels.Length : pixels.Length - 1;
        var path = new SKPath();
        path.MoveTo(pixels[0]);
        for (int i = 1; i < end - 1; i++)
        {
            var mid = new SKPoint(
                (pixels[i].X + pixels[i + 1].X) / 2f,
                (pixels[i].Y + pixels[i + 1].Y) / 2f);
            path.QuadTo(pixels[i], mid);
        }
        path.LineTo(pixels[end - 1]);
        return path;
    }

    /// <summary>
    /// Approximate visible-spectrum color from wavelength (Bruton's piecewise-linear fit).
    /// </summary>
    private static SKColor WavelengthToColor(double nm)
    {
        if (nm < 380) nm = 380;
        if (nm > 780) nm = 780;
        double r, g, b;
        if (nm < 440)      { r = -(nm - 440) / 60; g = 0;          b = 1; }
        else if (nm < 490) { r = 0;                   g = (nm - 440) / 50; b = 1; }
        else if (nm < 510) { r = 0;                   g = 1;          b = -(nm - 510) / 20; }
        else if (nm < 580) { r = (nm - 510) / 70;    g = 1;          b = 0; }
        else if (nm < 645) { r = 1;                   g = -(nm - 645) / 65; b = 0; }
        else               { r = 1;                   g = 0;          b = 0; }

        double factor = 1.0;
        if (nm < 420) factor = 0.3 + 0.7 * (nm - 380) / 40.0;
        else if (nm > 700) factor = 0.3 + 0.7 * (780.0 - nm) / 80.0;
        r *= factor; g *= factor; b *= factor;

        return new SKColor(
            (byte)Math.Clamp(255 * r, 0, 255),
            (byte)Math.Clamp(255 * g, 0, 255),
            (byte)Math.Clamp(255 * b, 0, 255));
    }

    private static SKColor WithAlpha(SKColor color, byte alpha) => new(color.Red, color.Green, color.Blue, alpha);

    private static void DrawGamuts(SKCanvas c, SKRect plot, Domain.Entities.MeasurementRun run)
    {
        var dashEffect = SKPathEffect.CreateDash(new float[] { 6, 4 }, 0);
        for (int idx = 0; idx < run.GamutReports.Count; idx++)
        {
            var g = run.GamutReports[idx];
            var color = GamutPalette[idx % GamutPalette.Length];
            var measuredColor = new SKColor(
                (byte)(color.Red * 7 / 10),
                (byte)(color.Green * 7 / 10),
                (byte)(color.Blue * 7 / 10));

            // Primary-channel colors used by the R→B gradient fill: red at R,
            // green at the projection of G onto the R-B edge, blue at B. This is
            // the standard rtings / notebookcheck chromaticity-fill convention.
            var rPx = ToPixel(g.Target.Red.X, g.Target.Red.Y, plot);
            var gPx = ToPixel(g.Target.Green.X, g.Target.Green.Y, plot);
            var bPx = ToPixel(g.Target.Blue.X, g.Target.Blue.Y, plot);

            var dx = bPx.X - rPx.X;
            var dy = bPx.Y - rPx.Y;
            var lenSq = dx * dx + dy * dy;
            var projG = lenSq > 0 ? (gPx.X - rPx.X) * dx + (gPx.Y - rPx.Y) * dy : 0;

            var shader = SKShader.CreateLinearGradient(
                rPx, bPx,
                new SKColor[] { new(255, 34, 34), new(34, 204, 34), new(34, 68, 255) },
                new float[] { 0f, (float)(projG / lenSq), 1f },
                SKShaderTileMode.Clamp);

            DrawGamutTriangle(c, plot, g.Target.Red, g.Target.Green, g.Target.Blue, g.Target.White,
                SKColors.Transparent, color, 2.2f, false,
                dashEffect: null, fillShader: shader, fillAlpha: 115);

            DrawGamutTriangle(c, plot,
                g.MeasuredRed, g.MeasuredGreen, g.MeasuredBlue, g.MeasuredWhite,
                SKColors.Transparent,
                measuredColor, 1.8f, true, dashEffect);

            // Primary vertex markers (R red / G green / B blue) on the target triangle.
            var primaryColors = new SKColor[] { new(255, 34, 34), new(34, 204, 34), new(34, 68, 255) };
            var primaryPoints = new[] { rPx, gPx, bPx };
            for (int p = 0; p < 3; p++)
            {
                using var stroke = new SKPaint { Color = SKColors.White, IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true };
                c.DrawCircle(primaryPoints[p], 4.5f, new SKPaint { Color = primaryColors[p], IsAntialias = true });
                c.DrawCircle(primaryPoints[p], 4.5f, stroke);
            }

            // Whichever white point is finite: small filled dot in the muted palette color.
            if (IsFinite(g.MeasuredWhite.X) && IsFinite(g.MeasuredWhite.Y))
            {
                var wp = ToPixel(g.MeasuredWhite.X, g.MeasuredWhite.Y, plot);
                using var dotFill = new SKPaint { Color = measuredColor, IsAntialias = true };
                using var dotStroke = new SKPaint { Color = SKColors.White, IsStroke = true, StrokeWidth = 1f, IsAntialias = true };
                c.DrawCircle(wp, 4f, dotFill);
                c.DrawCircle(wp, 4f, dotStroke);
            }
        }
    }

    private static void DrawGamutTriangle(SKCanvas c, SKRect plot,
        Domain.Entities.ChromaticityPoint r,
        Domain.Entities.ChromaticityPoint g,
        Domain.Entities.ChromaticityPoint b,
        Domain.Entities.ChromaticityPoint w,
        SKColor fill, SKColor stroke, float strokeWidth, bool dashed, SKPathEffect? dashEffect,
        SKShader? fillShader = null, byte fillAlpha = 0)
    {
        if (!IsFinite(r.X) || !IsFinite(r.Y) || !IsFinite(g.X) || !IsFinite(g.Y) ||
            !IsFinite(b.X) || !IsFinite(b.Y))
        {
            return;
        }
        using var path = new SKPath();
        path.MoveTo(ToPixel(r.X, r.Y, plot));
        path.LineTo(ToPixel(g.X, g.Y, plot));
        path.LineTo(ToPixel(b.X, b.Y, plot));
        path.Close();

        // Either a flat alpha fill OR a shader-driven fill (the R/G/B gradient).
        if (fillShader is not null)
        {
            using var fillPaint = new SKPaint { Shader = fillShader, IsAntialias = true, Color = new SKColor(0, 0, 0, fillAlpha) };
            c.DrawPath(path, fillPaint);
        }
        else if (fill.Alpha > 0)
        {
            using var fillPaint = new SKPaint { Color = fill, IsAntialias = true };
            c.DrawPath(path, fillPaint);
        }
        var strokePaint = new SKPaint
        {
            Color = stroke, IsStroke = true, StrokeWidth = strokeWidth, IsAntialias = true,
        };
        if (dashed && dashEffect is not null) strokePaint.PathEffect = dashEffect;
        using (strokePaint) c.DrawPath(path, strokePaint);

        if (IsFinite(w.X) && IsFinite(w.Y))
        {
            using var dot = new SKPaint { Color = stroke, IsAntialias = true };
            c.DrawCircle(ToPixel(w.X, w.Y, plot), 2.5f, dot);
        }
    }

    private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

    private static SKPoint ToPixel(double x, double y, SKRect plot)
    {
        const double maxX = 0.8, maxY = 0.9;
        var px = (float)(plot.Left + (x / maxX) * plot.Width);
        var py = (float)(plot.Top + (1 - y / maxY) * plot.Height);
        return new SKPoint(px, py);
    }

    private static void DrawLegend(SKCanvas c, SKRect plot, Domain.Entities.MeasurementRun run)
    {
        const int rowHeight = 22;
        const int padX = 12;
        const int padY = 10;
        const int swatchSize = 14;
        const int gap = 8;

        using var titleFont = new SKFont(SKTypeface.Default, 13);
        using var textFont = new SKFont(SKTypeface.Default, 13);
        using var textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };

        var rows = run.GamutReports.Count;
        var blockW = 260;
        var blockH = padY * 2 + 18 + rows * rowHeight;
        var blockX = plot.Right - blockW - 8;
        var blockY = plot.Top + 8;

        using (var bg = new SKPaint { Color = new SKColor(255, 255, 255, 230), IsAntialias = true })
        {
            c.DrawRoundRect(new SKRoundRect(SKRect.Create(blockX, blockY, blockW, blockH), 4, 4), bg);
        }
        using (var border = new SKPaint { Color = new SKColor(180, 180, 180), IsStroke = true, StrokeWidth = 1, IsAntialias = true })
        {
            c.DrawRoundRect(new SKRoundRect(SKRect.Create(blockX, blockY, blockW, blockH), 4, 4), border);
        }

        c.DrawText("Coverage (target vs measured)", blockX + padX, blockY + padY + 12,
            SKTextAlign.Left, titleFont, textPaint);

        for (int i = 0; i < rows; i++)
        {
            var g = run.GamutReports[i];
            var color = GamutPalette[i % GamutPalette.Length];
            var measuredColor = new SKColor(
                (byte)(color.Red * 7 / 10),
                (byte)(color.Green * 7 / 10),
                (byte)(color.Blue * 7 / 10));
            var yMid = blockY + padY + 28 + i * rowHeight;

            using (var targetFill = new SKPaint { Color = new SKColor(color.Red, color.Green, color.Blue, 90), IsAntialias = true })
            using (var targetStroke = new SKPaint { Color = color, IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true })
            {
                var r = SKRect.Create(blockX + padX, yMid - swatchSize / 2, swatchSize, swatchSize);
                c.DrawRect(r, targetFill);
                c.DrawRect(r, targetStroke);
            }
            using (var measuredStroke = new SKPaint
            {
                Color = measuredColor,
                IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true,
                PathEffect = SKPathEffect.CreateDash(new float[] { 3, 2 }, 0),
            })
            {
                var r = SKRect.Create(blockX + padX + swatchSize + 4, yMid - swatchSize / 2, swatchSize, swatchSize);
                c.DrawRect(r, measuredStroke);
            }

            var textX = blockX + padX + swatchSize * 2 + 4 + gap;
            var cov = double.IsNaN(g.CoverageRatio) ? 0 : g.CoverageRatio;
            var verdict = g.Passed ? "PASS" : "FAIL";
            var label = $"{g.Target.Name,-8}  cov {cov,5:P1}   {verdict}";
            c.DrawText(label, textX, yMid + 4, SKTextAlign.Left, textFont, textPaint);
        }
    }
}
