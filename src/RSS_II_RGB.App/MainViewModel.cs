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

    // Independent display overlays, separate from the base effect (layered on top).
    // Reactive outranks Audio: keypress flares always show, audio sits below them.
    [ObservableProperty]
    private bool _enableReactive;

    [ObservableProperty]
    private bool _enableAudio;

    // Audio visualiser brightness multiplier (1 = raw, higher fills the keyboard more).
    [ObservableProperty]
    private double _audioSensitivity = 0.9;

    // System-metric overlay.
    [ObservableProperty]
    private bool _showMetrics;

    [ObservableProperty]
    private MetricLayoutChoice _metricLayout = MetricLayoutChoice.FunctionRow;

    [ObservableProperty]
    private decimal _pct1 = 30;
    [ObservableProperty]
    private decimal _pct2 = 60;
    [ObservableProperty]
    private decimal _pct3 = 90;
    [ObservableProperty]
    private decimal _temp1 = 55;
    [ObservableProperty]
    private decimal _temp2 = 65;
    [ObservableProperty]
    private decimal _temp3 = 75;

    public MetricLayoutChoice[] MetricLayoutOptions { get; } =
    {
        MetricLayoutChoice.FunctionRow, MetricLayoutChoice.Numpad, MetricLayoutChoice.Diagonal,
    };

    // Single source of truth for the effect colour (ColorPicker + preset swatches).
    [ObservableProperty]
    private Color _pickedColor = Color.FromRgb(0x00, 0xFF, 0x66);

    // Base effects only. Reactive and Audio are independent overlay toggles below.
    public EffectChoice[] Effects { get; } =
    {
        EffectChoice.Off, EffectChoice.Solid, EffectChoice.Breathing,
        EffectChoice.Rainbow, EffectChoice.Wave,
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
        EnableReactive = s.EnableReactive;
        EnableAudio = s.EnableAudio;
        // Reactive/Audio are now overlay toggles, and CpuTemp/GpuTemp are no longer
        // base effects. Migrate any of those legacy choices to a base + the toggle.
        switch (s.GlobalEffect)
        {
            case EffectChoice.Reactive:
                SelectedEffect = EffectChoice.Off;
                EnableReactive = true;
                break;
            case EffectChoice.Audio:
                SelectedEffect = EffectChoice.Off;
                EnableAudio = true;
                break;
            case EffectChoice.CpuTemp or EffectChoice.GpuTemp:
                SelectedEffect = EffectChoice.Rainbow;
                break;
            default:
                SelectedEffect = s.GlobalEffect;
                break;
        }
        if (TryParseColor(s.GlobalColorHex, out Color color))
        {
            PickedColor = color;
        }
        BrightnessPercent = s.BrightnessPercent;
        AudioSensitivity = s.AudioSensitivity;
        ShowMetrics = s.ShowMetrics;
        MetricLayout = s.MetricLayout;
        if (s.PercentThresholds.Length >= 3)
        {
            Pct1 = (decimal)s.PercentThresholds[0];
            Pct2 = (decimal)s.PercentThresholds[1];
            Pct3 = (decimal)s.PercentThresholds[2];
        }
        if (s.TempThresholds.Length >= 3)
        {
            Temp1 = (decimal)s.TempThresholds[0];
            Temp2 = (decimal)s.TempThresholds[1];
            Temp3 = (decimal)s.TempThresholds[2];
        }
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

    partial void OnEnableReactiveChanged(bool value) => Apply();

    partial void OnEnableAudioChanged(bool value) => Apply();

    partial void OnBrightnessPercentChanged(double value) => Apply();

    partial void OnPickedColorChanged(Color value) => Apply();

    partial void OnAudioSensitivityChanged(double value) => Apply();

    partial void OnShowMetricsChanged(bool value) => Apply();
    partial void OnMetricLayoutChanged(MetricLayoutChoice value) => Apply();
    partial void OnPct1Changed(decimal value) => Apply();
    partial void OnPct2Changed(decimal value) => Apply();
    partial void OnPct3Changed(decimal value) => Apply();
    partial void OnTemp1Changed(decimal value) => Apply();
    partial void OnTemp2Changed(decimal value) => Apply();
    partial void OnTemp3Changed(decimal value) => Apply();

    private void Apply()
    {
        if (!_ready || _loading)
        {
            return;
        }

        double[] percent = { (double)Pct1, (double)Pct2, (double)Pct3 };
        double[] temp = { (double)Temp1, (double)Temp2, (double)Temp3 };

        _controller.EnableReactive = EnableReactive;
        _controller.EnableAudio = EnableAudio;
        _controller.AudioSensitivity = AudioSensitivity;
        _controller.ShowMetrics = ShowMetrics;
        _controller.MetricLayout = MetricLayout;
        _controller.PercentThresholds = percent;
        _controller.TempThresholds = temp;

        var rgb = new Rgb(PickedColor.R, PickedColor.G, PickedColor.B);
        _controller.SetGlobalEffect(SelectedEffect, rgb, BrightnessPercent / 100.0);

        AppSettings s = _settings.Settings;
        s.GlobalEffect = SelectedEffect;
        s.GlobalColorHex = $"{PickedColor.R:X2}{PickedColor.G:X2}{PickedColor.B:X2}";
        s.BrightnessPercent = BrightnessPercent;
        s.EnableReactive = EnableReactive;
        s.EnableAudio = EnableAudio;
        s.AudioSensitivity = AudioSensitivity;
        s.ShowMetrics = ShowMetrics;
        s.MetricLayout = MetricLayout;
        s.PercentThresholds = percent;
        s.TempThresholds = temp;
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
