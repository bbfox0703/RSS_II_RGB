using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.Core.Effects;

/// <summary>
/// One layer of the render stack. The compositor clears a scratch buffer, calls
/// <see cref="Render"/>, then blends the result into the frame using
/// <see cref="Mask"/> and <see cref="Blend"/>.
///
/// Convention: a <see cref="BlendMode.Normal"/> layer must fill every masked
/// pixel (black is opaque). Reactive overlays should use
/// <see cref="BlendMode.Additive"/> so untouched pixels stay transparent.
/// </summary>
public interface IEffectLayer
{
    string Id { get; }

    /// <summary>Composite order, ascending (lower drawn first).</summary>
    int ZOrder { get; }

    BlendMode Blend { get; }

    KeyMask Mask { get; }

    /// <summary>When true the compositor drops this layer (transient effects that have ended).</summary>
    bool IsComplete { get; }

    /// <summary>Fill <paramref name="target"/> (107 pixels, pre-cleared) for this tick.</summary>
    void Render(Span<Rgb> target, in EffectContext ctx);
}
