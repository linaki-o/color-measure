using System.IO;
using System.Globalization;
using ColorMeasure.Domain.Abstractions;
using ColorMeasure.Domain.Entities;
using CsvHelper;

namespace ColorMeasure.Infrastructure.Reporting;

public sealed class CsvReportSink : IReportSink
{
    public async Task WriteAsync(MeasurementRun run, string outputDirectory, CancellationToken ct)
    {
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, "samples.csv");
        await using var writer = new StreamWriter(path);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteField("patch_id");
        csv.WriteField("x");
        csv.WriteField("y");
        csv.WriteField("Lv_cd_m2");
        csv.WriteField("X");
        csv.WriteField("Y");
        csv.WriteField("Z");
        csv.WriteField("duv");
        csv.WriteField("Tcp_K");
        csv.WriteField("probe_serial");
        csv.WriteField("measured_at_utc");
        await csv.NextRecordAsync();

        foreach (var s in run.Samples)
        {
            csv.WriteField(s.PatchId);
            csv.WriteField(s.Chromaticity.X);
            csv.WriteField(s.Chromaticity.Y);
            csv.WriteField(s.Lv);
            csv.WriteField(s.X);
            csv.WriteField(s.Y);
            csv.WriteField(s.Z);
            csv.WriteField(s.DuV);
            csv.WriteField(s.Tcp);
            csv.WriteField(s.ProbeSerialNo);
            csv.WriteField(s.MeasuredAt.UtcDateTime.ToString("o"));
            await csv.NextRecordAsync();
        }

        await csv.WriteRecordsAsync(run.GamutReports.SelectMany(g => g.PrimaryDeltaEs.Select(d => new
        {
            target = g.Target.Name,
            channel = d.Channel,
            dE2000 = d.DeltaE2000,
            coverage = g.CoverageRatio,
            white_duv = g.WhiteDeltaUv,
            passed = g.Passed,
        })));
    }
}
