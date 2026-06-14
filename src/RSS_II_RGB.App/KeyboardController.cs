using RSS_II_RGB.Core.Device;
using RSS_II_RGB.Core.Effects;
using RSS_II_RGB.Core.Effects.Layers;
using RSS_II_RGB.Core.Engine;
using RSS_II_RGB.Core.Input;
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

    public KeyboardController(ILogSink log, SensorState sensors)
    {
        _log = log;
        _sensors = sensors;
    }

    public bool IsRunning { get; private set; }
    public string Firmware { get; private set; } = "";
    public int LayoutId { get; private set; } = -1;
    public IReadOnlyList<Zone> Zones => _zones;

    /// <summary>Brightness multiplier for the audio visualiser (set before SetGlobalEffect).</summary>
    public double AudioSensitivity { get; set; } = 1.5;

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

        DeviceInfo info = await _device.ReadInfoAsync();
        Firmware = info.FirmwareVersion;
        LayoutId = info.LayoutId;

        _engine = new RenderEngine(_device, new Compositor(), _log);
        _hook = new Win32KeyboardHook();
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

    private void Rebuild()
    {
        if (_engine is null)
        {
            return;
        }

        var layers = new List<IEffectLayer>();
        AddEffectLayers(layers, "global", _globalEffect, _globalColor, KeyMask.All, baseZ: 0);

        int z = 100;
        foreach (Zone zone in _zones)
        {
            if (zone.KeyIndices.Count == 0)
            {
                continue;
            }
            KeyMask mask = KeyMask.FromIndices(zone.KeyIndices is int[] arr ? arr : zone.KeyIndices.ToArray());
            AddEffectLayers(layers, $"zone-{z}", zone.Effect, zone.Color, mask, baseZ: z);
            z += 100;
        }

        // System-metric bars overlay on top of the global effect and zones.
        if (ShowMetrics)
        {
            layers.Add(new MetricOverlayLayer("metrics", _sensors, MetricLayouts.Build(MetricLayout),
                                              PercentThresholds, TempThresholds, zOrder: 500));
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

    // Maps an effect choice to its layer(s), masked to the given keys. Reactive is
    // global-only (its overlays don't yet honour a mask), so a Reactive zone falls
    // back to its dim base.
    private void AddEffectLayers(List<IEffectLayer> layers, string id, EffectChoice effect,
                                 Rgb color, KeyMask mask, int baseZ)
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
            case EffectChoice.Reactive:
                layers.Add(new SolidLayer($"{id}-base", new Rgb(0, 0, 20), mask, baseZ));
                layers.Add(new KeypressFadeLayer($"{id}-fade", Rgb.White, fadeSeconds: 0.6, zOrder: baseZ + 10));
                layers.Add(new RippleLayer($"{id}-ripple", new Rgb(0, 180, 255), speedGridPerSec: 14,
                                           width: 1.3, fadeSeconds: 0.8, zOrder: baseZ + 20));
                break;
            case EffectChoice.CpuTemp:
                layers.Add(new TempIndicatorLayer(id, _sensors, gpu: false, mask, zOrder: baseZ));
                break;
            case EffectChoice.GpuTemp:
                layers.Add(new TempIndicatorLayer(id, _sensors, gpu: true, mask, zOrder: baseZ));
                break;
            case EffectChoice.Audio:
                layers.Add(new AudioReactiveLayer(id, _sensors, mask, sensitivity: AudioSensitivity, zOrder: baseZ));
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
