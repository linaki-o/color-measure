using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorMeasure.Host.Wpf.Models;

/// <summary>
/// Editable mirror of a recipe (test plan) JSON file. The WPF UI works against
/// this type — the Application layer's <c>TestPlan</c> / <c>TestPlanParser</c>
/// remains the source of truth for the CLI runtime. They share the same JSON
/// schema (defined inline in <see cref="RecipeSerializer"/>) so a recipe edited
/// here round-trips through the CLI without any transformation.
/// </summary>
public sealed class Recipe
{
    public string Name { get; set; } = "New Recipe";
    public string Connect { get; set; } = "auto";
    public bool CalibrateBeforeStart { get; set; } = true;
    public int SettleMs { get; set; } = 1500;
    public List<string> Targets { get; set; } = new() { "sRGB", "DCI-P3", "AdobeRGB", "Rec.2020" };
    public List<RecipePatch> Patches { get; set; } = new();
    public double? MaxDeltaE2000 { get; set; } = 5.0;
    public double? MinCoverage { get; set; } = 0.90;
}

public sealed class RecipePatch
{
    public string Id { get; set; } = "";
    public string Hex { get; set; } = "#FFFFFF";
    public int Avg { get; set; } = 8;
    public string? Label { get; set; }

    public RecipePatch Clone() => new() { Id = Id, Hex = Hex, Avg = Avg, Label = Label };
}
