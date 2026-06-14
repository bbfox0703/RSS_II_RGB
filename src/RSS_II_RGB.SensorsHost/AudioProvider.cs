using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using RSS_II_RGB.Core.Ipc;

namespace RSS_II_RGB.SensorsHost;

/// <summary>
/// Captures system output (WASAPI loopback — no admin needed) and turns it into
/// <see cref="BandCount"/> frequency-band magnitudes via FFT. Bands stay at zero
/// when nothing is playing.
/// </summary>
internal sealed class AudioProvider : ISensorProvider
{
    private const int FftSize = 1024;
    private const int FftPow = 10;        // log2(FftSize)
    private const int BandCount = 24;     // one per keyboard column
    private const double FMinHz = 30;     // ignore sub-bass rumble below this
    private const double FMaxHz = 16000;  // treble cap (most adults can't hear above ~15k)
    private const double SilenceRms = 0.002; // overall loudness below this = black (no flicker)
    private const double PeakFloor = 0.02;   // per-band gain floor
    private const double PeakDecay = 0.99;   // adaptive-gain decay per FFT frame

    private readonly object _gate = new();
    private readonly float[] _accum = new float[FftSize];
    private readonly double[] _peaks = new double[BandCount]; // per-band adaptive peak
    private double[] _bands = new double[BandCount];
    private int _accumPos;
    private int _sampleRate = 48000;
    private WasapiLoopbackCapture? _capture;

    public void Start()
    {
        Array.Fill(_peaks, PeakFloor);
        try
        {
            _capture = new WasapiLoopbackCapture();
            _sampleRate = _capture.WaveFormat.SampleRate;
            _capture.DataAvailable += OnDataAvailable;
            _capture.StartRecording();
        }
        catch
        {
            _capture = null; // no render device — bands stay zero
        }
    }

    public IEnumerable<SensorSample> Poll()
    {
        double[] copy;
        lock (_gate)
        {
            copy = (double[])_bands.Clone();
        }
        yield return new SensorSample(SensorKind.AudioBands, copy, Environment.TickCount64);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        WaveFormat? format = _capture?.WaveFormat;
        if (format is null)
        {
            return;
        }

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
            for (int b = 0; b < BandCount; b++)
            {
                _peaks[b] = Math.Max(_peaks[b] * PeakDecay, PeakFloor);
            }
            lock (_gate)
            {
                _bands = bands;
            }
            return;
        }

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

        lock (_gate)
        {
            _bands = bands;
        }
    }

    public void Dispose()
    {
        try
        {
            _capture?.StopRecording();
        }
        catch
        {
            // ignore
        }
        _capture?.Dispose();
        _capture = null;
    }
}
