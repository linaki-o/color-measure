using System.IO;
using ColorMeasure.Domain.Abstractions;
using ColorMeasure.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ColorMeasure.Infrastructure.Reporting;

public sealed class PdfReportSink(IXyDiagramRenderer diagram) : IReportSink
{
    public Task WriteAsync(MeasurementRun run, string outputDirectory, CancellationToken ct)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var path = Path.Combine(outputDirectory, "report.pdf");
        var png = diagram.RenderPng(run, 1024, 768);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text(run.PlanName).FontSize(22).Bold();
                    col.Item().Text($"Probe: {run.ProbeSerialNo}    Started: {run.StartedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss}").FontSize(9).FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Image(png);

                    col.Item().Text("Gamut analysis").FontSize(14).Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Text("Target").Bold();
                            h.Cell().Text("Coverage").Bold();
                            h.Cell().Text("dUv (W)").Bold();
                            h.Cell().Text("Max dE2000").Bold();
                            h.Cell().Text("Result").Bold();
                        });
                        foreach (var g in run.GamutReports)
                        {
                            table.Cell().Text(g.Target.Name);
                            table.Cell().Text(g.CoverageRatio.ToString("P1"));
                            table.Cell().Text(g.WhiteDeltaUv.ToString("F4"));
                            table.Cell().Text(g.MaxPrimaryDeltaE2000.ToString("F2"));
                            table.Cell().Text(g.Passed ? "PASS" : "FAIL").FontColor(g.Passed ? Colors.Green.Darken2 : Colors.Red.Darken2).Bold();
                        }
                    });

                    col.Item().Text("Samples").FontSize(14).Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Text("Patch").Bold();
                            h.Cell().Text("x").Bold();
                            h.Cell().Text("y").Bold();
                            h.Cell().Text("Lv (cd/m^2)").Bold();
                            h.Cell().Text("Tcp (K)").Bold();
                        });
                        foreach (var s in run.Samples)
                        {
                            table.Cell().Text(s.PatchId);
                            table.Cell().Text(Fmt(s.Chromaticity.X));
                            table.Cell().Text(Fmt(s.Chromaticity.Y));
                            table.Cell().Text(Fmt(s.Lv, "F2"));
                            table.Cell().Text(Fmt(s.Tcp, "F0"));
                        }
                    });
                });
            });
        }).GeneratePdf(path);
        return Task.CompletedTask;
    }

    private static string Fmt(double v, string fmt = "F4") =>
        double.IsNaN(v) || double.IsInfinity(v) ? "—" : v.ToString(fmt);
}
