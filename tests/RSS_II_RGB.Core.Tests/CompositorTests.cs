using RSS_II_RGB.Core.Effects;
using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.Core.Tests;

// File-local fake layer that fills its target with a constant colour.
file sealed class FillLayer : IEffectLayer
{
    private readonly Rgb _color;

    public FillLayer(string id, Rgb color, BlendMode blend, KeyMask mask, int zOrder = 0, bool complete = false)
    {
        Id = id;
        _color = color;
        Blend = blend;
        Mask = mask;
        ZOrder = zOrder;
        IsComplete = complete;
    }

    public string Id { get; }
    public int ZOrder { get; }
    public BlendMode Blend { get; }
    public KeyMask Mask { get; }
    public bool IsComplete { get; }

    public void Render(Span<Rgb> target, in EffectContext ctx) => target.Fill(_color);
}

public class CompositorTests
{
    private static EffectContext EmptyCtx() => new(TimeSpan.Zero, TimeSpan.Zero, ReadOnlySpan<KeyHit>.Empty);

    [Fact]
    public void Blend_Normal_Replaces()
        => Assert.Equal(new Rgb(1, 2, 3), Compositor.Blend(new Rgb(9, 9, 9), new Rgb(1, 2, 3), BlendMode.Normal));

    [Fact]
    public void Blend_Additive_Saturates()
        => Assert.Equal(new Rgb(255, 255, 200), Compositor.Blend(new Rgb(200, 100, 100), new Rgb(100, 200, 100), BlendMode.Additive));

    [Fact]
    public void Blend_Multiply()
        => Assert.Equal(new Rgb(128, 0, 255), Compositor.Blend(new Rgb(255, 0, 255), new Rgb(128, 255, 255), BlendMode.Multiply));

    [Fact]
    public void Blend_Max()
        => Assert.Equal(new Rgb(200, 200, 200), Compositor.Blend(new Rgb(200, 50, 200), new Rgb(50, 200, 100), BlendMode.Max));

    [Fact]
    public void Compose_SingleSolid_FillsFrame()
    {
        var comp = new Compositor();
        comp.Add(new FillLayer("a", new Rgb(10, 20, 30), BlendMode.Normal, KeyMask.All));
        var frame = new LedFrame();

        comp.Compose(frame, EmptyCtx());

        foreach (Rgb px in frame.Pixels.ToArray())
        {
            Assert.Equal(new Rgb(10, 20, 30), px);
        }
    }

    [Fact]
    public void Compose_MaskConfinesWrites()
    {
        var comp = new Compositor();
        comp.Add(new FillLayer("a", new Rgb(255, 0, 0), BlendMode.Normal, KeyMask.FromIndices(new[] { 5 })));
        var frame = new LedFrame();

        comp.Compose(frame, EmptyCtx());

        for (int i = 0; i < frame.Length; i++)
        {
            Assert.Equal(i == 5 ? new Rgb(255, 0, 0) : Rgb.Black, frame[i]);
        }
    }

    [Fact]
    public void Compose_HigherZOrderWinsForNormalBlend()
    {
        var comp = new Compositor();
        comp.Add(new FillLayer("top", new Rgb(2, 2, 2), BlendMode.Normal, KeyMask.All, zOrder: 10));
        comp.Add(new FillLayer("bottom", new Rgb(1, 1, 1), BlendMode.Normal, KeyMask.All, zOrder: 0));
        var frame = new LedFrame();

        comp.Compose(frame, EmptyCtx());

        Assert.Equal(new Rgb(2, 2, 2), frame[0]);
    }

    [Fact]
    public void Compose_AdditiveOverlayStacksOnBase()
    {
        var comp = new Compositor();
        comp.Add(new FillLayer("base", new Rgb(10, 10, 10), BlendMode.Normal, KeyMask.All, zOrder: 0));
        comp.Add(new FillLayer("add", new Rgb(5, 0, 0), BlendMode.Additive, KeyMask.All, zOrder: 1));
        var frame = new LedFrame();

        comp.Compose(frame, EmptyCtx());

        Assert.Equal(new Rgb(15, 10, 10), frame[0]);
    }

    [Fact]
    public void Compose_DropsCompletedLayers()
    {
        var comp = new Compositor();
        comp.Add(new FillLayer("done", Rgb.White, BlendMode.Normal, KeyMask.All, complete: true));
        var frame = new LedFrame();

        comp.Compose(frame, EmptyCtx());

        Assert.Empty(comp.Layers);
    }
}
