using RSS_II_RGB.Core.Layout;
using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.Core.Effects;

/// <summary>
/// Holds the ordered layer stack and composites it into a frame each tick.
/// Renders each layer (z ascending) into a reused scratch buffer, then blends
/// it into the frame per the layer's mask + blend mode. Completed layers are
/// dropped after compositing.
/// </summary>
public sealed class Compositor
{
    private readonly List<IEffectLayer> _layers = new();
    private readonly Rgb[] _scratch;
    private bool _needsSort;

    /// <summary>Defaults to the Scope II LED count (used by tests).</summary>
    public Compositor() : this(ScopeIILayout.Profile.LedCount)
    {
    }

    public Compositor(int ledCount) => _scratch = new Rgb[ledCount];

    public IReadOnlyList<IEffectLayer> Layers => _layers;

    public void Add(IEffectLayer layer)
    {
        _layers.Add(layer);
        _needsSort = true;
    }

    public void Remove(string id) => _layers.RemoveAll(l => l.Id == id);

    public void Clear() => _layers.Clear();

    public void Compose(LedFrame frame, in EffectContext ctx)
    {
        if (_needsSort)
        {
            _layers.Sort(static (a, b) => a.ZOrder.CompareTo(b.ZOrder));
            _needsSort = false;
        }

        frame.Clear();
        Span<Rgb> framePixels = frame.Pixels;
        Span<Rgb> scratch = _scratch;

        foreach (IEffectLayer layer in _layers)
        {
            scratch.Clear();
            layer.Render(scratch, ctx);

            KeyMask mask = layer.Mask;
            BlendMode mode = layer.Blend;
            for (int i = 0; i < framePixels.Length; i++)
            {
                if (mask.Contains(i))
                {
                    framePixels[i] = Blend(framePixels[i], scratch[i], mode);
                }
            }
        }

        if (_layers.Exists(static l => l.IsComplete))
        {
            _layers.RemoveAll(static l => l.IsComplete);
        }
    }

    /// <summary>Combine a source pixel over a destination pixel per <paramref name="mode"/>.</summary>
    public static Rgb Blend(Rgb dst, Rgb src, BlendMode mode) => mode switch
    {
        BlendMode.Normal => src,
        BlendMode.Additive => new Rgb(SatAdd(dst.R, src.R), SatAdd(dst.G, src.G), SatAdd(dst.B, src.B)),
        BlendMode.Multiply => new Rgb(MulChannel(dst.R, src.R), MulChannel(dst.G, src.G), MulChannel(dst.B, src.B)),
        BlendMode.Max => new Rgb(Math.Max(dst.R, src.R), Math.Max(dst.G, src.G), Math.Max(dst.B, src.B)),
        BlendMode.Over => (src.R | src.G | src.B) == 0 ? dst : src, // black source = transparent
        _ => src,
    };

    private static byte SatAdd(byte a, byte b) => (byte)Math.Min(255, a + b);

    private static byte MulChannel(byte a, byte b) => (byte)(a * b / 255);
}
