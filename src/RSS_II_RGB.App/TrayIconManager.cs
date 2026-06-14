using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace RSS_II_RGB.App;

/// <summary>
/// Minimise-to-tray support. This app runs continuously to drive the keyboard, so
/// closing or minimising the window only hides it to a system-tray icon — the
/// render loop keeps running. The app exits only via the tray menu's Exit (or an
/// OS shutdown). A second launch surfaces the hidden window instead of starting a
/// new instance (see <see cref="Program"/>).
/// </summary>
internal sealed class TrayIconManager : IDisposable
{
    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly Window _window;

    private TrayIcon? _trayIcon;
    private EventWaitHandle? _showEvent;
    private Thread? _listener;
    private volatile bool _running;
    private bool _exiting;

    public TrayIconManager(IClassicDesktopStyleApplicationLifetime desktop, Window window)
    {
        _desktop = desktop;
        _window = window;
    }

    public void Initialize()
    {
        // We own shutdown: hiding the window must not quit the app.
        _desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _trayIcon = new TrayIcon
        {
            Icon = _window.Icon,
            ToolTipText = AppConstants.AppTitle,
            IsVisible = true,
            Menu = BuildMenu(),
        };
        _trayIcon.Clicked += (_, _) => ShowWindow();

        _window.Closing += OnClosing;
        _window.PropertyChanged += OnWindowPropertyChanged;

        StartActivationListener();
    }

    private NativeMenu BuildMenu()
    {
        var show = new NativeMenuItem("Show");
        show.Click += (_, _) => ShowWindow();

        var exit = new NativeMenuItem("Exit");
        exit.Click += (_, _) => Exit();

        return new NativeMenu { Items = { show, new NativeMenuItemSeparator(), exit } };
    }

    // X button hides to the tray instead of quitting (unless we're really exiting).
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exiting)
        {
            return;
        }
        e.Cancel = true;
        _window.Hide();
    }

    // Minimising hides to the tray too; reset the state so it restores normally.
    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty && _window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
            _window.Hide();
        }
    }

    private void ShowWindow() => Dispatcher.UIThread.Post(() =>
    {
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    });

    private void Exit()
    {
        _exiting = true;
        if (_trayIcon is not null)
        {
            _trayIcon.IsVisible = false;
        }
        Dispatcher.UIThread.Post(() => _desktop.Shutdown());
    }

    // Background thread that waits for a second launch to ask us to surface.
    private void StartActivationListener()
    {
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, AppConstants.ShowWindowEventName);
        _running = true;
        _listener = new Thread(ListenLoop) { IsBackground = true, Name = "rss-tray-activate" };
        _listener.Start();
    }

    private void ListenLoop()
    {
        while (_running)
        {
            try
            {
                if (_showEvent!.WaitOne(500) && _running)
                {
                    ShowWindow();
                }
            }
            catch
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        _running = false;
        _listener?.Join(1000);
        _showEvent?.Dispose();
        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
