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
/// Black when nothing is playing.
/// </summary>
public sealed class AudioReactiveLayer : IEffectLayer
{
    private readonly SensorState _state;
    private readonly double[] _bands = new double[64];
    private readonly double _sensitivity;

    public AudioReactiveLayer(string id, SensorState state, KeyMask mask, double sensitivity = 1.0, int zOrder = 0)
    {
        Id = id;
        _state = state;
        Mask = mask;
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
        if (n <= 0)
        {
            target.Fill(Rgb.Black);
            return;
        }

        for (int i = 0; i < target.Length; i++)
        {
            int col = ScopeIILayout.ByIndex(i).Col;
            int band = Math.Clamp(col * n / ScopeIILayout.Cols, 0, n - 1);
            double magnitude = Math.Clamp(_bands[band] * _sensitivity, 0, 1);
            double hue = (double)band / n * 0.85; // low freq red ... high freq violet
            target[i] = Rgb.FromHsv(hue, 1, magnitude);
        }
    }
}

