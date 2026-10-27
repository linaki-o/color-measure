using System.Text;
using ColorMeasure.Application.Analysis;
using ColorMeasure.Application.Plans;
using ColorMeasure.Domain;
using ColorMeasure.Domain.Entities;
using ColorMeasure.Infrastructure.Reporting;
using FluentAssertions;
using Xunit;

namespace ColorMeasure.Infrastructure.Tests;

public class CsvReportSinkTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cm-csv-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public async Task NonFiniteValuesAreWrittenAsEmptyCells_NotInfinity()
    {
        // duv and Tcp are undefined for fully saturated primaries — the SDK reports
        // Infinity. Writing the literal "Infinity" breaks Excel/pandas import.
        var srgb = ColorSpaceCatalog.Srgb;
        var samples = new[]
        {
            new ColorSample("white", srgb.White, 100, 95.05, 100, 108.88, 0.0091, 7246.2, "SN", DateTimeOffset.UtcNow),
            new ColorSample("red",   srgb.Red,   67.35, 116.38, 67.35, 8.89, double.PositiveInfinity, double.PositiveInfinity, "SN", DateTimeOffset.UtcNow),
            new ColorSample("green", srgb.Green, 182.04, 91.75, 182.04, 35.27, 0.0977, 6136.2, "SN", DateTimeOffset.UtcNow),
            new ColorSample("blue",  srgb.Blue,  42.92, 104.73, 42.92, 506.08, double.NaN, double.NegativeInfinity, "SN", DateTimeOffset.UtcNow),
        };
        var gamuts = ColorAnalyzer.Analyze(samples, new[] { srgb }, new PassCriteria(null, null)).Value!;
        var run = new MeasurementRun("t", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "SN",
            samples, gamuts);

        await new CsvReportSink().WriteAsync(run, _dir, CancellationToken.None);

        var csv = File.ReadAllText(Path.Combine(_dir, "samples.csv"), Encoding.UTF8);
        csv.Should().NotContain("Infinity");
        csv.Should().NotContain("NaN");
        // The red row should have empty duv/Tcp cells but keep its other values.
        var redRow = csv.Split('\n').First(l => l.StartsWith("red,"));
        redRow.Should().Contain("0.64");
        var cells = redRow.Split(',');
        cells[7].Should().BeEmpty();      // duv
        cells[8].Should().BeEmpty();      // Tcp
        cells[3].Should().Be("67.35");    // Lv preserved
    }
}
