using RSS_II_RGB.Core.Layout;
using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.Core.Effects.Layers;

/// <summary>
/// Lights each pressed key and fades it out. Additive overlay — sits on top of
/// a base layer. Persistent (always listening); individual presses are transient.
/// </summary>
public sealed class KeypressFadeLayer : IEffectLayer
{
    private readonly struct Active
    {
        public Active(int keyIndex, TimeSpan start) { KeyIndex = keyIndex; Start = start; }
        public int KeyIndex { get; }
        public TimeSpan Start { get; }
    }

    private readonly List<Active> _active = new();
    private readonly Rgb _color;
    private readonly double _fadeSeconds;

    public KeypressFadeLayer(string id, Rgb color, double fadeSeconds = 0.6, int zOrder = 10)
    {
        Id = id;
        _color = color;
        _fadeSeconds = fadeSeconds;
        ZOrder = zOrder;
    }

    public string Id { get; }
    public int ZOrder { get; }
    public BlendMode Blend => BlendMode.Additive;
    public KeyMask Mask => KeyMask.All;
    public bool IsComplete => false;

    public void Render(Span<Rgb> target, in EffectContext ctx)
    {
        foreach (KeyHit h in ctx.NewHits)
        {
            if ((uint)h.KeyIndex < (uint)CoreConstants.LedCount)
            {
                _active.Add(new Active(h.KeyIndex, h.When));
            }
        }

        for (int k = _active.Count - 1; k >= 0; k--)
        {
            float frac = (float)(1.0 - (ctx.Elapsed - _active[k].Start).TotalSeconds / _fadeSeconds);
            if (frac <= 0f)
            {
                _active.RemoveAt(k);
                continue;
            }

            int idx = _active[k].KeyIndex;
            target[idx] = Compositor.Blend(target[idx], _color.Scale(frac), BlendMode.Max);
        }
    }
}

/// <summary>
/// Each key press spawns a ring that expands outward across the grid and fades.
/// Additive overlay. Distance is Euclidean in grid (row, col) space — an
/// approximation, since only grid (not pixel) coordinates are available.
/// </summary>
public sealed class RippleLayer : IEffectLayer
{
    private readonly struct Active
    {
        public Active(int row, int col, TimeSpan start) { Row = row; Col = col; Start = start; }
        public int Row { get; }
        public int Col { get; }
        public TimeSpan Start { get; }
    }

    private readonly List<Active> _active = new();
    private readonly Rgb _color;
    private readonly double _speed;       // grid units per second
    private readonly double _width;       // ring thickness, grid units
    private readonly double _fadeSeconds; // ring lifetime

    public RippleLayer(string id, Rgb color, double speedGridPerSec = 12.0, double width = 1.2,
                       double fadeSeconds = 0.8, int zOrder = 20)
    {
        Id = id;
        _color = color;
        _speed = speedGridPerSec;
        _width = width;
        _fadeSeconds = fadeSeconds;
        ZOrder = zOrder;
    }

    public string Id { get; }
    public int ZOrder { get; }
    public BlendMode Blend => BlendMode.Additive;
    public KeyMask Mask => KeyMask.All;
    public bool IsComplete => false;

    public void Render(Span<Rgb> target, in EffectContext ctx)
    {
        foreach (KeyHit h in ctx.NewHits)
        {
            if ((uint)h.KeyIndex < (uint)CoreConstants.LedCount)
            {
                ref readonly LedKey key = ref ScopeIILayout.ByIndex(h.KeyIndex);
                _active.Add(new Active(key.Row, key.Col, h.When));
            }
        }

        for (int k = _active.Count - 1; k >= 0; k--)
        {
            double age = (ctx.Elapsed - _active[k].Start).TotalSeconds;
            double life = 1.0 - age / _fadeSeconds;
            if (life <= 0)
            {
                _active.RemoveAt(k);
                continue;
            }

            double radius = age * _speed;
            int row0 = _active[k].Row;
            int col0 = _active[k].Col;

            for (int i = 0; i < target.Length; i++)
            {
                ref readonly LedKey key = ref ScopeIILayout.ByIndex(i);
                double dr = key.Row - row0;
                double dc = key.Col - col0;
                double dist = Math.Sqrt(dr * dr + dc * dc);
                double ring = 1.0 - Math.Abs(dist - radius) / _width;
                if (ring <= 0)
                {
                    continue;
                }

                float intensity = (float)(ring * life);
                target[i] = Compositor.Blend(target[i], _color.Scale(intensity), BlendMode.Additive);
            }
        }
    }
}
