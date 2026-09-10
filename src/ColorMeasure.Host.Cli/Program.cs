using System.CommandLine;
using System.IO;
using ColorMeasure.Application.Plans;
using ColorMeasure.Application.Workflow;
using ColorMeasure.Domain;
using ColorMeasure.Domain.Abstractions;
using ColorMeasure.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ColorMeasure.Host.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            var root = new RootCommand("ColorMeasure — automated display color QA via Konica Minolta CA-410")
            {
                Name = "color-measure",
            };

            var planArg = new Option<string>("--plan", "Path to test plan JSON") { IsRequired = true };
            var outOpt = new Option<string>("--out", () => Path.Combine(Environment.CurrentDirectory, "runs", DateTime.Now.ToString("yyyyMMdd-HHmmss")), "Output directory");
            var connectOpt = new Option<string>("--connect", () => "auto", "Connection mode: auto|usb");
            var dryRunOpt = new Option<bool>("--dry-run", "Parse plan only, do not connect hardware");
            root.Add(planArg);
            root.Add(outOpt);
            root.Add(connectOpt);
            root.Add(dryRunOpt);

            root.SetHandler(async (string plan, string outDir, string connect, bool dryRun) =>
            {
                await RunAsync(plan, outDir, connect, dryRun);
            }, planArg, outOpt, connectOpt, dryRunOpt);

            return await root.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unhandled exception");
            return 99;
        }
        finally { await Log.CloseAndFlushAsync(); }
    }

    private static async Task RunAsync(string planPath, string outDir, string connect, bool dryRun)
    {
        var parseResult = TestPlanParser.ParseFile(planPath);
        if (parseResult.IsFailure)
        {
            Log.Error("Failed to parse plan: {Error}", parseResult.Error?.Message);
            Environment.Exit(2);
            return;
        }
        var plan = parseResult.Value!;
        Log.Information("Plan: {Name}, {PatchCount} patches, targets=[{Targets}], settle={Settle}ms",
            plan.Name, plan.Patches.Count, string.Join(",", plan.Targets), plan.SettleMs);

        if (dryRun)
        {
            Log.Information("--dry-run: skipping measurement");
            return;
        }

        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(dispose: false);
        builder.Services.AddColorMeasureInfrastructure();
        builder.Services.AddSingleton<RunRunner>();

        using var host = builder.Build();
        var runner = host.Services.GetRequiredService<RunRunner>();

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var result = await runner.ExecuteAsync(plan, outDir, cts.Token);
        if (result.IsFailure)
        {
            var code = result.Error?.Code ?? "UNKNOWN";
            var hint = code switch
            {
                "SDK_EXCEPTION" or "SDK_READ" or "SDK_INFINITY" =>
                    "Check that the CA-410 is connected, the lens is uncovered, and the display is showing a solid color.",
                "MISSING_PRIMARIES" =>
                    "All 4 required primaries (red, green, blue, white) must be measured. See warnings above for which patches were skipped.",
                "NO_SAMPLES" =>
                    "Every measurement returned an error. The probe/display setup is likely wrong.",
                _ => null,
            };
            Log.Error("Run failed [{Code}]: {Error}", code, result.Error?.Message);
            if (hint is not null) Log.Information("Hint: {Hint}", hint);
            Environment.Exit(1);
            return;
        }

        var run = result.Value!.Run;
        Log.Information("Done. Plan={Plan} Samples={Samples} Reports at: {Dir}",
            run.PlanName, run.Samples.Count, outDir);

        foreach (var g in run.GamutReports)
        {
            Log.Information("  {Target}: coverage={Cov:P1}, max dE2000={De:F2}, dUv(W)={Duv:F4}, {Result}",
                g.Target.Name, g.CoverageRatio, g.MaxPrimaryDeltaE2000, g.WhiteDeltaUv,
                g.Passed ? "PASS" : "FAIL");
        }
    }

    private sealed class RunRunner(
        IMeasurementWorkflow workflow,
        IReportSink reports,
        ILogger<RunRunner> log)
    {
        public async Task<Result<WorkflowResult>> ExecuteAsync(TestPlan plan, string outDir, CancellationToken ct)
        {
            Directory.CreateDirectory(outDir);
            var progress = new Progress<WorkflowProgress>(p =>
                log.LogInformation("  [{Step}/{Total}] {Stage} {PatchId}", p.Step, p.Total, p.Stage, p.PatchId));
            var run = await workflow.RunAsync(plan, outDir, progress, ct);
            if (run.IsSuccess)
            {
                await reports.WriteAsync(run.Value!.Run, outDir, ct);
            }
            return run;
        }
    }
}
