using ColorMeasure.Domain.Entities;

namespace ColorMeasure.Domain.Abstractions;

/// <summary>
/// Renders the test patch on the device under test. The driver is intentionally decoupled
/// from the measurement probe: the workflow calls <see cref="ShowAsync"/> before measuring
/// and <see cref="HideAsync"/> afterwards.
/// </summary>
public interface IDisplayDriver : IAsyncDisposable
{
    Task ShowAsync(PatchSpec patch, TimeSpan settle, CancellationToken ct);
    Task HideAsync(CancellationToken ct);
}
