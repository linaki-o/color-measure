using ColorMeasure.Application.Analysis;
using ColorMeasure.Application.Plans;
using ColorMeasure.Domain;
using ColorMeasure.Domain.Entities;
using ColorMeasure.Infrastructure.Reporting;
using FluentAssertions;
using Xunit;

namespace ColorMeasure.Infrastructure.Tests;

public class HtmlReportSinkTests : IDisposable
{
    private readonly string _dir;

    public HtmlReportSinkTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "color-measure-html-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public async Task WritesSelfContainedInteractiveHtml()
    {
        var srgb = ColorSpaceCatalog.Srgb;
        var samples = new[]
        {
            new ColorSample("white", srgb.White, 100, 95.05, 100, 108.88, 0, 6504, "TEST-SN", DateTimeOffset.UtcNow),
            new ColorSample("red",   srgb.Red,   21.26, 41.24, 21.26, 1.93, 0, 6504, "TEST-SN", DateTimeOffset.UtcNow),
            new ColorSample("green", srgb.Green, 71.52, 38.74, 71.52, 13.20, 0, 6504, "TEST-SN", DateTimeOffset.UtcNow),
            new ColorSample("blue",  srgb.Blue,   7.22, 15.06, 7.22, 73.76, 0, 6504, "TEST-SN", DateTimeOffset.UtcNow),
        };
        var reports = ColorAnalyzer.Analyze(samples, new[] { srgb }, new PassCriteria(null, null)).Value!;
        var run = new MeasurementRun(
            PlanName: "Demo",
            StartedAt: DateTimeOffset.UtcNow,
            FinishedAt: DateTimeOffset.UtcNow,
            ProbeSerialNo: "TEST-SN",
            Samples: samples,
            GamutReports: reports);

        var sink = new HtmlReportSink();
        await sink.WriteAsync(run, _dir, CancellationToken.None);

        var path = Path.Combine(_dir, "report.html");
        File.Exists(path).Should().BeTrue();

        var html = await File.ReadAllTextAsync(path);
        html.Should().Contain("<svg id=\"diagram\"");
        html.Should().Contain("const RUN =");
        html.Should().Contain("\"name\":\"sRGB\"");
        html.Should().Contain("toggleGamut");
        html.Should().Contain("plantMarker");
        html.Should().Contain("showSampleDetail");
    }

    [Fact]
    public async Task HandlesEmptySamplesGracefully()
    {
        var run = new MeasurementRun(
            PlanName: "Empty",
            StartedAt: DateTimeOffset.UtcNow,
            FinishedAt: DateTimeOffset.UtcNow,
            ProbeSerialNo: "TEST-SN",
            Samples: Array.Empty<ColorSample>(),
            GamutReports: Array.Empty<GamutReport>());

        var sink = new HtmlReportSink();
        await sink.WriteAsync(run, _dir, CancellationToken.None);

        var html = await File.ReadAllTextAsync(Path.Combine(_dir, "report.html"));
        html.Should().Contain("Empty");
        html.Should().Contain("没有采集到任何样本");
    }
}
