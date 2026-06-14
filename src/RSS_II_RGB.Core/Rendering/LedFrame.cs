namespace RSS_II_RGB.Core.Rendering;

/// <summary>
/// A reusable 107-LED colour buffer (one frame). Indexed by render order
/// (same order as <see cref="Layout.ScopeIILayout.Keys"/>). Reused across ticks
/// so the render loop allocates nothing on the hot path.
/// </summary>
public sealed class LedFrame
{
    private readonly Rgb[] _pixels = new Rgb[CoreConstants.LedCount];

    public Span<Rgb> Pixels => _pixels;
    public int Length => _pixels.Length;

    public Rgb this[int index]
    {
        get => _pixels[index];
        set => _pixels[index] = value;
    }

    public void Clear() => Array.Clear(_pixels);
}
