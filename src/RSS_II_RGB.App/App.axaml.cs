using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RSS_II_RGB.Core.Sensors;
using RSS_II_RGB.Windows;

namespace RSS_II_RGB.App;

public partial class App : Application
{
    private KeyboardController? _controller;
    private SensorService? _sensorService;
    private TrayIconManager? _tray;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var log = new RotatingFileLogSink();
            var sensors = new SensorState();
            _sensorService = new SensorService(sensors, log);
            _controller = new KeyboardController(log, sensors);
            var settings = new SettingsService();
            var startup = new Win32StartupManager(AppConstants.StartupRegistryValueName, AppConstants.StartMinimizedArg);
            var viewModel = new MainViewModel(_controller, settings, startup);

            var mainWindow = new MainWindow { DataContext = viewModel };
            desktop.MainWindow = mainWindow;
            desktop.ShutdownRequested += OnShutdownRequested;

            // Launched by auto-start? Come up hidden in the tray.
            bool startHidden = desktop.Args is { } args && args.Contains(AppConstants.StartMinimizedArg);

            // Keep running in the tray when the window is closed/minimised.
            _tray = new TrayIconManager(desktop, mainWindow);
            _tray.Initialize(startHidden);

            _ = viewModel.InitializeAsync();
            _sensorService.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _tray?.Dispose();
        if (_sensorService is not null)
        {
            await _sensorService.DisposeAsync();
        }
        if (_controller is not null)
        {
            await _controller.DisposeAsync();
        }
    }
}
