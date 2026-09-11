using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ColorMeasure.Host.Wpf.ViewModels;

namespace ColorMeasure.Host.Wpf;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

        // Common keyboard shortcuts
        InputBindings.Add(new System.Windows.Input.KeyBinding(
            ((MainViewModel)DataContext).NewCommand,
            new System.Windows.Input.KeyGesture(Key.N, ModifierKeys.Control)));
        InputBindings.Add(new System.Windows.Input.KeyBinding(
            ((MainViewModel)DataContext).OpenCommand,
            new System.Windows.Input.KeyGesture(Key.O, ModifierKeys.Control)));
        InputBindings.Add(new System.Windows.Input.KeyBinding(
            ((MainViewModel)DataContext).SaveCommand,
            new System.Windows.Input.KeyGesture(Key.S, ModifierKeys.Control)));
        InputBindings.Add(new System.Windows.Input.KeyBinding(
            ((MainViewModel)DataContext).SaveAsCommand,
            new System.Windows.Input.KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift)));
        InputBindings.Add(new System.Windows.Input.KeyBinding(
            ((MainViewModel)DataContext).AddPatchCommand,
            new System.Windows.Input.KeyGesture(Key.Insert)));
    }

    private void OnExitClicked(object sender, RoutedEventArgs e) => Close();

    private void OnAboutClicked(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show(
            "ColorMeasure 配方编辑器 (v1)\n\n" +
            "配置和编辑测试配方 (JSON)，与 CLI 共用同一种 schema。\n" +
            "运行请用 CLI: color-measure --plan <recipe.json> --out <dir>",
            "关于", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    /// <summary>
    /// The MultiBinding/ValueConverter pattern can't know which target was
    /// toggled, so the CheckBox Click handler routes the change through
    /// MainViewModel.SetTargetSelected(target, isChecked).
    /// </summary>
    private void OnTargetToggled(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.CheckBox cb || cb.DataContext is not string name) return;
        if (DataContext is not MainViewModel vm) return;
        vm.SetTargetSelected(name, cb.IsChecked == true);
    }
}
