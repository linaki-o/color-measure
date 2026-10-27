using ColorMeasure.Domain.Entities;

namespace ColorMeasure.Application.Plans;

public sealed record TestPlan(
    string Name,
    string ConnectMode,
    int CalibrateBeforeStart,
    int SettleMs,
    IReadOnlyList<PatchEntry> Patches,
    IReadOnlyList<string> Targets,
    PassCriteria Pass,
    DisplayState DisplayState = DisplayState.Unknown);

public sealed record PatchEntry(string Id, string Hex, int Avg, string? Label);

public sealed record PassCriteria(double? MaxDeltaE2000, double? MinCoverage);
