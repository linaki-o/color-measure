using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ColorMeasure.Domain.Abstractions;
using ColorMeasure.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ColorMeasure.Infrastructure.Display.GpuPalette;

/// <summary>
/// WPF fullscreen "color box" driver. Runs the WPF dispatcher on a dedicated STA thread so
/// it can be hosted from a console process. Renders a single solid-color rectangle at the
/// requested RGB and waits <c>settle</c> ms before returning.
/// </summary>
public sealed class GpuPaletteDisplayDriver : IDisplayDriver
{
    private readonly ILogger<GpuPaletteDisplayDriver> _log;
    private readonly Thread _staThread;
    private readonly ManualResetEventSlim _ready = new();
    private Dispatcher? _dispatcher;
    private PaletteWindow? _window;
    private Exception? _startupError;

    public GpuPaletteDisplayDriver(ILogger<GpuPaletteDisplayDriver> log)
    {
        _log = log;
        _staThread = new Thread(Start) { Name = "ColorMeasure.Sta", IsBackground = true };
        _staThread.SetApartmentState(ApartmentState.STA);
        _staThread.Start();
        if (!_ready.Wait(TimeSpan.FromSeconds(10)))
            throw new InvalidOperationException("GPU palette driver failed to start in time");
        if (_startupError is not null) throw _startupError;
    }

    private void Start()
    {
        try
        {
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            _dispatcher = Dispatcher.CurrentDispatcher;
            _window = new PaletteWindow();
            _window.Show();
            _ready.Set();
            Dispatcher.Run();
        }
        catch (Exception ex)
        {
            _startupError = ex;
            _ready.Set();
        }
    }

    public Task ShowAsync(PatchSpec patch, TimeSpan settle, CancellationToken ct)
    {
        var showTask = _dispatcher!.InvokeAsync(() =>
        {
            _window!.Apply(patch.R, patch.G, patch.B);
            _window.Activate();
        }).Task;

        return showTask.ContinueWith(_ =>
        {
            if (settle > TimeSpan.Zero) Task.Delay(settle, ct).GetAwaiter().GetResult();
        }, TaskScheduler.Default);
    }

    public Task HideAsync(CancellationToken ct) => _dispatcher!.InvokeAsync(() =>
    {
        _window?.Apply(0, 0, 0);
    }).Task;

    public async ValueTask DisposeAsync()
    {
        if (_dispatcher is null) return;
        await _dispatcher.InvokeAsync(() =>
        {
            _window?.Close();
            System.Windows.Application.Current?.Shutdown();
        });
        _dispatcher.InvokeShutdown();
    }

    private sealed class PaletteWindow : Window
    {
        private readonly Border _fill;

        public PaletteWindow()
        {
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Normal;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ShowInTaskbar = false;
            Cursor = Cursors.None;
            Background = Brushes.Black;
            AllowsTransparency = false;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            Left = 0; Top = 0;

            _fill = new Border { Background = Brushes.Black };
            Content = _fill;
            PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        }

        public void Apply(byte r, byte g, byte b)
        {
            _fill.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
        }
    }
}
