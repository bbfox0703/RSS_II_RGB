using System.Text.Json;

namespace RSS_II_RGB.App;

/// <summary>Loads and saves <see cref="AppSettings"/> to settings.json under %LOCALAPPDATA%\RSS_II_RGB.</summary>
internal sealed class SettingsService
{
    private readonly string _path;

    public AppSettings Settings { get; }

    public SettingsService()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppConstants.AppFolderName);
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
        Settings = Load();
    }

    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(Settings, AppSettingsJsonContext.Default.AppSettings);
            File.WriteAllText(_path, json);
        }
        catch
        {
            // Best effort — a failed save must never crash the app.
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                string json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();
            }
        }
        catch
        {
            // Corrupt/unreadable settings fall back to defaults.
        }
        return new AppSettings();
    }
}
