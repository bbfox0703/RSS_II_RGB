using RSS_II_RGB.Core.Layout;

namespace RSS_II_RGB.Core.Rendering;

/// <summary>
/// A reusable per-LED colour buffer (one frame), sized to the connected keyboard.
/// Indexed by render order (same order as the profile's <see cref="KeyboardProfile.Keys"/>).
/// Reused across ticks so the render loop allocates nothing on the hot path.
/// </summary>
public sealed class LedFrame
{
    private readonly Rgb[] _pixels;

    /// <summary>Defaults to the Scope II LED count (used by tests).</summary>
    public LedFrame() : this(ScopeIILayout.Profile.LedCount)
    {
    }

    public LedFrame(int ledCount) => _pixels = new Rgb[ledCount];

    public Span<Rgb> Pixels => _pixels;
    public int Length => _pixels.Length;

    public Rgb this[int index]
    {
        get => _pixels[index];
        set => _pixels[index] = value;
    }

    public void Clear() => Array.Clear(_pixels);
}
