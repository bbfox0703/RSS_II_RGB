namespace RSS_II_RGB.App;

/// <summary>Centralised magic values for the App project (CLAUDE.md rule 8).</summary>
internal static class AppConstants
{
    public const string AppFolderName = "RSS_II_RGB";                          // %LOCALAPPDATA%\RSS_II_RGB
    // Per-user single instance (no "Global\\" — one instance per logon session).
    public const string SingleInstanceMutexName = "RSS_II_RGB_SingleInstance_5b6c1f2a";
    // A second launch signals this event so the running instance surfaces from the tray.
    public const string ShowWindowEventName = "RSS_II_RGB_ShowWindow_5b6c1f2a";
    public const string AppTitle = "Strix Scope II RGB";
}
