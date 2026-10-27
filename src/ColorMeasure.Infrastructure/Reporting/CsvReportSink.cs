using System.IO;
using System.Globalization;
using ColorMeasure.Domain.Abstractions;
using ColorMeasure.Domain.Entities;
using CsvHelper;

namespace ColorMeasure.Infrastructure.Reporting;

public sealed class CsvReportSink : IReportSink
{
    /// <summary>
    /// Formats a double for CSV. Non-finite values become an empty cell rather than
    /// "Infinity"/"NaN" — those break Excel/pandas import, and they legitimately occur
    /// (duv and Tcp are undefined for fully saturated primaries).
    /// </summary>
    private static string Num(double v) =>
        double.IsFinite(v) ? v.ToString("R", CultureInfo.InvariantCulture) : "";

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
            csv.WriteField(Num(s.Chromaticity.X));
            csv.WriteField(Num(s.Chromaticity.Y));
            csv.WriteField(Num(s.Lv));
            csv.WriteField(Num(s.X));
            csv.WriteField(Num(s.Y));
            csv.WriteField(Num(s.Z));
            csv.WriteField(Num(s.DuV));
            csv.WriteField(Num(s.Tcp));
            csv.WriteField(s.ProbeSerialNo);
            csv.WriteField(s.MeasuredAt.UtcDateTime.ToString("o"));
            await csv.NextRecordAsync();
        }

        await csv.WriteRecordsAsync(run.GamutReports.SelectMany(g => g.PrimaryDeltaEs.Select(d => new
        {
            target = g.Target.Name,
            channel = d.Channel,
            dE2000 = Num(d.DeltaE2000),
            coverage = Num(g.CoverageRatio),
            white_duv = Num(g.WhiteDeltaUv),
            passed = g.Passed,
        })));

        if (run.Gamma is { } gm)
        {
            await csv.WriteRecordsAsync(new[]
            {
                new
                {
                    target = "_gamma",
                    channel = "luminance",
                    dE2000 = (double?)null,
                    coverage = (double?)null,
                    white_duv = (double?)null,
                    passed = false,
                }
            });
            await csv.NextRecordAsync();
            csv.WriteField("gamma");
            csv.WriteField(gm.Gamma.ToString("F4"));
            csv.WriteField("");
            csv.WriteField(gm.ScaleA.ToString("F4"));
            csv.WriteField(gm.RSquared.ToString("F4"));
            csv.WriteField(gm.BlackLevelCdM2.ToString("F4"));
            csv.WriteField("");
            csv.WriteField("");
            await csv.NextRecordAsync();
        }

        if (run.LinkHealth is { } lh)
        {
            // Header row for the link-health block, then one row per signal.
            csv.WriteField("_link_health");
            csv.WriteField("verdict");
            csv.WriteField(lh.Verdict.ToString());
            csv.WriteField("declared");
            csv.WriteField(lh.DeclaredState.ToString());
            csv.WriteField("");
            csv.WriteField("");
            csv.WriteField("");
            await csv.NextRecordAsync();

            foreach (var s in lh.Signals)
            {
                csv.WriteField("_link_health");
                csv.WriteField(s.Id);
                csv.WriteField(s.Severity.ToString());
                csv.WriteField(s.Value.ToString("F6"));
                csv.WriteField(s.Threshold.ToString("F6"));
                csv.WriteField(s.Title);
                csv.WriteField(s.Detail);
                await csv.NextRecordAsync();
            }
        }
    }
}
