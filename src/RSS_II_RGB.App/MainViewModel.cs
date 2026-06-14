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
    private bool _ready;

    [ObservableProperty]
    private string _statusText = "Connecting…";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private EffectChoice _selectedEffect = EffectChoice.Rainbow;

    [ObservableProperty]
    private double _brightnessPercent = 100;

    // Single source of truth for the effect colour (driven by the ColorPicker
    // and the preset swatch buttons).
    [ObservableProperty]
    private Color _pickedColor = Color.FromRgb(0x00, 0xFF, 0x66);

    public EffectChoice[] Effects { get; } =
    {
        EffectChoice.Off, EffectChoice.Solid, EffectChoice.Breathing,
        EffectChoice.Rainbow, EffectChoice.Wave, EffectChoice.Reactive,
    };

    public MainViewModel(KeyboardController controller) => _controller = controller;

    public async Task InitializeAsync()
    {
        bool ok = await _controller.StartAsync();
        IsConnected = ok;
        StatusText = ok
            ? $"Connected — Scope II RX, firmware {_controller.Firmware}"
            : "Keyboard not found. Close Armoury Crate / OpenRGB, then restart.";
        _ready = ok;
        if (ok)
        {
            Apply();
        }
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

    private void Apply()
    {
        if (!_ready)
        {
            return;
        }
        var rgb = new Rgb(PickedColor.R, PickedColor.G, PickedColor.B);
        _controller.ApplyEffect(SelectedEffect, rgb, BrightnessPercent / 100.0);
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
