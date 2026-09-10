using ColorMeasure.Domain;
using ColorMeasure.Domain.Abstractions;
using ColorMeasure.Domain.Entities;
using ColorMeasure.Application.Analysis;
using ColorMeasure.Application.Plans;
using Microsoft.Extensions.Logging;

namespace ColorMeasure.Application.Workflow;

public sealed record WorkflowProgress(int Step, int Total, string PatchId, string Stage);

public sealed record WorkflowResult(MeasurementRun Run);

public interface IMeasurementWorkflow
{
    Task<Result<WorkflowResult>> RunAsync(TestPlan plan, string outputDirectory, IProgress<WorkflowProgress>? progress, CancellationToken ct);
}

public sealed class MeasurementWorkflow(
    ICA200Probe probe,
    IDisplayDriver display,
    ILogger<MeasurementWorkflow> log) : IMeasurementWorkflow
{
    public async Task<Result<WorkflowResult>> RunAsync(
        TestPlan plan,
        string outputDirectory,
        IProgress<WorkflowProgress>? progress,
        CancellationToken ct)
    {
        Directory.CreateDirectory(outputDirectory);

        var connectRes = await probe.ConnectAsync(new ConnectionProfile(plan.ConnectMode, null, 0), ct);
        if (connectRes.IsFailure) return Result<WorkflowResult>.Fail(connectRes.Error!);

        var infoRes = await probe.GetInfoAsync(ct);
        var probeInfo = infoRes.IsSuccess ? infoRes.Value! : new ProbeInfo("unknown", "unknown", "unknown");

        if (plan.CalibrateBeforeStart == 1)
        {
            progress?.Report(new WorkflowProgress(0, plan.Patches.Count + 1, "-", "ZeroCal"));
            var cal = await probe.ZeroCalAsync(ct);
            if (cal.IsFailure) return Result<WorkflowResult>.Fail(cal.Error!);
        }

        var samples = new List<ColorSample>(plan.Patches.Count);
        int step = 0;
        foreach (var patch in plan.Patches)
        {
            ct.ThrowIfCancellationRequested();
            step++;
            progress?.Report(new WorkflowProgress(step, plan.Patches.Count, patch.Id, "Show"));
            var ps = PatchSpec.FromHex(patch.Id, patch.Hex, patch.Label);
            await display.ShowAsync(ps, TimeSpan.FromMilliseconds(plan.SettleMs), ct);

            progress?.Report(new WorkflowProgress(step, plan.Patches.Count, patch.Id, "Measure"));
            var measRes = await probe.MeasureAsync(new MeasureOptions(patch.Id, patch.Avg), ct);
            if (measRes.IsFailure)
            {
                log.LogWarning("Skipping patch {PatchId}: {Error}", patch.Id, measRes.Error?.Message);
                continue;
            }
            samples.Add(measRes.Value!);
        }

        progress?.Report(new WorkflowProgress(plan.Patches.Count, plan.Patches.Count, "-", "Analyze"));
        var targets = plan.Targets
            .Select(t => ColorSpaceCatalog.Find(t)!)
            .ToList();
        var analysisResult = ColorAnalyzer.Analyze(samples, targets, plan.Pass);
        if (analysisResult.IsFailure)
            return Result<WorkflowResult>.Fail(analysisResult.Error!);
        var reports = analysisResult.Value!;

        await display.HideAsync(ct);

        var run = new MeasurementRun(
            PlanName: plan.Name,
            StartedAt: DateTimeOffset.UtcNow,
            FinishedAt: DateTimeOffset.UtcNow,
            ProbeSerialNo: probeInfo.SerialNo,
            Samples: samples,
            GamutReports: reports);

        return Result<WorkflowResult>.Ok(new WorkflowResult(run));
    }
}
