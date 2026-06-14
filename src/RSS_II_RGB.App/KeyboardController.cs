using RSS_II_RGB.Core.Device;
using RSS_II_RGB.Core.Effects;
using RSS_II_RGB.Core.Effects.Layers;
using RSS_II_RGB.Core.Engine;
using RSS_II_RGB.Core.Input;
using RSS_II_RGB.Core.Layout;
using RSS_II_RGB.Core.Logging;
using RSS_II_RGB.Core.Rendering;
using RSS_II_RGB.Core.Sensors;
using RSS_II_RGB.Windows;

namespace RSS_II_RGB.App;

/// <summary>
/// Owns the device, render engine, and keyboard hook. Composes the active layer
/// stack from a global effect plus zero or more per-zone overrides and streams it
/// to the engine.
/// </summary>
internal sealed class KeyboardController : IAsyncDisposable
{
    private readonly ILogSink _log;
    private readonly SensorState _sensors;
    private IKeyboardDevice? _device;
    private Win32KeyboardHook? _hook;
    private RenderEngine? _engine;
    private CancellationTokenSource? _cts;
    private Task? _engineTask;

    // Composed state.
    private EffectChoice _globalEffect = EffectChoice.Rainbow;
    private Rgb _globalColor = new(0x00, 0xFF, 0x66);
    private double _brightness = 1.0;
    private IReadOnlyList<Zone> _zones = Array.Empty<Zone>();
    private KeyboardProfile _profile = ScopeIILayout.Profile;

    public KeyboardController(ILogSink log, SensorState sensors)
    {
        _log = log;
        _sensors = sensors;
    }

    public bool IsRunning { get; private set; }
    public string Firmware { get; private set; } = "";
    public int LayoutId { get; private set; } = -1;
    public IReadOnlyList<Zone> Zones => _zones;

    /// <summary>The connected keyboard's layout profile (geometry + key map).</summary>
    public KeyboardProfile Profile => _profile;

    /// <summary>Brightness multiplier for the audio visualiser (set before SetGlobalEffect).</summary>
    public double AudioSensitivity { get; set; } = 0.9;

    // Independent global overlays, toggled from the main UI (set before SetGlobalEffect/SetZones).
    // Reactive is the higher-priority of the two: it sits above the zones, audio below them.
    public bool EnableReactive { get; set; }
    public bool EnableAudio { get; set; }

    // System-metric overlay configuration (set before SetGlobalEffect/SetZones).
    public bool ShowMetrics { get; set; }
    public MetricLayoutChoice MetricLayout { get; set; } = MetricLayoutChoice.FunctionRow;
    public double[] PercentThresholds { get; set; } = { 30, 60, 90 };
    public double[] TempThresholds { get; set; } = { 55, 65, 75 };

    public async Task<bool> StartAsync()
    {
        var factory = new Win32KeyboardDeviceFactory();
        _device = await factory.FindAsync();
        if (_device is null)
        {
            _log.Log(LogLevel.Warning, "Keyboard not found or already in use.");
            return false;
        }

        _profile = _device.Profile;

        DeviceInfo info = await _device.ReadInfoAsync();
        Firmware = info.FirmwareVersion;
        LayoutId = info.LayoutId;

        _engine = new RenderEngine(_device, new Compositor(_profile.LedCount), _log, _profile);
        _hook = new Win32KeyboardHook(_profile);
        _hook.KeyChanged += OnKey;
        await _hook.StartAsync();

        _cts = new CancellationTokenSource();
        _engineTask = Task.Run(() => _engine.RunAsync(_cts.Token));
        IsRunning = true;
        _log.Log(LogLevel.Info, $"Started. Firmware {Firmware}, layout {LayoutId}.");
        Rebuild();
        return true;
    }

    /// <summary>Set the base effect that covers every key.</summary>
    public void SetGlobalEffect(EffectChoice effect, Rgb color, double brightness)
    {
        _globalEffect = effect;
        _globalColor = color;
        _brightness = brightness;
        Rebuild();
    }

    /// <summary>Replace the per-zone overrides layered on top of the global effect.</summary>
    public void SetZones(IReadOnlyList<Zone> zones)
    {
        _zones = zones;
        Rebuild();
    }

    // Fixed z-order bands for the display priority stack (higher = more on top).
    // Each band is far enough apart that per-item increments never collide.
    private const int ZBase = 0;           // Layer 0: main-UI base effect (every key)
    private const int ZAudio = 1_000;      // Layer 1: global Audio overlay
    private const int ZZoneOther = 10_000; // Layer 2: non-audio zones
    private const int ZZoneAudio = 20_000; // Layer 3: audio zones
    private const int ZReactive = 30_000;  // Layer 4: global Reactive overlay
    private const int ZMetrics = 500_000;  // Layer 5: system-metric bars (top)

    private void Rebuild()
    {
        if (_engine is null)
        {
            return;
        }

        var layers = new List<IEffectLayer>();

        // Layer 0 — base effect from the main UI, covering every key.
        AddEffectLayers(layers, "global", _globalEffect, _globalColor, KeyMask.All, baseZ: ZBase);

        // Layer 1 — global Audio overlay (spectrum). Additive so silent frames are
        // transparent and the base effect shows through.
        if (EnableAudio)
        {
            layers.Add(new AudioReactiveLayer("audio", _sensors, KeyMask.All,
                                              sensitivity: AudioSensitivity, zOrder: ZAudio,
                                              blend: BlendMode.Additive));
        }

        // Layers 2 & 3 — zone overrides. Non-audio zones sit below all audio zones,
        // so a zone spectrum always wins over a static zone on overlapping keys.
        int zOther = ZZoneOther;
        int zAudio = ZZoneAudio;
        foreach (Zone zone in _zones)
        {
            if (zone.KeyIndices.Count == 0)
            {
                continue;
            }
            KeyMask mask = KeyMask.FromIndices(zone.KeyIndices is int[] arr ? arr : zone.KeyIndices.ToArray());
            bool isAudioZone = zone.Effect == EffectChoice.Audio;
            int baseZ = isAudioZone ? zAudio : zOther;
            AddEffectLayers(layers, $"zone-{baseZ}", zone.Effect, zone.Color, mask, baseZ, zone.AudioMode);
            if (isAudioZone) { zAudio += 10; } else { zOther += 10; }
        }

        // Layer 4 — global Reactive overlay (keypress flare + ripple). Additive
        // overlays only touch pressed keys, so everything below shows through when idle.
        if (EnableReactive)
        {
            layers.Add(new KeypressFadeLayer("reactive-fade", Rgb.White, fadeSeconds: 0.6, zOrder: ZReactive));
            layers.Add(new RippleLayer("reactive-ripple", new Rgb(0, 180, 255), speedGridPerSec: 14,
                                       width: 1.3, fadeSeconds: 0.8, zOrder: ZReactive + 10));
        }

        // Layer 5 — system-metric bars on top of every effect and zone.
        if (ShowMetrics)
        {
            layers.Add(new MetricOverlayLayer("metrics", _sensors, MetricLayouts.Build(MetricLayout, _profile),
                                              PercentThresholds, TempThresholds, zOrder: ZMetrics));
        }

        // Master brightness on top: a Multiply layer that uniformly scales the result.
        if (_brightness < 0.999)
        {
            byte b = (byte)Math.Clamp(_brightness * 255.0, 0, 255);
            layers.Add(new SolidLayer("master-brightness", new Rgb(b, b, b), KeyMask.All,
                                      zOrder: 1_000_000, blend: BlendMode.Multiply));
        }

        _engine.SetEffectLayers(layers);
    }

    // Maps a base/zone effect choice to its layer(s), masked to the given keys.
    // Reactive and the global Audio overlay are composed directly in Rebuild, not here.
    private void AddEffectLayers(List<IEffectLayer> layers, string id, EffectChoice effect,
                                 Rgb color, KeyMask mask, int baseZ,
                                 AudioZoneMode audioMode = AudioZoneMode.Spectrum)
    {
        switch (effect)
        {
            case EffectChoice.Off:
                layers.Add(new SolidLayer(id, Rgb.Black, mask, baseZ));
                break;
            case EffectChoice.Solid:
                layers.Add(new SolidLayer(id, color, mask, baseZ));
                break;
            case EffectChoice.Breathing:
                layers.Add(new BreathingLayer(id, color, mask, zOrder: baseZ));
                break;
            case EffectChoice.Rainbow:
                layers.Add(new RainbowLayer(id, mask, zOrder: baseZ));
                break;
            case EffectChoice.Wave:
                layers.Add(new WaveLayer(id, mask, zOrder: baseZ));
                break;
            case EffectChoice.CpuTemp:
                layers.Add(new TempIndicatorLayer(id, _sensors, gpu: false, mask, zOrder: baseZ));
                break;
            case EffectChoice.GpuTemp:
                layers.Add(new TempIndicatorLayer(id, _sensors, gpu: true, mask, zOrder: baseZ));
                break;
            case EffectChoice.Audio:
                switch (audioMode)
                {
                    case AudioZoneMode.SolidColor:
                        layers.Add(new AudioVolumeLayer(id, _sensors, mask, color, rainbow: false,
                                                        sensitivity: AudioSensitivity, zOrder: baseZ));
                        break;
                    case AudioZoneMode.SolidRainbow:
                        layers.Add(new AudioVolumeLayer(id, _sensors, mask, Rgb.White, rainbow: true,
                                                        sensitivity: AudioSensitivity, zOrder: baseZ));
                        break;
                    default:
                        layers.Add(new AudioReactiveLayer(id, _sensors, mask, sensitivity: AudioSensitivity, zOrder: baseZ));
                        break;
                }
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        IsRunning = false;
        _cts?.Cancel();
        if (_engineTask is not null)
        {
            try
            {
                await _engineTask.ConfigureAwait(false);
            }
            catch
            {
                // shutting down
            }
        }
        if (_hook is not null)
        {
            await _hook.StopAsync().ConfigureAwait(false);
        }
        if (_device is not null)
        {
            await _device.DisposeAsync().ConfigureAwait(false);
        }
        _cts?.Dispose();
        (_log as IDisposable)?.Dispose();
    }

    private void OnKey(KeyEvent keyEvent) => _engine?.EnqueueKeyEvent(keyEvent);
}
