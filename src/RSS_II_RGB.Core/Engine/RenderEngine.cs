using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using RSS_II_RGB.Core.Device;
using RSS_II_RGB.Core.Effects;
using RSS_II_RGB.Core.Input;
using RSS_II_RGB.Core.Layout;
using RSS_II_RGB.Core.Logging;
using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.Core.Engine;

/// <summary>
/// The software render loop. On a background task it drains key events, composites
/// the layer stack into a frame, and streams it to the device — paced to a target
/// FPS (the device write is the real throttle). Key events arrive lock-free from
/// the hook thread.
/// </summary>
public sealed class RenderEngine : IAsyncDisposable
{
    private readonly IKeyboardDevice _device;
    private readonly Compositor _compositor;
    private readonly ILogSink _log;
    private readonly KeyboardProfile _profile;
    private readonly int _targetFps;

    private readonly LedFrame _frame;
    private readonly ConcurrentQueue<KeyEvent> _keyQueue = new();
    private readonly List<KeyHit> _hits = new();
    private readonly bool[] _heldKeys; // per-LED down state, to drop OS auto-repeat

    // Set from any thread; applied to the compositor on the engine thread.
    private volatile IReadOnlyList<IEffectLayer>? _pendingLayers;

    public RenderEngine(IKeyboardDevice device, Compositor compositor, ILogSink log,
                        KeyboardProfile profile, int targetFps = CoreConstants.DefaultTargetFps)
    {
        _device = device;
        _compositor = compositor;
        _log = log;
        _profile = profile;
        _frame = new LedFrame(profile.LedCount);
        _heldKeys = new bool[profile.LedCount];
        _targetFps = Math.Clamp(targetFps, 1, 240);
    }

    public Compositor Compositor => _compositor;

    /// <summary>Frames streamed so far (diagnostics / FPS checks).</summary>
    public long FramesRendered { get; private set; }

    /// <summary>Thread-safe; called from the hook thread.</summary>
    public void EnqueueKeyEvent(in KeyEvent keyEvent) => _keyQueue.Enqueue(keyEvent);

    /// <summary>
    /// Atomically swap the active effect stack. Thread-safe: the UI thread sets
    /// the pending list; the engine thread installs it at the next tick, so the
    /// compositor is only ever mutated on the engine thread.
    /// </summary>
    public void SetEffectLayers(IReadOnlyList<IEffectLayer> layers) => _pendingLayers = layers;

    public async Task RunAsync(CancellationToken ct)
    {
        var period = TimeSpan.FromSeconds(1.0 / _targetFps);
        var clock = Stopwatch.StartNew();
        TimeSpan previous = TimeSpan.Zero;

        _log.Log(LogLevel.Info, $"Render loop started ({_targetFps} FPS target).");

        while (!ct.IsCancellationRequested)
        {
            TimeSpan now = clock.Elapsed;
            TimeSpan delta = now - previous;
            previous = now;

            ApplyPendingLayers();
            DrainKeyEvents(now);

            var ctx = new EffectContext(now, delta, CollectionsMarshal.AsSpan(_hits), _profile);
            _compositor.Compose(_frame, ctx);

            try
            {
                await _device.WriteFrameAsync(_frame, ct).ConfigureAwait(false);
                FramesRendered++;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failed write means the device is gone (unplugged). Log once and
                // stop the loop; the controller treats the engine exit as a lost
                // device and re-enters its detect/reconnect poll.
                _log.Log(LogLevel.Warning, "Keyboard write failed — device likely disconnected.", ex);
                break;
            }

            TimeSpan spent = clock.Elapsed - now;
            if (spent < period)
            {
                try
                {
                    await Task.Delay(period - spent, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _log.Log(LogLevel.Info, $"Render loop stopped after {FramesRendered} frames.");
    }

    private void ApplyPendingLayers()
    {
        IReadOnlyList<IEffectLayer>? pending = _pendingLayers;
        if (pending is null)
        {
            return;
        }
        _pendingLayers = null;

        _compositor.Clear();
        foreach (IEffectLayer layer in pending)
        {
            _compositor.Add(layer);
        }
    }

    private void DrainKeyEvents(TimeSpan now)
    {
        _hits.Clear();
        while (_keyQueue.TryDequeue(out KeyEvent ke))
        {
            if ((uint)ke.KeyIndex >= (uint)_heldKeys.Length)
            {
                continue; // no LED for this key
            }

            if (ke.IsDown)
            {
                // The first press becomes a reactive hit; the OS sends repeated
                // key-downs while a key is held (auto-repeat / continuous input),
                // which we drop so a held key doesn't keep re-triggering the effect.
                if (!_heldKeys[ke.KeyIndex])
                {
                    _heldKeys[ke.KeyIndex] = true;
                    _hits.Add(new KeyHit(ke.KeyIndex, now));
                }
            }
            else
            {
                _heldKeys[ke.KeyIndex] = false; // released — the next press hits again
            }
        }
    }

    public ValueTask DisposeAsync() => _device.DisposeAsync();
}
