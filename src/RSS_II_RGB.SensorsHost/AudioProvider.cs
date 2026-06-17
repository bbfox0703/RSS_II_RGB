using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Dsp;
using NAudio.Wave;
using RSS_II_RGB.Core;
using RSS_II_RGB.Core.Ipc;

namespace RSS_II_RGB.SensorsHost;

/// <summary>
/// Captures system output (WASAPI loopback — no admin needed) and turns it into
/// <see cref="BandCount"/> frequency-band magnitudes via FFT. Bands stay at zero
/// when nothing is playing.
///
/// The capture self-heals across idle/sleep, default-device changes, and USB sound
/// cards whose power is toggled. Three independent signals drive a rebuild — no
/// single one is reliable on its own:
///   1. <see cref="WasapiLoopbackCapture.RecordingStopped"/> — fires when Windows
///      hard-invalidates the device (AUDCLNT_E_DEVICE_INVALIDATED).
///   2. <see cref="IMMNotificationClient"/> — fires when the default render device
///      changes, or our bound device cycles state (USB selective suspend on monitor
///      blank powers a USB DAC down/up: Active→Unplugged→Active). In that case the
///      loopback client silently zombies — it neither errors nor receives data — so
///      RecordingStopped never fires and this is the only signal we get.
///   3. A 1 Hz poll in <see cref="Poll"/> comparing the live default render endpoint
///      ID to the one we bound to — a backstop for any missed notification.
/// Any of these flips <see cref="_capturing"/> off; <see cref="Poll"/> then rebuilds
/// the capture against the (possibly new) default device, so audio reactivity
/// returns on its own without an app restart.
/// </summary>
internal sealed class AudioProvider : ISensorProvider
{
    private const int FftSize = 1024;
    private const int FftPow = 10;        // log2(FftSize)
    private const int BandCount = CoreConstants.AudioBandCount; // one per keyboard column
    private const double FMinHz = CoreConstants.AudioMinHz;     // ignore sub-bass rumble below this
    private const double FMaxHz = CoreConstants.AudioMaxHz;     // treble cap (most adults can't hear above ~15k)
    private const double SilenceRms = 0.002; // overall loudness below this = black (no flicker)
    private const double PeakFloor = 0.02;   // per-band gain floor
    private const double PeakDecay = 0.99;   // adaptive-gain decay per FFT frame
    private const double Attack = 0.55;      // temporal smoothing on a rising band (higher = calmer)
    private const double Release = 0.9;      // temporal smoothing on a falling band (~0.45s fade-out)
    private const long RetryBackoffMs = 1000;       // wait this long between (re)connect attempts
    private const long HealthCheckIntervalMs = 1000; // throttle for the default-endpoint poll

    private readonly object _gate = new();        // guards _bands
    private readonly object _captureLock = new();  // serialises capture create/teardown
    private readonly float[] _accum = new float[FftSize];
    private readonly double[] _peaks = new double[BandCount];    // per-band adaptive peak
    private readonly double[] _smoothed = new double[BandCount]; // temporally smoothed output
    private double[] _bands = new double[BandCount];
    private int _accumPos;
    private int _sampleRate = 48000;
    private WasapiLoopbackCapture? _capture;
    private MMDevice? _captureDevice;            // render endpoint the capture is bound to (disposed with _capture)
    private MMDeviceEnumerator? _enumerator;     // long-lived; hosts the device-change callback
    private DeviceNotificationClient? _notifications;
    private volatile string? _boundDeviceId;     // ID of _captureDevice, for the watchdog comparison
    private volatile bool _capturing;
    private volatile bool _disposed;
    private long _nextRetryTick;       // Environment.TickCount64 before which we won't retry
    private long _nextHealthCheckTick; // Environment.TickCount64 of the next default-endpoint poll

    public void Start()
    {
        lock (_captureLock)
        {
            if (_disposed)
            {
                return;
            }
            EnsureEnumerator();
        }
        TryStartCapture();
    }

    public IEnumerable<SensorSample> Poll()
    {
        long now = Environment.TickCount64;

        // Watchdog: if the capture is down (failed start, or a device that went away),
        // rebuild it once the backoff has elapsed. Poll runs ~30 Hz while the app is up.
        if (!_capturing)
        {
            if (now >= Volatile.Read(ref _nextRetryTick))
            {
                TryStartCapture();
            }
        }
        else if (now >= _nextHealthCheckTick)
        {
            // Backstop for the silent-zombie case (USB suspend) where neither
            // RecordingStopped nor a device notification arrives: confirm we're still
            // bound to the live default render endpoint, otherwise force a rebuild.
            _nextHealthCheckTick = now + HealthCheckIntervalMs;
            VerifyStillOnDefaultDevice();
        }

        double[] copy;
        lock (_gate)
        {
            copy = (double[])_bands.Clone();
        }
        yield return new SensorSample(SensorKind.AudioBands, copy, now);
    }

    // Create the device enumerator once and register for default-device / device-state
    // change notifications. Notifications are best-effort: if registration fails we still
    // have RecordingStopped and the 1 Hz endpoint poll. Call under _captureLock.
    private void EnsureEnumerator()
    {
        _enumerator ??= new MMDeviceEnumerator();
        if (_notifications is null)
        {
            try
            {
                var client = new DeviceNotificationClient(this);
                _enumerator.RegisterEndpointNotificationCallback(client);
                _notifications = client;
            }
            catch
            {
                _notifications = null;
            }
        }
    }

    // (Re)create the loopback capture against the current default render device.
    private void TryStartCapture()
    {
        lock (_captureLock)
        {
            if (_disposed)
            {
                return;
            }

            EnsureEnumerator();
            DisposeCapture(); // tear down any previous (possibly dead) instance first
            try
            {
                // Bind explicitly to the default render endpoint so we can remember its ID
                // and detect later when the system default moves out from under us.
                MMDevice device = _enumerator!.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
                var capture = new WasapiLoopbackCapture(device);
                _captureDevice = device;
                _boundDeviceId = device.ID;
                _sampleRate = capture.WaveFormat.SampleRate;
                capture.DataAvailable += OnDataAvailable;
                capture.RecordingStopped += OnRecordingStopped;
                capture.StartRecording();
                _capture = capture;
                ResetAnalysis();
                _capturing = true;
            }
            catch
            {
                // No render device right now (e.g. the USB card is still powering up).
                // Stay dark and try again after the backoff.
                _capture = null;
                _captureDevice = null;
                _boundDeviceId = null;
                _capturing = false;
                Volatile.Write(ref _nextRetryTick, Environment.TickCount64 + RetryBackoffMs);
            }
        }
    }

    // The capture thread stopped — device hard-invalidated or errored. Go dark and let
    // Poll rebuild it. Disposal happens in TryStartCapture (disposing from this handler's
    // own thread can deadlock NAudio).
    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }
        RequestRestart();
    }

    // Flip the capture off and ask Poll to rebuild ASAP. Safe to call from any thread
    // (the capture thread, the COM notification thread, or the Poll thread): it only
    // touches volatile flags and _bands, never COM or _captureLock, so it can't deadlock.
    private void RequestRestart()
    {
        _capturing = false;
        Volatile.Write(ref _nextRetryTick, 0); // rebuild on the next Poll, skip the backoff
        lock (_gate)
        {
            _bands = new double[BandCount]; // fade to black rather than freeze on the last frame
        }
    }

    // 1 Hz backstop: if the system default render endpoint no longer matches the device
    // our capture is bound to (or there's no usable default), force a rebuild.
    private void VerifyStillOnDefaultDevice()
    {
        string? bound = _boundDeviceId;
        MMDeviceEnumerator? enumerator = _enumerator;
        if (bound is null || enumerator is null)
        {
            return; // bound without a known ID — nothing to compare against
        }

        try
        {
            using MMDevice current = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            if (!string.Equals(current.ID, bound, StringComparison.OrdinalIgnoreCase))
            {
                RequestRestart(); // default render device changed under us
            }
        }
        catch
        {
            RequestRestart(); // no usable default render endpoint right now
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (sender is not WasapiLoopbackCapture capture)
        {
            return;
        }

        WaveFormat format = capture.WaveFormat;
        int channels = format.Channels;
        int bytesPerSample = format.BitsPerSample / 8; // 4 (IEEE float)
        int frameSize = bytesPerSample * channels;
        if (frameSize == 0)
        {
            return;
        }

        int frames = e.BytesRecorded / frameSize;
        for (int i = 0; i < frames; i++)
        {
            int baseOffset = i * frameSize;
            float sum = 0;
            for (int c = 0; c < channels; c++)
            {
                sum += BitConverter.ToSingle(e.Buffer, baseOffset + c * bytesPerSample);
            }

            _accum[_accumPos++] = sum / channels;
            if (_accumPos >= FftSize)
            {
                Compute();
                _accumPos = 0;
            }
        }
    }

    private void Compute()
    {
        var bands = new double[BandCount];

        // Loudness gate: if the window is essentially silent, output black and let
        // the per-band gains decay. This rejects idle-line noise cleanly.
        double energy = 0;
        for (int i = 0; i < FftSize; i++)
        {
            energy += _accum[i] * _accum[i];
        }
        double rms = Math.Sqrt(energy / FftSize);
        if (rms < SilenceRms)
        {
            // Silent: target stays 0 (the smoothing below fades the bands out), and the
            // per-band gains decay.
            for (int b = 0; b < BandCount; b++)
            {
                _peaks[b] = Math.Max(_peaks[b] * PeakDecay, PeakFloor);
            }
        }
        else
        {
            var buffer = new Complex[FftSize];
            for (int i = 0; i < FftSize; i++)
            {
                double window = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (FftSize - 1))); // Hann
                buffer[i].X = (float)(_accum[i] * window);
                buffer[i].Y = 0;
            }
            FastFourierTransform.FFT(true, FftPow, buffer);

            int half = FftSize / 2;
            double binHz = _sampleRate / (double)FftSize;
            double ratio = FMaxHz / FMinHz;
            for (int b = 0; b < BandCount; b++)
            {
                // Logarithmic (constant-ratio, octave-like) band edges so bass, mid and
                // treble each get a fair share of the keyboard width.
                double freqLo = FMinHz * Math.Pow(ratio, (double)b / BandCount);
                double freqHi = FMinHz * Math.Pow(ratio, (double)(b + 1) / BandCount);
                int lo = Math.Max(1, (int)(freqLo / binHz));
                int hi = Math.Min(half - 1, (int)(freqHi / binHz));
                if (hi < lo)
                {
                    hi = lo;
                }

                double sum = 0;
                int count = 0;
                for (int k = lo; k <= hi; k++)
                {
                    sum += Math.Sqrt(buffer[k].X * buffer[k].X + buffer[k].Y * buffer[k].Y);
                    count++;
                }

                double mag = count > 0 ? sum / count : 0;

                // Per-band auto-gain: each band fills its own range, so the whole keyboard
                // responds even though treble carries far less energy than bass.
                _peaks[b] = Math.Max(Math.Max(_peaks[b] * PeakDecay, mag), PeakFloor);
                bands[b] = Math.Clamp(Math.Pow(mag / _peaks[b], 0.7), 0, 1); // normalise + mild gamma
            }
        }

        // Temporal smoothing (fast attack, slow release): kills per-frame flicker and
        // gives a smooth fade-out when the sound drops.
        var emit = new double[BandCount];
        for (int b = 0; b < BandCount; b++)
        {
            double factor = bands[b] > _smoothed[b] ? Attack : Release;
            _smoothed[b] = _smoothed[b] * factor + bands[b] * (1 - factor);
            emit[b] = _smoothed[b];
        }

        lock (_gate)
        {
            _bands = emit;
        }
    }

    // Reset the analysis state for a fresh capture (call under _captureLock).
    private void ResetAnalysis()
    {
        Array.Fill(_peaks, PeakFloor);
        Array.Clear(_smoothed);
        _accumPos = 0;
        lock (_gate)
        {
            _bands = new double[BandCount];
        }
    }

    // Detach and release the current capture (call under _captureLock). Unhooking the
    // events first means our own teardown never re-enters OnRecordingStopped.
    private void DisposeCapture()
    {
        WasapiLoopbackCapture? capture = _capture;
        MMDevice? device = _captureDevice;
        _capture = null;
        _captureDevice = null;
        _capturing = false;
        if (capture is not null)
        {
            capture.DataAvailable -= OnDataAvailable;
            capture.RecordingStopped -= OnRecordingStopped;
            try { capture.StopRecording(); } catch { /* device may already be gone */ }
            try { capture.Dispose(); } catch { /* ignore */ }
        }
        try { device?.Dispose(); } catch { /* ignore */ }
    }

    public void Dispose()
    {
        _disposed = true;
        lock (_captureLock)
        {
            if (_enumerator is not null && _notifications is not null)
            {
                try { _enumerator.UnregisterEndpointNotificationCallback(_notifications); } catch { /* ignore */ }
            }
            _notifications = null;
            DisposeCapture();
            try { _enumerator?.Dispose(); } catch { /* ignore */ }
            _enumerator = null;
        }
    }

    // Listens for default-device and device-state changes from the audio stack. Callbacks
    // arrive on a COM thread, so they only flip flags via RequestRestart — never touch
    // COM or _captureLock — and the actual rebuild happens on the Poll thread.
    private sealed class DeviceNotificationClient : IMMNotificationClient
    {
        private readonly AudioProvider _owner;

        public DeviceNotificationClient(AudioProvider owner) => _owner = owner;

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            // The system's default output moved (or a resumed USB DAC re-took default).
            if (flow == DataFlow.Render)
            {
                _owner.RequestRestart();
            }
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            // Our bound device cycled power (USB selective suspend → resume), or some device
            // (re)appeared while we were dark. Either way, rebuild to re-bind to a live endpoint.
            if (!_owner._capturing
                || string.Equals(deviceId, _owner._boundDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                _owner.RequestRestart();
            }
        }

        public void OnDeviceRemoved(string deviceId)
        {
            if (string.Equals(deviceId, _owner._boundDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                _owner.RequestRestart();
            }
        }

        public void OnDeviceAdded(string pwstrDeviceId)
        {
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
        }
    }
}
