using RSS_II_RGB.Core.Animation;
using RSS_II_RGB.Core.Layout;
using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.Core.Effects.Layers;

/// <summary>
/// Plays a pre-baked <see cref="KbAnim"/> as a base effect: each tick it picks the
/// frame for the current elapsed time (looping) and maps the animation's Cols×Rows
/// grid onto the keyboard by each LED's (Row, Col). Opaque base layer — it fills
/// every masked key; LEDs whose grid cell is out of range stay black.
/// </summary>
public sealed class GifLayer : IEffectLayer
{
    private readonly KbAnim _anim;

    public GifLayer(string id, KbAnim anim, KeyMask mask, int zOrder = 0, BlendMode blend = BlendMode.Normal)
    {
        Id = id;
        _anim = anim;
        Mask = mask;
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
        KbAnim.Frame frame = _anim.FrameAt(ctx.Elapsed);
        Rgb[] pixels = frame.Pixels;
        int cols = _anim.Cols;
        int rows = _anim.Rows;

        for (int i = 0; i < target.Length; i++)
        {
            ref readonly LedKey key = ref ctx.Layout.ByIndex(i);
            if ((uint)key.Col < (uint)cols && (uint)key.Row < (uint)rows)
            {
                target[i] = pixels[key.Row * cols + key.Col];
            }
        }
    }
}
