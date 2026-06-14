using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RSS_II_RGB.Windows;

namespace RSS_II_RGB.App;

public partial class App : Application
{
    private KeyboardController? _controller;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var log = new RotatingFileLogSink();
            _controller = new KeyboardController(log);
            var settings = new SettingsService();
            var viewModel = new MainViewModel(_controller, settings);

            desktop.MainWindow = new MainWindow { DataContext = viewModel };
            desktop.ShutdownRequested += OnShutdownRequested;

            _ = viewModel.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_controller is not null)
        {
            await _controller.DisposeAsync();
        }
    }
}
