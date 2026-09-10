using ColorMeasure.Domain.Entities;

namespace ColorMeasure.Domain.Abstractions;

/// <summary>
/// Persists the artifacts of a measurement run. Implementations may emit CSV / XLSX / PDF / HTML.
/// </summary>
public interface IReportSink
{
    Task WriteAsync(MeasurementRun run, string outputDirectory, CancellationToken ct);
}
