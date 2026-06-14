using RSS_II_RGB.Core.Layout;
using RSS_II_RGB.Core.Rendering;
using RSS_II_RGB.Core.Sensors;

namespace RSS_II_RGB.Core.Effects.Layers;

/// <summary>
/// Tints the masked keys by a temperature: cold = blue, hot = red (HSV hue 240°→0°).
/// Reads the live <see cref="SensorState"/>; dim grey until data arrives.
/// </summary>
public sealed class TempIndicatorLayer : IEffectLayer
{
    private readonly SensorState _state;
    private readonly bool _gpu;
    private readonly double _minC;
    private readonly double _maxC;

    public TempIndicatorLayer(string id, SensorState state, bool gpu, KeyMask mask,
                              double minCelsius = 30, double maxCelsius = 85, int zOrder = 0)
    {
        Id = id;
        _state = state;
        _gpu = gpu;
        Mask = mask;
        _minC = minCelsius;
        _maxC = maxCelsius;
        ZOrder = zOrder;
    }

    public string Id { get; }
    public int ZOrder { get; }
    public BlendMode Blend => BlendMode.Normal;
    public KeyMask Mask { get; }
    public bool IsComplete => false;

    public void Render(Span<Rgb> target, in EffectContext ctx)
    {
        double temp = _gpu ? _state.GpuTemp : _state.CpuTemp;

        Rgb color;
        if (double.IsNaN(temp))
        {
            color = new Rgb(40, 40, 40); // no data yet
        }
        else
        {
            double f = Math.Clamp((temp - _minC) / (_maxC - _minC), 0, 1);
            double hue = (1 - f) * (240.0 / 360.0); // blue (cold) -> red (hot)
            color = Rgb.FromHsv(hue, 1, 1);
        }

        target.Fill(color);
    }
}

/// <summary>
/// A spectrum visualiser: maps audio frequency bands across the keyboard columns
/// (low frequencies on the left). Hue tracks the band, brightness its magnitude.
/// Black when nothing is playing. The blend mode is configurable: a zone uses
/// <see cref="BlendMode.Normal"/> (opaque on its keys), while a global overlay
/// uses <see cref="BlendMode.Additive"/> so silent frames let the base show through.
/// </summary>
public sealed class AudioReactiveLayer : IEffectLayer
{
    private readonly SensorState _state;
    private readonly double[] _bands = new double[64];
    private readonly double _sensitivity;

    public AudioReactiveLayer(string id, SensorState state, KeyMask mask, double sensitivity = 1.0,
                              int zOrder = 0, BlendMode blend = BlendMode.Normal)
    {
        Id = id;
        _state = state;
        Mask = mask;
        _sensitivity = sensitivity;
        ZOrder = zOrder;
        Blend = blend;
    }

    public string Id { get; }
    public int ZOrder { get; }
    public BlendMode Blend { get; }
    public KeyMask Mask { get; }
    public bool IsComplete => false;

    public void Render(Span<Rgb> target, in EffectContext ctx)
    {
        int n = _state.CopyAudioBands(_bands);
        if (n <= 0)
        {
            target.Fill(Rgb.Black);
            return;
        }

        for (int i = 0; i < target.Length; i++)
        {
            int col = ctx.Layout.ByIndex(i).Col;
            int band = Math.Clamp(col * n / ctx.Layout.Cols, 0, n - 1);
            double magnitude = Math.Clamp(_bands[band] * _sensitivity, 0, 1);
            double hue = (double)band / n * 0.85; // low freq red ... high freq violet
            target[i] = Rgb.FromHsv(hue, 1, magnitude);
        }
    }
}

/// <summary>
/// Lights its whole masked area at a brightness driven by overall audio loudness
/// (the peak band). The colour is either a fixed colour or a slow time-cycling
/// rainbow. Black when silent.
/// </summary>
public sealed class AudioVolumeLayer : IEffectLayer
{
    private readonly SensorState _state;
    private readonly Rgb _color;
    private readonly bool _rainbow;
    private readonly double _sensitivity;
    private readonly double[] _bands = new double[64];

    public AudioVolumeLayer(string id, SensorState state, KeyMask mask, Rgb color, bool rainbow,
                            double sensitivity = 1.0, int zOrder = 0)
    {
        Id = id;
        _state = state;
        Mask = mask;
        _color = color;
        _rainbow = rainbow;
        _sensitivity = sensitivity;
        ZOrder = zOrder;
    }

    public string Id { get; }
    public int ZOrder { get; }
    public BlendMode Blend => BlendMode.Normal;
    public KeyMask Mask { get; }
    public bool IsComplete => false;

    public void Render(Span<Rgb> target, in EffectContext ctx)
    {
        int n = _state.CopyAudioBands(_bands);
        double volume = 0;
        for (int i = 0; i < n; i++)
        {
            volume = Math.Max(volume, _bands[i]);
        }
        volume = Math.Clamp(volume * _sensitivity, 0, 1);

        Rgb color = _rainbow
            ? Rgb.FromHsv(ctx.Elapsed.TotalSeconds * 0.2, 1, 1)
            : _color;

        target.Fill(color.Scale((float)volume));
    }
}

/// <summary>
/// Splits the keyboard into three horizontal regions — treble (top alpha row),
/// mid (home row), bass (bottom alpha row) — and lights each as a left-to-right
/// bar whose length tracks that band's energy (scaled by a per-region multiplier):
/// the louder the band, the more keys in its row light up (e.g. heavy bass fills
/// Left Shift → Right Shift). Uses <see cref="BlendMode.Over"/> so lit keys show
/// clearly over any base effect while unlit keys stay transparent. Colours:
/// bass = red, mid = green, treble = blue.
/// </summary>
public sealed class AudioBarsLayer : IEffectLayer
{
    private readonly SensorState _state;
    private readonly int[] _bass;   // each row's render indices, ordered left-to-right
    private readonly int[] _mid;
    private readonly int[] _treble;
    private readonly double _bassMul;
    private readonly double _midMul;
    private readonly double _trebleMul;
    private readonly double[] _bands = new double[CoreConstants.AudioBandCount];

    public AudioBarsLayer(string id, SensorState state, int[] bass, int[] mid, int[] treble,
                          double bassMultiplier = 1.0, double midMultiplier = 1.0, double trebleMultiplier = 1.0,
                          int zOrder = 0, BlendMode blend = BlendMode.Over)
    {
        Id = id;
        _state = state;
        _bass = bass;
        _mid = mid;
        _treble = treble;
        _bassMul = bassMultiplier;
        _midMul = midMultiplier;
        _trebleMul = trebleMultiplier;
        ZOrder = zOrder;
        Blend = blend;

        var all = new List<int>(bass.Length + mid.Length + treble.Length);
        all.AddRange(bass);
        all.AddRange(mid);
        all.AddRange(treble);
        Mask = KeyMask.FromIndices(all.ToArray());
    }

    public string Id { get; }
    public int ZOrder { get; }
    public BlendMode Blend { get; }
    public KeyMask Mask { get; }
    public bool IsComplete => false;

    public void Render(Span<Rgb> target, in EffectContext ctx)
    {
        int n = _state.CopyAudioBands(_bands);
        if (n <= 0)
        {
            return; // silent — additive black, base shows through
        }

        DrawBar(target, _bass, RegionLevel(n, CoreConstants.AudioMinHz, CoreConstants.AudioBassMaxHz) * _bassMul, hue: 0.00);   // red
        DrawBar(target, _mid, RegionLevel(n, CoreConstants.AudioBassMaxHz, CoreConstants.AudioMidMaxHz) * _midMul, hue: 0.33);  // green
        DrawBar(target, _treble, RegionLevel(n, CoreConstants.AudioMidMaxHz, CoreConstants.AudioMaxHz) * _trebleMul, hue: 0.66); // blue
    }

    private static void DrawBar(Span<Rgb> target, int[] keys, double level, double hue)
    {
        level = Math.Clamp(level, 0, 1);
        int lit = (int)Math.Round(level * keys.Length);
        if (lit <= 0)
        {
            return;
        }

        Rgb color = Rgb.FromHsv(hue, 1, 1);
        for (int i = 0; i < lit && i < keys.Length; i++)
        {
            int idx = keys[i];
            if ((uint)idx < (uint)target.Length)
            {
                target[idx] = color;
            }
        }
    }

    // Average magnitude of the bands whose geometric centre falls in [loHz, hiHz).
    private double RegionLevel(int n, double loHz, double hiHz)
    {
        double ratio = CoreConstants.AudioMaxHz / CoreConstants.AudioMinHz;
        double sum = 0;
        int count = 0;
        for (int b = 0; b < n; b++)
        {
            double centre = CoreConstants.AudioMinHz * Math.Pow(ratio, (b + 0.5) / n);
            if (centre >= loHz && centre < hiHz)
            {
                sum += _bands[b];
                count++;
            }
        }
        return count > 0 ? sum / count : 0;
    }
}

