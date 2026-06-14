using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.Core.Effects;

/// <summary>A physical key press, stamped with the engine-elapsed time it occurred.</summary>
public readonly record struct KeyHit(int KeyIndex, TimeSpan When);

/// <summary>How a layer's output combines with the pixels beneath it.</summary>
public enum BlendMode
{
    /// <summary>Replace (top wins). Use for opaque base layers that fill every masked pixel.</summary>
    Normal,

    /// <summary>Saturating add. Use for reactive overlays so unlit pixels (black) are no-ops.</summary>
    Additive,

    /// <summary>Channel-wise multiply.</summary>
    Multiply,

    /// <summary>Channel-wise maximum.</summary>
    Max,
}

/// <summary>
/// Per-tick inputs handed to every layer. A ref struct so <see cref="NewHits"/>
/// can be a span — layers that need history must copy what they want, not store
/// the context.
/// </summary>
public readonly ref struct EffectContext
{
    public TimeSpan Elapsed { get; }
    public TimeSpan Delta { get; }
    public ReadOnlySpan<KeyHit> NewHits { get; }

    public EffectContext(TimeSpan elapsed, TimeSpan delta, ReadOnlySpan<KeyHit> newHits)
    {
        Elapsed = elapsed;
        Delta = delta;
        NewHits = newHits;
    }
}

/// <summary>
/// The set of LED indices a layer applies to. <see cref="All"/> is allocation-free.
/// This is what lets one layered model express both "global effect" and
/// "different effects on different key groups".
/// </summary>
public readonly struct KeyMask
{
    private readonly bool[]? _included; // null => every key

    private KeyMask(bool[]? included) => _included = included;

    public static KeyMask All => new(null);

    public static KeyMask FromIndices(ReadOnlySpan<int> indices)
    {
        var arr = new bool[CoreConstants.LedCount];
        foreach (int i in indices)
        {
            if ((uint)i < (uint)arr.Length)
            {
                arr[i] = true;
            }
        }
        return new KeyMask(arr);
    }

    public bool Contains(int index)
        => _included is null || ((uint)index < (uint)_included.Length && _included[index]);
}
