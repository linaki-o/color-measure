using System.Globalization;
using System.Windows.Data;
using ColorMeasure.Host.Wpf.ViewModels;

namespace ColorMeasure.Host.Wpf;

/// <summary>
/// Multi-value converter for the per-target CheckBox.IsChecked binding.
/// The MultiBinding passes (MainViewModel, targetName) and we return the
/// current membership. Setter is a no-op (the Click handler in MainWindow
/// code-behind is the single source of truth for the toggle, since
/// ConverterParameter cannot accept a Binding).
/// </summary>
public sealed class TargetCheckedConverter : IMultiValueConverter
{
    public static readonly TargetCheckedConverter Instance = new();

    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length != 2) return false;
        if (values[0] is not MainViewModel vm || values[1] is not string name) return false;
        return vm.IsTargetSelected(name);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        // Click handler does the real mutation; the bound back-channel is unused.
        return Array.Empty<object>();
    }
}
