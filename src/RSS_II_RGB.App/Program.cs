using Avalonia;

namespace RSS_II_RGB.App;

internal static class Program
{
    // Single instance via Mutex (CLAUDE.md rule 2): only one process may own the
    // keyboard at a time.
    [STAThread]
    public static int Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, AppConstants.SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            // Already running (possibly hidden in the tray): ask it to surface.
            try
            {
                using EventWaitHandle ev = EventWaitHandle.OpenExisting(AppConstants.ShowWindowEventName);
                ev.Set();
            }
            catch
            {
                // No listener yet — nothing to surface.
            }
            return 0;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    // Avalonia configuration (also used by the visual designer).
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
