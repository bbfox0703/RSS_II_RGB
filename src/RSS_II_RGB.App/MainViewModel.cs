using System.Globalization;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RSS_II_RGB.Core.Rendering;
using RSS_II_RGB.Core.Startup;

namespace RSS_II_RGB.App;

/// <summary>
/// UI state for the main window. Uses CommunityToolkit.Mvvm source generators
/// only (no reflection) so it survives Native AOT trimming.
/// </summary>
internal sealed partial class MainViewModel : ObservableObject
{
    private readonly KeyboardController _controller;
    private readonly SettingsService _settings;
    private readonly IStartupManager _startup;
    private bool _ready;
    private bool _loading;

    [ObservableProperty]
    private string _statusText = L.StatusConnecting;

    /// <summary>Localized, formatted labels for the sliders.</summary>
    public string BrightnessText => string.Format(CultureInfo.InvariantCulture, L.BrightnessFormat, BrightnessPercent);
    public string AudioSensitivityText => string.Format(CultureInfo.InvariantCulture, L.AudioSensitivityFormat, AudioSensitivity);

    [ObservableProperty]
    private bool _isConnected;

    // Launch with Windows, registered to start minimised in the tray. The registry
    // is the source of truth, so this isn't part of the saved settings.
    [ObservableProperty]
    private bool _startWithWindows;

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

    // Global Audio overlay: spectrum vs three-region bars, with a per-region multiplier.
    [ObservableProperty]
    private AudioLayoutChoice _audioLayout = AudioLayoutChoice.Spectrum;

    [ObservableProperty]
    private double _audioBarsBassMultiplier = 0.9;

    [ObservableProperty]
    private double _audioBarsMidMultiplier = 1.0;

    [ObservableProperty]
    private double _audioBarsTrebleMultiplier = 2.0;

    public AudioLayoutChoice[] AudioLayoutOptions { get; } =
    {
        AudioLayoutChoice.Spectrum, AudioLayoutChoice.Bars,
    };

    /// <summary>True when the bars layout is selected (gates its multiplier sliders).</summary>
    public bool IsAudioBarsLayout => AudioLayout == AudioLayoutChoice.Bars;

    public string AudioBarsBassText =>
        string.Format(CultureInfo.InvariantCulture, L.AudioBarsBassFormat, AudioBarsBassMultiplier);
    public string AudioBarsMidText =>
        string.Format(CultureInfo.InvariantCulture, L.AudioBarsMidFormat, AudioBarsMidMultiplier);
    public string AudioBarsTrebleText =>
        string.Format(CultureInfo.InvariantCulture, L.AudioBarsTrebleFormat, AudioBarsTrebleMultiplier);

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

    public MainViewModel(KeyboardController controller, SettingsService settings, IStartupManager startup)
    {
        _controller = controller;
        _settings = settings;
        _startup = startup;
    }

    /// <summary>The shared controller, handed to the zone editor.</summary>
    public KeyboardController Controller => _controller;

    /// <summary>The shared settings store, handed to the zone editor.</summary>
    public SettingsService Settings => _settings;

    public Task InitializeAsync()
    {
        // Restore the saved setup first — it applies whether or not a keyboard is
        // present yet; the controller reconnects on its own and re-applies it.
        _loading = true;
        StartWithWindows = _startup.IsEnabled();
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
        AudioLayout = s.AudioLayout;
        AudioBarsBassMultiplier = s.AudioBarsBassMultiplier;
        AudioBarsMidMultiplier = s.AudioBarsMidMultiplier;
        AudioBarsTrebleMultiplier = s.AudioBarsTrebleMultiplier;
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
        _ready = true;

        // Push the restored configuration to the controller, then begin the
        // detect/reconnect lifecycle; the status text tracks StateChanged.
        _controller.SetZones(s.Zones.Select(ZoneMapping.ToZone).ToArray());
        Apply();

        _controller.StateChanged += OnControllerStateChanged;
        StatusText = L.StatusSearching;
        _controller.Start();
        return Task.CompletedTask;
    }

    private void OnControllerStateChanged() => Dispatcher.UIThread.Post(() =>
    {
        IsConnected = _controller.IsConnected;
        StatusText = _controller.IsConnected
            ? string.Format(CultureInfo.InvariantCulture, L.StatusConnectedFormat, _controller.Firmware)
            : L.StatusSearching;
    });

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

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading)
        {
            return;
        }
        _startup.SetEnabled(value);
        // Reflect the real registry state in case the write was blocked.
        bool actual = _startup.IsEnabled();
        if (actual != value)
        {
            StartWithWindows = actual;
        }
    }

    partial void OnBrightnessPercentChanged(double value)
    {
        OnPropertyChanged(nameof(BrightnessText));
        Apply();
    }

    partial void OnPickedColorChanged(Color value) => Apply();

    partial void OnAudioSensitivityChanged(double value)
    {
        OnPropertyChanged(nameof(AudioSensitivityText));
        Apply();
    }

    partial void OnAudioLayoutChanged(AudioLayoutChoice value)
    {
        OnPropertyChanged(nameof(IsAudioBarsLayout));
        Apply();
    }

    partial void OnAudioBarsBassMultiplierChanged(double value)
    {
        OnPropertyChanged(nameof(AudioBarsBassText));
        Apply();
    }

    partial void OnAudioBarsMidMultiplierChanged(double value)
    {
        OnPropertyChanged(nameof(AudioBarsMidText));
        Apply();
    }

    partial void OnAudioBarsTrebleMultiplierChanged(double value)
    {
        OnPropertyChanged(nameof(AudioBarsTrebleText));
        Apply();
    }

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
        _controller.AudioLayout = AudioLayout;
        _controller.AudioBarsBassMultiplier = AudioBarsBassMultiplier;
        _controller.AudioBarsMidMultiplier = AudioBarsMidMultiplier;
        _controller.AudioBarsTrebleMultiplier = AudioBarsTrebleMultiplier;
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
        s.AudioLayout = AudioLayout;
        s.AudioBarsBassMultiplier = AudioBarsBassMultiplier;
        s.AudioBarsMidMultiplier = AudioBarsMidMultiplier;
        s.AudioBarsTrebleMultiplier = AudioBarsTrebleMultiplier;
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
