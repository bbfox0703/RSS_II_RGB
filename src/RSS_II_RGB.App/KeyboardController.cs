using RSS_II_RGB.Core.Device;
using RSS_II_RGB.Core.Effects;
using RSS_II_RGB.Core.Effects.Layers;
using RSS_II_RGB.Core.Engine;
using RSS_II_RGB.Core.Input;
using RSS_II_RGB.Core.Logging;
using RSS_II_RGB.Core.Rendering;
using RSS_II_RGB.Windows;

namespace RSS_II_RGB.App;

/// <summary>
/// Owns the device, render engine, and keyboard hook. Translates a UI effect
/// choice into a layer stack and swaps it onto the running engine.
/// </summary>
internal sealed class KeyboardController : IAsyncDisposable
{
    private readonly ILogSink _log;
    private IKeyboardDevice? _device;
    private Win32KeyboardHook? _hook;
    private RenderEngine? _engine;
    private CancellationTokenSource? _cts;
    private Task? _engineTask;

    public KeyboardController(ILogSink log) => _log = log;

    public bool IsRunning { get; private set; }
    public string Firmware { get; private set; } = "";
    public int LayoutId { get; private set; } = -1;

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
        return true;
    }

    public void ApplyEffect(EffectChoice choice, Rgb color, double brightness)
    {
        if (_engine is null)
        {
            return;
        }

        var layers = new List<IEffectLayer>();
        switch (choice)
        {
            case EffectChoice.Off:
                layers.Add(new SolidLayer("base", Rgb.Black, KeyMask.All));
                break;
            case EffectChoice.Solid:
                layers.Add(new SolidLayer("base", color, KeyMask.All));
                break;
            case EffectChoice.Breathing:
                layers.Add(new BreathingLayer("base", color, KeyMask.All));
                break;
            case EffectChoice.Rainbow:
                layers.Add(new RainbowLayer("base", KeyMask.All));
                break;
            case EffectChoice.Wave:
                layers.Add(new WaveLayer("base", KeyMask.All));
                break;
            case EffectChoice.Reactive:
                layers.Add(new SolidLayer("base", new Rgb(0, 0, 20), KeyMask.All, zOrder: 0));
                layers.Add(new KeypressFadeLayer("fade", Rgb.White, fadeSeconds: 0.6, zOrder: 10));
                layers.Add(new RippleLayer("ripple", new Rgb(0, 180, 255), speedGridPerSec: 14,
                                           width: 1.3, fadeSeconds: 0.8, zOrder: 20));
                break;
        }

        // Master brightness as a top Multiply layer (scales every effect uniformly).
        if (brightness < 0.999)
        {
            byte b = (byte)Math.Clamp(brightness * 255.0, 0, 255);
            layers.Add(new SolidLayer("master-brightness", new Rgb(b, b, b), KeyMask.All,
                                      zOrder: 1000, blend: BlendMode.Multiply));
        }

        _engine.SetEffectLayers(layers);
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
