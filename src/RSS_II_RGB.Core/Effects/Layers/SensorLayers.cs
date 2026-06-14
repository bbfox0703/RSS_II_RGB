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
