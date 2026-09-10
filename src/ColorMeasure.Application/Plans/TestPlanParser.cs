using System.Text.Json;
using System.Text.Json.Serialization;
using ColorMeasure.Domain;
using ColorMeasure.Domain.Entities;

namespace ColorMeasure.Application.Plans;

/// <summary>
/// Parses a JSON test plan file into a <see cref="TestPlan"/>.
/// The schema is intentionally tiny for v0 (color-space coverage).
/// </summary>
public static class TestPlanParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static Result<TestPlan> ParseFile(string path)
    {
        if (!File.Exists(path)) return Result<TestPlan>.Fail(new Error("PLAN_NOT_FOUND", $"Plan file not found: {path}"));
        try
        {
            var dto = JsonSerializer.Deserialize<PlanDto>(File.ReadAllText(path), Options);
            if (dto is null) return Result<TestPlan>.Fail(Error.InvalidPlan);
            return FromDto(dto);
        }
        catch (JsonException ex)
        {
            return Result<TestPlan>.Fail(new Error("PLAN_PARSE", ex.Message));
        }
    }

    public static Result<TestPlan> FromDto(PlanDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<TestPlan>.Fail(new Error("INVALID_PLAN_NAME", "Plan 'name' is required"));
        if (dto.Patches is null || dto.Patches.Count == 0)
            return Result<TestPlan>.Fail(new Error("INVALID_PLAN_PATCHES", "Plan must define at least one patch"));

        var patches = new List<PatchEntry>(dto.Patches.Count);
        foreach (var p in dto.Patches)
        {
            if (string.IsNullOrWhiteSpace(p.Id) || string.IsNullOrWhiteSpace(p.Hex))
                return Result<TestPlan>.Fail(new Error("INVALID_PATCH", "Patch 'id' and 'hex' are required"));
            try { _ = PatchSpec.FromHex(p.Id, p.Hex, p.Label); }
            catch (ArgumentException ex) { return Result<TestPlan>.Fail(new Error("INVALID_PATCH_HEX", ex.Message)); }

            patches.Add(new PatchEntry(p.Id, p.Hex, p.Avg <= 0 ? 8 : p.Avg, p.Label));
        }

        var targets = dto.Targets is { Count: > 0 } ? dto.Targets : new List<string> { "sRGB" };
        foreach (var t in targets)
        {
            if (ColorSpaceCatalog.Find(t) is null)
                return Result<TestPlan>.Fail(new Error("UNKNOWN_TARGET", $"Unknown color space target: {t}"));
        }

        var pass = dto.Pass is null
            ? new PassCriteria(null, null)
            : new PassCriteria(dto.Pass.MaxDeltaE2000, dto.Pass.MinCoverage);

        return Result<TestPlan>.Ok(new TestPlan(
            Name: dto.Name,
            ConnectMode: string.IsNullOrWhiteSpace(dto.Connect) ? "auto" : dto.Connect!,
            CalibrateBeforeStart: dto.CalibrateBeforeStart ? 1 : 0,
            SettleMs: dto.SettleMs <= 0 ? 1500 : dto.SettleMs,
            Patches: patches,
            Targets: targets,
            Pass: pass));
    }

    public sealed class PlanDto
    {
        public string? Name { get; set; }
        public string? Connect { get; set; }
        public bool CalibrateBeforeStart { get; set; }
        public int SettleMs { get; set; }
        public List<PatchDto>? Patches { get; set; }
        public List<string>? Targets { get; set; }
        public PassDto? Pass { get; set; }
    }

    public sealed class PatchDto
    {
        public string? Id { get; set; }
        public string? Hex { get; set; }
        public int Avg { get; set; }
        public string? Label { get; set; }
    }

    public sealed class PassDto
    {
        [JsonPropertyName("max_delta_e2000")]
        public double? MaxDeltaE2000 { get; set; }

        [JsonPropertyName("min_coverage")]
        public double? MinCoverage { get; set; }
    }
}
