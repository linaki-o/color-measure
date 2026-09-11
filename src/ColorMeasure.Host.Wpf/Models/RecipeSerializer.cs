using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorMeasure.Host.Wpf.Models;

/// <summary>
/// Read / write <see cref="Recipe"/> to the same JSON schema the CLI
/// (<c>TestPlanParser</c>) uses. This is the single contract between the GUI
/// editor and the CLI runtime — a recipe edited here round-trips into the
/// workflow without any code change in <c>ColorMeasure.Application</c>.
/// </summary>
public static class RecipeSerializer
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string Serialize(Recipe r) => JsonSerializer.Serialize(ToNode(r), Opts);

    public static Recipe Deserialize(string json)
    {
        var node = JsonNode.Parse(json)!.AsObject();
        return FromNode(node);
    }

    public static Recipe LoadFromFile(string path) => Deserialize(File.ReadAllText(path));

    public static void SaveToFile(Recipe r, string path) => File.WriteAllText(path, Serialize(r));

    // -- mapping between Recipe and the JSON tree --

    private static JsonObject ToNode(Recipe r)
    {
        var obj = new JsonObject
        {
            ["name"] = r.Name,
            ["connect"] = r.Connect,
            ["calibrateBeforeStart"] = r.CalibrateBeforeStart,
            ["settleMs"] = r.SettleMs,
            ["targets"] = new JsonArray(r.Targets.Select(t => JsonValue.Create(t)).ToArray()),
            ["patches"] = new JsonArray(r.Patches.Select(p => new JsonObject
            {
                ["id"] = p.Id,
                ["hex"] = p.Hex,
                ["avg"] = p.Avg,
                ["label"] = p.Label,
            }).ToArray()),
        };
        if (r.MaxDeltaE2000 > 0 || r.MinCoverage > 0)
        {
            var pass = new JsonObject();
            if (r.MaxDeltaE2000 > 0) pass["max_delta_e2000"] = r.MaxDeltaE2000!.Value;
            if (r.MinCoverage > 0) pass["min_coverage"] = r.MinCoverage!.Value;
            obj["pass"] = pass;
        }
        return obj;
    }

    private static Recipe FromNode(JsonObject obj)
    {
        var r = new Recipe
        {
            Name = obj["name"]?.GetValue<string>() ?? "Unnamed",
            Connect = obj["connect"]?.GetValue<string>() ?? "auto",
            CalibrateBeforeStart = obj["calibrateBeforeStart"]?.GetValue<bool>() ?? true,
            SettleMs = obj["settleMs"]?.GetValue<int>() ?? 1500,
        };
        if (obj["targets"] is JsonArray tg)
        {
            r.Targets = tg.Select(n => n!.GetValue<string>()).ToList();
        }
        if (obj["patches"] is JsonArray pa)
        {
            foreach (var p in pa)
            {
                if (p is not JsonObject po) continue;
                r.Patches.Add(new RecipePatch
                {
                    Id = po["id"]?.GetValue<string>() ?? "",
                    Hex = po["hex"]?.GetValue<string>() ?? "#FFFFFF",
                    Avg = po["avg"]?.GetValue<int>() ?? 8,
                    Label = po["label"]?.GetValue<string?>(),
                });
            }
        }
        if (obj["pass"] is JsonObject ps)
        {
            r.MaxDeltaE2000 = ps["max_delta_e2000"]?.GetValue<double?>();
            r.MinCoverage = ps["min_coverage"]?.GetValue<double?>();
        }
        return r;
    }
}
