using NAudio.CoreAudioApi;
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
/// The capture self-heals: when Windows invalidates the render device (idle/sleep
/// or hibernate wake, a USB sound card whose power is toggled, or a default-device
/// change) NAudio's capture thread stops and raises <see cref="WasapiLoopbackCapture.RecordingStopped"/>.
/// We catch that, go dark, and rebuild the capture from <see cref="Poll"/> with a
/// short backoff — re-reading the (possibly new) default device and sample rate —
/// so audio reactivity returns on its own without an app restart.
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
    private const long RetryBackoffMs = 1000; // wait this long between (re)connect attempts

    private readonly object _gate = new();        // guards _bands
    private readonly object _captureLock = new();  // serialises capture create/teardown
    private readonly float[] _accum = new float[FftSize];
    private readonly double[] _peaks = new double[BandCount];    // per-band adaptive peak
    private readonly double[] _smoothed = new double[BandCount]; // temporally smoothed output
    private double[] _bands = new double[BandCount];
    private int _accumPos;
    private int _sampleRate = 48000;
    private WasapiLoopbackCapture? _capture;
    private volatile bool _capturing;
    private volatile bool _disposed;
    private long _nextRetryTick; // Environment.TickCount64 before which we won't retry

    public void Start() => TryStartCapture();

    public IEnumerable<SensorSample> Poll()
    {
        // Watchdog: if the capture is down (failed start, or a device that went away),
        // rebuild it once the backoff has elapsed. Poll runs ~30 Hz while the app is up.
        if (!_capturing && Environment.TickCount64 >= Volatile.Read(ref _nextRetryTick))
        {
            TryStartCapture();
        }

        double[] copy;
        lock (_gate)
        {
            copy = (double[])_bands.Clone();
        }
        yield return new SensorSample(SensorKind.AudioBands, copy, Environment.TickCount64);
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

            DisposeCapture(); // tear down any previous (possibly dead) instance first
            try
            {
                var capture = new WasapiLoopbackCapture();
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
                _capturing = false;
                Volatile.Write(ref _nextRetryTick, Environment.TickCount64 + RetryBackoffMs);
            }
        }
    }

    // The capture thread stopped — device lost (sleep/wake, USB power cycle, default
    // change) or errored. Go dark and let Poll rebuild it. Disposal happens in
    // TryStartCapture (disposing from this handler's own thread can deadlock NAudio).
    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }
        _capturing = false;
        Volatile.Write(ref _nextRetryTick, Environment.TickCount64 + RetryBackoffMs);
        lock (_gate)
        {
            _bands = new double[BandCount]; // fade to black rather than freeze on the last frame
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
        _capture = null;
        _capturing = false;
        if (capture is null)
        {
            return;
        }
        capture.DataAvailable -= OnDataAvailable;
        capture.RecordingStopped -= OnRecordingStopped;
        try { capture.StopRecording(); } catch { /* device may already be gone */ }
        try { capture.Dispose(); } catch { /* ignore */ }
    }

    public void Dispose()
    {
        _disposed = true;
        lock (_captureLock)
        {
            DisposeCapture();
        }
    }
}
