using ColorMeasure.Domain.Abstractions;
using SkiaSharp;

namespace ColorMeasure.Infrastructure.Reporting;

public interface IXyDiagramRenderer
{
    byte[] RenderPng(Domain.Entities.MeasurementRun run, int width, int height);
}

/// <summary>
/// Renders a CIE 1931 xy chromaticity diagram with the spectral locus, target gamut outlines,
/// measured primaries, and coverage labels. Backed by SkiaSharp so it works in any host.
/// </summary>
public sealed class XyDiagramRenderer : IXyDiagramRenderer
{
    /// <summary>Distinct palette assigned to gamuts in order. Measured version uses a darker shade.</summary>
    private static readonly SKColor[] GamutPalette =
    {
        new(31, 119, 180),    // blue       - sRGB
        new(44, 160, 44),     // green      - DCI-P3
        new(255, 127, 14),    // orange     - AdobeRGB
        new(214, 39, 40),     // red        - Rec.2020
        new(148, 103, 189),   // purple     - spare
        new(140, 86, 75),     // brown      - spare
        new(23, 190, 207),    // cyan       - spare
    };

    private static readonly (double X, double Y)[] SpectralLocus =
    {
        (0.1741, 0.0050),(0.1738, 0.0049),(0.1733, 0.0048),(0.1726, 0.0048),(0.1714, 0.0051),
        (0.1703, 0.0058),(0.1689, 0.0069),(0.1669, 0.0086),(0.1644, 0.0109),(0.1611, 0.0138),
        (0.1566, 0.0177),(0.1510, 0.0227),(0.1440, 0.0297),(0.1355, 0.0389),(0.1241, 0.0578),
        (0.1096, 0.0868),(0.0913, 0.1327),(0.0687, 0.2007),(0.0454, 0.2950),(0.0235, 0.4127),
        (0.0082, 0.5384),(0.0039, 0.6548),(0.0139, 0.7502),(0.0389, 0.8120),(0.0743, 0.8338),
        (0.1142, 0.8262),(0.1547, 0.8059),(0.1929, 0.7816),(0.2296, 0.7543),(0.2658, 0.7243),
        (0.3016, 0.6923),(0.3373, 0.6589),(0.3731, 0.6245),(0.4087, 0.5896),(0.4441, 0.5547),
        (0.4788, 0.5202),(0.5125, 0.4866),(0.5448, 0.4544),(0.5755, 0.4242),(0.6042, 0.3965),
    };

    public byte[] RenderPng(Domain.Entities.MeasurementRun run, int width, int height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888);
        using var surface = SKSurface.Create(info);
        var c = surface.Canvas;
        c.Clear(SKColors.White);

        const float left = 40, right = 40, top = 30, bottom = 50;
        var plot = SKRect.Create(left, top, width - left - right, height - top - bottom);

        using (var grid = new SKPaint { Color = new SKColor(230, 230, 230), StrokeWidth = 1, IsStroke = true, IsAntialias = true })
        {
            for (double x = 0; x <= 0.8; x += 0.1)
            {
                var px = (float)(left + (x - 0.0) / 0.8 * plot.Width);
                c.DrawLine(px, plot.Top, px, plot.Bottom, grid);
            }
            for (double y = 0; y <= 0.9; y += 0.1)
            {
                var py = (float)(top + (1 - (y - 0.0) / 0.9) * plot.Height);
                c.DrawLine(plot.Left, py, plot.Right, py, grid);
            }
        }

        using (var locus = new SKPaint { Color = new SKColor(180, 180, 180), IsStroke = true, StrokeWidth = 2, IsAntialias = true })
        {
            using var path = new SKPath();
            for (int i = 0; i < SpectralLocus.Length; i++)
            {
                var pt = ToPixel(SpectralLocus[i].Item1, SpectralLocus[i].Item2, plot);
                if (i == 0) path.MoveTo(pt); else path.LineTo(pt);
            }
            path.Close();
            c.DrawPath(path, locus);
        }

        // Pair every target with a measured triangle that uses the same hue but darker.
        var dashEffect = SKPathEffect.CreateDash(new float[] { 6, 4 }, 0);
        for (int idx = 0; idx < run.GamutReports.Count; idx++)
        {
            var g = run.GamutReports[idx];
            var color = GamutPalette[idx % GamutPalette.Length];
            var measuredColor = new SKColor(
                (byte)(color.Red * 7 / 10),
                (byte)(color.Green * 7 / 10),
                (byte)(color.Blue * 7 / 10));

            // Target triangle: solid stroke, light fill
            DrawGamut(c, plot,
                g.Target.Red, g.Target.Green, g.Target.Blue, g.Target.White,
                fill: new SKColor(color.Red, color.Green, color.Blue, 28),
                stroke: color,
                strokeWidth: 2.2f,
                dashed: false);

            // Measured triangle: dashed stroke, no fill
            DrawGamut(c, plot,
                g.MeasuredRed, g.MeasuredGreen, g.MeasuredBlue, g.MeasuredWhite,
                fill: SKColors.Transparent,
                stroke: measuredColor,
                strokeWidth: 1.8f,
                dashed: true,
                dashEffect: dashEffect);

            // White point dot (filled, target color)
            var wp = ToPixel(g.MeasuredWhite.X, g.MeasuredWhite.Y, plot);
            using (var dot = new SKPaint { Color = measuredColor, IsAntialias = true })
            {
                c.DrawCircle(wp, 3, dot);
            }
        }

        // Legend in the top-right of the plot area, one row per target with coverage.
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

    private static SKPoint ToPixel(double x, double y, SKRect plot)
    {
        const double maxX = 0.8, maxY = 0.9;
        var px = (float)(plot.Left + (x / maxX) * plot.Width);
        var py = (float)(plot.Top + (1 - y / maxY) * plot.Height);
        return new SKPoint(px, py);
    }

    private static void DrawGamut(SKCanvas c, SKRect plot,
        Domain.Entities.ChromaticityPoint r,
        Domain.Entities.ChromaticityPoint g,
        Domain.Entities.ChromaticityPoint b,
        Domain.Entities.ChromaticityPoint w,
        SKColor fill, SKColor stroke, float strokeWidth, bool dashed, SKPathEffect? dashEffect = null)
    {
        using var path = new SKPath();
        var rp = ToPixel(r.X, r.Y, plot);
        var gp = ToPixel(g.X, g.Y, plot);
        var bp = ToPixel(b.X, b.Y, plot);

        path.MoveTo(rp);
        path.LineTo(gp);
        path.LineTo(bp);
        path.Close();

        if (fill.Alpha > 0)
        {
            using var fillPaint = new SKPaint { Color = fill, IsAntialias = true };
            c.DrawPath(path, fillPaint);
        }

        var strokePaint = new SKPaint
        {
            Color = stroke,
            IsStroke = true,
            StrokeWidth = strokeWidth,
            IsAntialias = true,
        };
        if (dashed && dashEffect is not null) strokePaint.PathEffect = dashEffect;
        using (strokePaint)
        {
            c.DrawPath(path, strokePaint);
        }

        var wp = ToPixel(w.X, w.Y, plot);
        using (var dot = new SKPaint { Color = stroke, IsAntialias = true })
        {
            c.DrawCircle(wp, 2.5f, dot);
        }
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

            // Target swatch (solid filled square in target color)
            using (var targetFill = new SKPaint { Color = new SKColor(color.Red, color.Green, color.Blue, 90), IsAntialias = true })
            using (var targetStroke = new SKPaint { Color = color, IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true })
            {
                var r = SKRect.Create(blockX + padX, yMid - swatchSize / 2, swatchSize, swatchSize);
                c.DrawRect(r, targetFill);
                c.DrawRect(r, targetStroke);
            }
            // Measured swatch (dashed outlined square in measured color)
            using (var measuredStroke = new SKPaint
            {
                Color = measuredColor,
                IsStroke = true,
                StrokeWidth = 1.5f,
                IsAntialias = true,
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
