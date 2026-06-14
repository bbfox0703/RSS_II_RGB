using Microsoft.Win32;
using RSS_II_RGB.Core.Startup;

namespace RSS_II_RGB.Windows;

/// <summary>
/// Auto-start via the per-user "Run" registry key
/// (HKCU\Software\Microsoft\Windows\CurrentVersion\Run). No admin rights needed.
/// When enabled, the registered command includes the start-minimised argument so
/// the app comes up hidden in the tray. AOT-safe (no reflection).
/// </summary>
public sealed class Win32StartupManager : IStartupManager
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _valueName;
    private readonly string _startupArg;

    public Win32StartupManager(string valueName, string startupArg)
    {
        _valueName = valueName;
        _startupArg = startupArg;
    }

    public bool IsSupported => OperatingSystem.IsWindows();

    public bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(_valueName) is not null;
        }
        catch
        {
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                string? exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe))
                {
                    key.SetValue(_valueName, $"\"{exe}\" {_startupArg}");
                }
            }
            else if (key.GetValue(_valueName) is not null)
            {
                key.DeleteValue(_valueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Best effort — a registry failure must never crash the app.
        }
    }
}
