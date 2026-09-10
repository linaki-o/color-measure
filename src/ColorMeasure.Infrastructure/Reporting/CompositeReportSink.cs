using ColorMeasure.Domain.Abstractions;
using ColorMeasure.Domain.Entities;

namespace ColorMeasure.Infrastructure.Reporting;

/// <summary>
/// Fans a measurement run out to multiple sinks (CSV + XLSX + PDF by default).
/// </summary>
public sealed class CompositeReportSink(IEnumerable<IReportSink> sinks) : IReportSink
{
    public async Task WriteAsync(MeasurementRun run, string outputDirectory, CancellationToken ct)
    {
        foreach (var sink in sinks) await sink.WriteAsync(run, outputDirectory, ct);
    }
}
