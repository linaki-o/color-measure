using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using ColorMeasure.Host.Wpf.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace ColorMeasure.Host.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private Recipe _recipe = new();
    private string? _filePath;
    private bool _isDirty;

    public string WindowTitle =>
        string.IsNullOrEmpty(_filePath)
            ? $"ColorMeasure 配方编辑器 — {_recipe.Name} (未保存)"
            : $"ColorMeasure 配方编辑器 — {_recipe.Name} — {Path.GetFileName(_filePath)}";

    public string RecipeName
    {
        get => _recipe.Name;
        set { if (_recipe.Name != value) { _recipe.Name = value; OnPropertyChanged(); MarkDirty(); } }
    }

    public string Connect
    {
        get => _recipe.Connect;
        set { if (_recipe.Connect != value) { _recipe.Connect = value; OnPropertyChanged(); MarkDirty(); } }
    }

    public bool CalibrateBeforeStart
    {
        get => _recipe.CalibrateBeforeStart;
        set { if (_recipe.CalibrateBeforeStart != value) { _recipe.CalibrateBeforeStart = value; OnPropertyChanged(); MarkDirty(); } }
    }

    public int SettleMs
    {
        get => _recipe.SettleMs;
        set { if (_recipe.SettleMs != value) { _recipe.SettleMs = value; OnPropertyChanged(); MarkDirty(); } }
    }

    public double? MaxDeltaE2000
    {
        get => _recipe.MaxDeltaE2000;
        set { if (_recipe.MaxDeltaE2000 != value) { _recipe.MaxDeltaE2000 = value; OnPropertyChanged(); MarkDirty(); } }
    }

    public double? MinCoverage
    {
        get => _recipe.MinCoverage;
        set { if (_recipe.MinCoverage != value) { _recipe.MinCoverage = value; OnPropertyChanged(); MarkDirty(); } }
    }

    public ObservableCollection<PatchViewModel> Patches { get; } = new();

    public static IReadOnlyList<string> AllTargets { get; } = new[]
    {
        "sRGB", "DCI-P3", "AdobeRGB", "Rec.2020"
    };

    public bool IsTargetSelected(string target) => _recipe.Targets.Contains(target);
    public void SetTargetSelected(string target, bool selected)
    {
        bool has = _recipe.Targets.Contains(target);
        if (selected && !has) _recipe.Targets.Add(target);
        else if (!selected && has) _recipe.Targets.Remove(target);
        OnPropertyChanged(nameof(IsTargetSelected));
        MarkDirty();
    }

    public bool IsDirty
    {
        get => _isDirty;
        set { if (_isDirty != value) { _isDirty = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); } }
    }

    public string StatusText => IsDirty
        ? $"已修改 (尚未保存) · {Patches.Count} 个 patch"
        : (string.IsNullOrEmpty(_filePath) ? "新建未保存" : $"已加载 · {Patches.Count} 个 patch");

    // -- file operations --

    [RelayCommand]
    public void New()
    {
        if (!ConfirmDiscardIfDirty()) return;
        LoadInternal(new Recipe(), null);
    }

    [RelayCommand]
    public void Open()
    {
        if (!ConfirmDiscardIfDirty()) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "打开配方",
            Filter = "ColorMeasure Recipe (*.json)|*.json",
            InitialDirectory = RecipesDir(),
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var r = RecipeSerializer.LoadFromFile(dlg.FileName);
            LoadInternal(r, dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开失败：{ex.Message}", "ColorMeasure", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void Save()
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            SaveAs();
            return;
        }
        try
        {
            RecipeSerializer.SaveToFile(_recipe, _filePath);
            IsDirty = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败：{ex.Message}", "ColorMeasure", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void SaveAs()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "保存配方",
            Filter = "ColorMeasure Recipe (*.json)|*.json",
            FileName = string.IsNullOrEmpty(_filePath) ? SanitizeFileName(_recipe.Name) + ".json" : Path.GetFileName(_filePath),
            InitialDirectory = string.IsNullOrEmpty(_filePath) ? RecipesDir() : Path.GetDirectoryName(_filePath)!,
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            RecipeSerializer.SaveToFile(_recipe, dlg.FileName);
            _filePath = dlg.FileName;
            OnPropertyChanged(nameof(WindowTitle));
            IsDirty = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败：{ex.Message}", "ColorMeasure", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // -- patch operations --

    [RelayCommand]
    public void AddPatch()
    {
        var n = Patches.Count + 1;
        var p = new RecipePatch { Id = $"patch-{n}", Hex = "#888888", Avg = 8 };
        _recipe.Patches.Add(p);
        Patches.Add(new PatchViewModel(p, this));
        MarkDirty();
    }

    [RelayCommand]
    public void RemovePatch(PatchViewModel? patch)
    {
        if (patch is null) return;
        _recipe.Patches.Remove(patch.Source);
        Patches.Remove(patch);
        MarkDirty();
    }

    [RelayCommand]
    public void MovePatchUp(PatchViewModel? patch)
    {
        if (patch is null) return;
        var i = Patches.IndexOf(patch);
        if (i <= 0) return;
        Patches.Move(i, i - 1);
        _recipe.Patches.Remove(patch.Source);
        _recipe.Patches.Insert(i - 1, patch.Source);
        MarkDirty();
    }

    [RelayCommand]
    public void MovePatchDown(PatchViewModel? patch)
    {
        if (patch is null) return;
        var i = Patches.IndexOf(patch);
        if (i < 0 || i >= Patches.Count - 1) return;
        Patches.Move(i, i + 1);
        _recipe.Patches.Remove(patch.Source);
        _recipe.Patches.Insert(i + 1, patch.Source);
        MarkDirty();
    }

    [RelayCommand]
    public void ResetToDefaults()
    {
        if (!ConfirmDiscardIfDirty()) return;
        LoadInternal(new Recipe
        {
            Name = "New Recipe",
            Targets = new List<string> { "sRGB", "DCI-P3", "AdobeRGB", "Rec.2020" },
        }, null);
    }

    // -- helpers --

    private void LoadInternal(Recipe r, string? path)
    {
        _recipe = r;
        _filePath = path;
        Patches.Clear();
        foreach (var p in r.Patches) Patches.Add(new PatchViewModel(p, this));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(RecipeName));
        OnPropertyChanged(nameof(Connect));
        OnPropertyChanged(nameof(CalibrateBeforeStart));
        OnPropertyChanged(nameof(SettleMs));
        OnPropertyChanged(nameof(MaxDeltaE2000));
        OnPropertyChanged(nameof(MinCoverage));
        OnPropertyChanged(nameof(IsTargetSelected));
        OnPropertyChanged(nameof(StatusText));
        IsDirty = false;
    }

    internal void MarkDirty()
    {
        IsDirty = true;
        OnPropertyChanged(nameof(StatusText));
    }

    private bool ConfirmDiscardIfDirty()
    {
        if (!IsDirty) return true;
        var r = MessageBox.Show("当前配方有未保存的修改。是否放弃？",
            "ColorMeasure", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        return r == MessageBoxResult.Yes;
    }

    private static string SanitizeFileName(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return string.IsNullOrWhiteSpace(s) ? "recipe" : s;
    }

    private static string RecipesDir()
    {
        // Default to <solution>/recipes for Open/Save; the CLI uses this same dir.
        var baseDir = AppContext.BaseDirectory;
        var dir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "recipes"));
        return Directory.Exists(dir) ? dir : baseDir;
    }
}
