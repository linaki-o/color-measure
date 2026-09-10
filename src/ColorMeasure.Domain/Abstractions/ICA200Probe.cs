using ColorMeasure.Domain.Entities;

namespace ColorMeasure.Domain.Abstractions;

/// <summary>
/// Thin abstraction over the CA-410 instrument. All blocking SDK calls go through here.
/// Implementations must be safe to call from a single dedicated worker; concurrent callers
/// are serialized internally.
/// </summary>
public interface ICA200Probe
{
    Task<Result<IReadOnlyList<DeviceInfo>>> DiscoverAsync(CancellationToken ct);

    Task<Result<Unit>> ConnectAsync(ConnectionProfile profile, CancellationToken ct);

    Task<Result<Unit>> ZeroCalAsync(CancellationToken ct);

    Task<Result<ColorSample>> MeasureAsync(MeasureOptions options, CancellationToken ct);

    Task<Result<ProbeInfo>> GetInfoAsync(CancellationToken ct);

    Task<Result<Unit>> DisconnectAsync(CancellationToken ct);
}

public sealed record DeviceInfo(string SerialNo, string ModelName, string ConnectionString);

public sealed record ConnectionProfile(string Mode, string? Target, int PortNo);

public sealed record MeasureOptions(
    string PatchId,
    int AverageCount = 8,
    string DisplayMode = "Lvxy",
    string SyncMode = "UNIV",
    double SyncFrequency = 60.0);

public sealed record ProbeInfo(string SerialNo, string ModelName, string FirmwareVersion);

public readonly record struct Unit;
