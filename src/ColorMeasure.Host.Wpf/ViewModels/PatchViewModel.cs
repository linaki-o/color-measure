using System.Text.RegularExpressions;
using System.Windows.Media;
using ColorMeasure.Host.Wpf.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ColorMeasure.Host.Wpf.ViewModels;

/// <summary>
/// One row in the patches DataGrid. Wraps the underlying <see cref="RecipePatch"/>
/// so we can hold an independent dirty flag and live-validate the HEX color.
/// </summary>
public partial class PatchViewModel : ObservableObject
{
    private static readonly Regex HexRegex = new(@"^#?[0-9A-Fa-f]{6}$", RegexOptions.Compiled);
    private static readonly SolidColorBrush BlackBrush = new(System.Windows.Media.Color.FromRgb(0, 0, 0));
    private static readonly SolidColorBrush ErrorBrush = new(System.Windows.Media.Color.FromRgb(255, 80, 80));

    public RecipePatch Source { get; }
    private readonly MainViewModel _parent;

    public PatchViewModel(RecipePatch source, MainViewModel parent)
    {
        Source = source;
        _parent = parent;
    }

    public string Id
    {
        get => Source.Id;
        set { if (Source.Id != value) { Source.Id = value; OnPropertyChanged(); _parent.MarkDirty(); } }
    }

    public string Hex
    {
        get => Source.Hex;
        set
        {
            value = NormalizeHex(value);
            if (Source.Hex == value) return;
            Source.Hex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsValidHex));
            OnPropertyChanged(nameof(IsGrayscale));
            OnPropertyChanged(nameof(Swatch));
            OnPropertyChanged(nameof(PreviewTextColor));
            _parent.MarkDirty();
        }
    }

    public int Avg
    {
        get => Source.Avg;
        set { if (Source.Avg != value) { Source.Avg = value; OnPropertyChanged(); _parent.MarkDirty(); } }
    }

    public string? Label
    {
        get => Source.Label;
        set { if (Source.Label != value) { Source.Label = value; OnPropertyChanged(); _parent.MarkDirty(); } }
    }

    public bool IsValidHex => HexRegex.IsMatch(Source.Hex);

    public bool IsGrayscale
    {
        get
        {
            if (!IsValidHex) return false;
            var h = Source.Hex.TrimStart('#');
            return h[0] == h[2] && h[2] == h[4];
        }
    }

    public System.Windows.Media.Brush Swatch => IsValidHex ? new SolidColorBrush(ParseHex(Source.Hex)) : ErrorBrush;

    public System.Windows.Media.Brush PreviewTextColor
    {
        get
        {
            if (!IsValidHex) return BlackBrush;
            var c = ParseHex(Source.Hex);
            var luma = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
            return luma > 140 ? BlackBrush : System.Windows.Media.Brushes.White;
        }
    }

    [RelayCommand]
    public void PickColor()
    {
        var initial = IsValidHex ? ParseHex(Source.Hex) : System.Windows.Media.Color.FromRgb(128, 128, 128);
        using var dlg = new System.Windows.Forms.ColorDialog
        {
            Color = System.Drawing.Color.FromArgb(initial.A, initial.R, initial.G, initial.B),
            FullOpen = true,
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var c = dlg.Color;
            Hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }

    private static string NormalizeHex(string v)
    {
        v = v.Trim();
        if (v.Length == 7 && v[0] == '#') return v.ToUpperInvariant();
        if (v.Length == 6) return "#" + v.ToUpperInvariant();
        if (v.Length == 8 && v[0] == '#') return "#" + v.Substring(2).ToUpperInvariant(); // strip leading AA
        return v;
    }

    private static System.Windows.Media.Color ParseHex(string hex)
    {
        var h = hex.TrimStart('#');
        return System.Windows.Media.Color.FromRgb(
            System.Convert.ToByte(h.Substring(0, 2), 16),
            System.Convert.ToByte(h.Substring(2, 2), 16),
            System.Convert.ToByte(h.Substring(4, 2), 16));
    }
}
