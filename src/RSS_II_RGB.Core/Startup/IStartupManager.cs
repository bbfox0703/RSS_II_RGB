namespace RSS_II_RGB.Core.Startup;

/// <summary>
/// Manages whether the app launches automatically when the user logs in
/// (CLAUDE.md rule 4 — the registry/OS call lives behind this interface, the
/// Windows project implements it). When enabled, the app is registered to start
/// minimised to the tray.
/// </summary>
public interface IStartupManager
{
    /// <summary>True on platforms where auto-start is available.</summary>
    bool IsSupported { get; }

    /// <summary>True if the app is currently registered to start with the OS.</summary>
    bool IsEnabled();

    /// <summary>Register or unregister auto-start. Best effort; re-query with <see cref="IsEnabled"/>.</summary>
    void SetEnabled(bool enabled);
}
