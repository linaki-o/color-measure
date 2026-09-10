using System.IO;
using ClosedXML.Excel;
using ColorMeasure.Domain.Abstractions;
using ColorMeasure.Domain.Entities;

namespace ColorMeasure.Infrastructure.Reporting;

public sealed class XlsxReportSink : IReportSink
{
    public Task WriteAsync(MeasurementRun run, string outputDirectory, CancellationToken ct)
    {
        var path = Path.Combine(outputDirectory, "summary.xlsx");
        using var wb = new XLWorkbook();
        var samples = wb.AddWorksheet("samples");
        samples.Cell(1, 1).Value = "patch_id";
        samples.Cell(1, 2).Value = "x";
        samples.Cell(1, 3).Value = "y";
        samples.Cell(1, 4).Value = "Lv";
        samples.Cell(1, 5).Value = "Tcp";
        samples.Cell(1, 6).Value = "duv";
        for (int i = 0; i < run.Samples.Count; i++)
        {
            var s = run.Samples[i];
            samples.Cell(i + 2, 1).Value = s.PatchId;
            WriteNumber(samples.Cell(i + 2, 2), s.Chromaticity.X);
            WriteNumber(samples.Cell(i + 2, 3), s.Chromaticity.Y);
            WriteNumber(samples.Cell(i + 2, 4), s.Lv);
            WriteNumber(samples.Cell(i + 2, 5), s.Tcp);
            WriteNumber(samples.Cell(i + 2, 6), s.DuV);
        }

        var gamut = wb.AddWorksheet("gamut");
        gamut.Cell(1, 1).Value = "target";
        gamut.Cell(1, 2).Value = "coverage";
        gamut.Cell(1, 3).Value = "white_dE_uv";
        gamut.Cell(1, 4).Value = "max_primary_dE2000";
        gamut.Cell(1, 5).Value = "passed";
        for (int i = 0; i < run.GamutReports.Count; i++)
        {
            var g = run.GamutReports[i];
            gamut.Cell(i + 2, 1).Value = g.Target.Name;
            WriteNumber(gamut.Cell(i + 2, 2), g.CoverageRatio);
            WriteNumber(gamut.Cell(i + 2, 3), g.WhiteDeltaUv);
            WriteNumber(gamut.Cell(i + 2, 4), g.MaxPrimaryDeltaE2000);
            gamut.Cell(i + 2, 5).Value = g.Passed ? "PASS" : "FAIL";
        }

        wb.SaveAs(path);
        return Task.CompletedTask;
    }

    private static void WriteNumber(IXLCell cell, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            cell.Value = string.Empty;
        else
            cell.Value = value;
    }
}
