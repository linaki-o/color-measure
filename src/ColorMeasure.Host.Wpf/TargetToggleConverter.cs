using System.Globalization;
using System.Windows.Data;
using ColorMeasure.Host.Wpf.ViewModels;

namespace ColorMeasure.Host.Wpf;

/// <summary>
/// Single-value converter for the per-target CheckBox: reads the IsChecked
/// state of (vm, targetName) on get; on set, returns the new bool (the actual
/// state mutation is done by the Click event handler because we don't have
/// a back-channel to know which target was toggled).
/// </summary>
public sealed class TargetToggleConverter : IValueConverter
{
    public static readonly TargetToggleConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not MainViewModel vm || parameter is not string name) return false;
        return vm.IsTargetSelected(name);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value; // bind back is a no-op; Click handler does the real mutation
    }
}
