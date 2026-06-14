using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.App;

/// <summary>
/// UI state for the main window. Uses CommunityToolkit.Mvvm source generators
/// only (no reflection) so it survives Native AOT trimming.
/// </summary>
internal sealed partial class MainViewModel : ObservableObject
{
    private readonly KeyboardController _controller;
    private readonly SettingsService _settings;
    private bool _ready;
    private bool _loading;

    [ObservableProperty]
    private string _statusText = "Connecting…";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private EffectChoice _selectedEffect = EffectChoice.Rainbow;

    [ObservableProperty]
    private double _brightnessPercent = 100;

    // Audio visualiser brightness multiplier (1 = raw, higher fills the keyboard more).
    [ObservableProperty]
    private double _audioSensitivity = 1.5;

    // Single source of truth for the effect colour (ColorPicker + preset swatches).
    [ObservableProperty]
    private Color _pickedColor = Color.FromRgb(0x00, 0xFF, 0x66);

    public EffectChoice[] Effects { get; } =
    {
        EffectChoice.Off, EffectChoice.Solid, EffectChoice.Breathing,
        EffectChoice.Rainbow, EffectChoice.Wave, EffectChoice.Reactive,
        EffectChoice.CpuTemp, EffectChoice.GpuTemp, EffectChoice.Audio,
    };

    public MainViewModel(KeyboardController controller, SettingsService settings)
    {
        _controller = controller;
        _settings = settings;
    }

    /// <summary>The shared controller, handed to the zone editor.</summary>
    public KeyboardController Controller => _controller;

    /// <summary>The shared settings store, handed to the zone editor.</summary>
    public SettingsService Settings => _settings;

    public async Task InitializeAsync()
    {
        bool ok = await _controller.StartAsync();
        IsConnected = ok;
        StatusText = ok
            ? $"Connected — Scope II RX, firmware {_controller.Firmware}"
            : "Keyboard not found. Close Armoury Crate / OpenRGB, then restart.";
        _ready = ok;
        if (!ok)
        {
            return;
        }

        // Restore the saved setup.
        _loading = true;
        AppSettings s = _settings.Settings;
        SelectedEffect = s.GlobalEffect;
        if (TryParseColor(s.GlobalColorHex, out Color color))
        {
            PickedColor = color;
        }
        BrightnessPercent = s.BrightnessPercent;
        AudioSensitivity = s.AudioSensitivity;
        _loading = false;

        _controller.SetZones(s.Zones.Select(ZoneMapping.ToZone).ToArray());
        Apply();
    }

    [RelayCommand]
    private void SetColor(string hex)
    {
        if (TryParseColor(hex, out Color color))
        {
            PickedColor = color;
        }
    }

    partial void OnSelectedEffectChanged(EffectChoice value) => Apply();

    partial void OnBrightnessPercentChanged(double value) => Apply();

    partial void OnPickedColorChanged(Color value) => Apply();

    partial void OnAudioSensitivityChanged(double value) => Apply();

    private void Apply()
    {
        if (!_ready || _loading)
        {
            return;
        }

        _controller.AudioSensitivity = AudioSensitivity;
        var rgb = new Rgb(PickedColor.R, PickedColor.G, PickedColor.B);
        _controller.SetGlobalEffect(SelectedEffect, rgb, BrightnessPercent / 100.0);

        AppSettings s = _settings.Settings;
        s.GlobalEffect = SelectedEffect;
        s.GlobalColorHex = $"{PickedColor.R:X2}{PickedColor.G:X2}{PickedColor.B:X2}";
        s.BrightnessPercent = BrightnessPercent;
        s.AudioSensitivity = AudioSensitivity;
        _settings.Save();
    }

    private static bool TryParseColor(string s, out Color color)
    {
        s = s.Trim().TrimStart('#');
        if (s.Length == 6 && uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
        {
            color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            return true;
        }
        color = Colors.White;
        return false;
    }
}
