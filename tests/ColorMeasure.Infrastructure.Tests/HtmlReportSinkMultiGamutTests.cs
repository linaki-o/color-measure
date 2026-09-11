using ColorMeasure.Application.Analysis;
using ColorMeasure.Application.Plans;
using ColorMeasure.Domain;
using ColorMeasure.Domain.Entities;
using ColorMeasure.Infrastructure.Reporting;
using FluentAssertions;
using Xunit;

namespace ColorMeasure.Infrastructure.Tests;

public class HtmlReportSinkMultiGamutTests
{
    [Fact]
    public async Task RendersAllGamutReportsInJson()
    {
        // Regression: the workflow emits one GamutReport per target color space
        // (sRGB / DCI-P3 / AdobeRGB / Rec.2020). The HTML JSON must carry ALL of
        // them — earlier transient states (where the Select was silently dropping
        // entries) would render only the first one in the diagram toggles.
        var srgb = ColorSpaceCatalog.Srgb;
        var p3 = ColorSpaceCatalog.DciP3;
        var adobe = ColorSpaceCatalog.AdobeRgb;
        var rec2020 = ColorSpaceCatalog.Rec2020;

        var samples = new[]
        {
            new ColorSample("white", srgb.White, 100, 95.05, 100, 108.88, 0, 6504, "TEST-SN", DateTimeOffset.UtcNow),
            new ColorSample("red",   srgb.Red,   21.26, 41.24, 21.26, 1.93, 0, 6504, "TEST-SN", DateTimeOffset.UtcNow),
            new ColorSample("green", srgb.Green, 71.52, 38.74, 71.52, 13.20, 0, 6504, "TEST-SN", DateTimeOffset.UtcNow),
            new ColorSample("blue",  srgb.Blue,   7.22, 15.06, 7.22, 73.76, 0, 6504, "TEST-SN", DateTimeOffset.UtcNow),
        };
        var gamutReports = ColorAnalyzer.Analyze(samples, new[] { srgb, p3, adobe, rec2020 }, new PassCriteria(null, null)).Value!;
        var run = new MeasurementRun(
            "multi-gamut", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "TEST-SN",
            samples, gamutReports);

        var sink = new HtmlReportSink();
        var dir = Path.Combine(Path.GetTempPath(), "cm-html-multi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await sink.WriteAsync(run, dir, CancellationToken.None);
            var html = await File.ReadAllTextAsync(Path.Combine(dir, "report.html"));

            // The RUN JSON must contain all four gamut names.
            html.Should().Contain("\"name\":\"sRGB\"");
            html.Should().Contain("\"name\":\"DCI-P3\"");
            html.Should().Contain("\"name\":\"AdobeRGB\"");
            html.Should().Contain("\"name\":\"Rec.2020\"");

            // And the JS toggle builder must run once per gamut (so the user gets
            // 4 toolbar buttons, not 1).
            for (int i = 0; i < 4; i++) html.Should().Contain("buildToolbar");

            // Per-gamut legend chip builder must also iterate over all 4.
            html.Should().Contain("buildGamutLegend");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
