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
    /// <summary>
    /// NOTE: do NOT add a global <c>PropertyNamingPolicy</c> here.
    /// The recipe schema is camelCase at the top level ("calibrateBeforeStart",
    /// "settleMs", "displayState") and snake_case only inside the "pass" block
    /// ("max_delta_e2000", "min_coverage", which carry explicit JsonPropertyName
    /// attributes). A global SnakeCaseLower policy rewrites the *expected* key of
    /// every camelCase property to snake_case, so those keys silently fail to match
    /// and deserialise as their default — which previously meant
    /// "calibrateBeforeStart": true never enabled ZeroCal and "settleMs" was
    /// always ignored. Every key is now spelled out explicitly instead.
    /// </summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
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

        var displayState = ParseDisplayState(dto.DisplayState);

        return Result<TestPlan>.Ok(new TestPlan(
            Name: dto.Name,
            ConnectMode: string.IsNullOrWhiteSpace(dto.Connect) ? "auto" : dto.Connect!,
            CalibrateBeforeStart: dto.CalibrateBeforeStart ? 1 : 0,
            SettleMs: dto.SettleMs <= 0 ? 1500 : dto.SettleMs,
            Patches: patches,
            Targets: targets,
            Pass: pass,
            DisplayState: displayState));
    }

    /// <summary>
    /// Parses the optional "displayState" field: "sdr" | "hdr" | "unknown".
    /// Unknown / unrecognised values degrade to <see cref="DisplayState.Unknown"/>
    /// rather than failing the plan — a stale recipe should still run.
    /// </summary>
    private static DisplayState ParseDisplayState(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return DisplayState.Unknown;
        return raw.Trim().ToLowerInvariant() switch
        {
            "sdr" => DisplayState.Sdr,
            "hdr" => DisplayState.Hdr,
            _ => DisplayState.Unknown,
        };
    }

    public sealed class PlanDto
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("connect")] public string? Connect { get; set; }
        [JsonPropertyName("calibrateBeforeStart")] public bool CalibrateBeforeStart { get; set; }
        [JsonPropertyName("settleMs")] public int SettleMs { get; set; }
        [JsonPropertyName("patches")] public List<PatchDto>? Patches { get; set; }
        [JsonPropertyName("targets")] public List<string>? Targets { get; set; }
        [JsonPropertyName("pass")] public PassDto? Pass { get; set; }

        /// <summary>"sdr" | "hdr" | "unknown" — what the operator declares about the display state.</summary>
        [JsonPropertyName("displayState")] public string? DisplayState { get; set; }
    }

    public sealed class PatchDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("hex")] public string? Hex { get; set; }
        [JsonPropertyName("avg")] public int Avg { get; set; }
        [JsonPropertyName("label")] public string? Label { get; set; }
    }

    public sealed class PassDto
    {
        [JsonPropertyName("max_delta_e2000")]
        public double? MaxDeltaE2000 { get; set; }

        [JsonPropertyName("min_coverage")]
        public double? MinCoverage { get; set; }
    }
}
