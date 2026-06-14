using RSS_II_RGB.Core.Layout;
using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.Core.Effects.Layers;

/// <summary>Fills its masked keys with one colour. Doubles as a master-brightness
/// layer when given <see cref="BlendMode.Multiply"/> and a grey colour.</summary>
public sealed class SolidLayer : IEffectLayer
{
    private readonly Rgb _color;

    public SolidLayer(string id, Rgb color, KeyMask mask, int zOrder = 0, BlendMode blend = BlendMode.Normal)
    {
        Id = id;
        _color = color;
        Mask = mask;
        ZOrder = zOrder;
        Blend = blend;
    }

    public string Id { get; }
    public int ZOrder { get; }
    public BlendMode Blend { get; }
    public KeyMask Mask { get; }
    public bool IsComplete => false;

    public void Render(Span<Rgb> target, in EffectContext ctx) => target.Fill(_color);
}

/// <summary>Pulses one colour up and down with a sine envelope.</summary>
public sealed class BreathingLayer : IEffectLayer
{
    private readonly Rgb _color;
    private readonly double _periodSeconds;

    public BreathingLayer(string id, Rgb color, KeyMask mask, double periodSeconds = 3.0, int zOrder = 0)
    {
        Id = id;
        _color = color;
        Mask = mask;
        _periodSeconds = periodSeconds;
        ZOrder = zOrder;
    }

    public string Id { get; }
    public int ZOrder { get; }
    public BlendMode Blend => BlendMode.Normal;
    public KeyMask Mask { get; }
    public bool IsComplete => false;

    public void Render(Span<Rgb> target, in EffectContext ctx)
    {
        double phase = ctx.Elapsed.TotalSeconds / _periodSeconds * (2 * Math.PI);
        float brightness = (float)((Math.Sin(phase) + 1.0) / 2.0);
        target.Fill(_color.Scale(brightness));
    }
}

/// <summary>A rainbow spread across the keys in render order, scrolling over time.</summary>
public sealed class RainbowLayer : IEffectLayer
{
    private readonly double _speed;  // hue cycles per second
    private readonly double _spread; // hue cycles across the whole keyboard

    public RainbowLayer(string id, KeyMask mask, double speed = 0.2, double spread = 1.0, int zOrder = 0)
    {
        Id = id;
        Mask = mask;
        _speed = speed;
        _spread = spread;
        ZOrder = zOrder;
    }

    public string Id { get; }
    public int ZOrder { get; }
    public BlendMode Blend => BlendMode.Normal;
    public KeyMask Mask { get; }
    public bool IsComplete => false;

    public void Render(Span<Rgb> target, in EffectContext ctx)
    {
        double t = ctx.Elapsed.TotalSeconds;
        for (int i = 0; i < target.Length; i++)
        {
            double hue = (double)i / CoreConstants.LedCount * _spread + t * _speed;
            target[i] = Rgb.FromHsv(hue, 1, 1);
        }
    }
}

/// <summary>
/// A rainbow that moves horizontally by physical column — the wave the firmware
/// can't render, done in software.
/// </summary>
public sealed class WaveLayer : IEffectLayer
{
    private readonly double _speed; // hue cycles per second

    public WaveLayer(string id, KeyMask mask, double speed = 0.25, int zOrder = 0)
    {
        Id = id;
        Mask = mask;
        _speed = speed;
        ZOrder = zOrder;
    }

    public string Id { get; }
    public int ZOrder { get; }
    public BlendMode Blend => BlendMode.Normal;
    public KeyMask Mask { get; }
    public bool IsComplete => false;

    public void Render(Span<Rgb> target, in EffectContext ctx)
    {
        double t = ctx.Elapsed.TotalSeconds;
        for (int i = 0; i < target.Length; i++)
        {
            int col = ScopeIILayout.ByIndex(i).Col;
            double hue = (double)col / ScopeIILayout.Cols + t * _speed;
            target[i] = Rgb.FromHsv(hue, 1, 1);
        }
    }
}
