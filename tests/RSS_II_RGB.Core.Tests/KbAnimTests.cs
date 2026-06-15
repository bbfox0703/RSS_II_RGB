using RSS_II_RGB.Core;
using RSS_II_RGB.Core.Animation;
using RSS_II_RGB.Core.Effects;
using RSS_II_RGB.Core.Effects.Layers;
using RSS_II_RGB.Core.Layout;
using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.Core.Tests;

public class KbAnimTests
{
    private const int Cols = CoreConstants.MatrixCols; // 24
    private const int Rows = CoreConstants.MatrixRows; // 6

    // A frame whose every cell encodes its position: (col, row, tag).
    private static KbAnim.Frame GradientFrame(ushort delayMs, byte tag)
    {
        var px = new Rgb[Cols * Rows];
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                px[r * Cols + c] = new Rgb((byte)c, (byte)r, tag);
            }
        }
        return new KbAnim.Frame(delayMs, px);
    }

    [Fact]
    public void SaveLoad_RoundTripsExactly()
    {
        var frames = new[] { GradientFrame(100, 0), GradientFrame(40, 1), GradientFrame(250, 2) };
        var anim = new KbAnim(Cols, Rows, frames);

        using var ms = new MemoryStream();
        anim.Save(ms);
        ms.Position = 0;
        KbAnim loaded = KbAnim.Load(ms);

        Assert.Equal(Cols, loaded.Cols);
        Assert.Equal(Rows, loaded.Rows);
        Assert.Equal(frames.Length, loaded.Frames.Count);
        for (int i = 0; i < frames.Length; i++)
        {
            Assert.Equal(frames[i].DelayMs, loaded.Frames[i].DelayMs);
            Assert.Equal(frames[i].Pixels, loaded.Frames[i].Pixels);
        }
    }

    [Fact]
    public void Load_RejectsBadMagic()
    {
        using var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        Assert.Throws<InvalidDataException>(() => KbAnim.Load(ms));
    }

    [Fact]
    public void FrameAt_AdvancesAndLoops()
    {
        var frames = new[] { GradientFrame(100, 10), GradientFrame(100, 20), GradientFrame(100, 30) };
        var anim = new KbAnim(Cols, Rows, frames); // total 300 ms

        Assert.Equal(10, anim.FrameAt(TimeSpan.FromMilliseconds(0)).Pixels[0].B);
        Assert.Equal(10, anim.FrameAt(TimeSpan.FromMilliseconds(99)).Pixels[0].B);
        Assert.Equal(20, anim.FrameAt(TimeSpan.FromMilliseconds(150)).Pixels[0].B);
        Assert.Equal(30, anim.FrameAt(TimeSpan.FromMilliseconds(250)).Pixels[0].B);
        Assert.Equal(10, anim.FrameAt(TimeSpan.FromMilliseconds(350)).Pixels[0].B); // 350 % 300 = 50 -> frame 0
        Assert.Equal(30, anim.FrameAt(TimeSpan.FromMilliseconds(300 * 4 + 280)).Pixels[0].B); // wraps to frame 2
    }

    [Fact]
    public void ZeroDelayFrames_StillAdvance()
    {
        // A GIF can declare 0-delay frames; KbAnim must still produce a positive period.
        var anim = new KbAnim(Cols, Rows, new[] { GradientFrame(0, 1), GradientFrame(0, 2) });
        Assert.True(anim.TotalDurationMs >= 2);
    }

    [Fact]
    public void GifLayer_MapsGridToLedsByRowCol()
    {
        var anim = new KbAnim(Cols, Rows, new[] { GradientFrame(100, 7) });
        var layer = new GifLayer("gif", anim, KeyMask.All);

        var target = new Rgb[CoreConstants.LedCount];
        var ctx = new EffectContext(TimeSpan.Zero, TimeSpan.FromMilliseconds(16), ReadOnlySpan<KeyHit>.Empty);
        layer.Render(target, ctx);

        // Every LED should receive the grid cell at its own (Row, Col).
        for (int i = 0; i < target.Length; i++)
        {
            ref readonly LedKey key = ref ScopeIILayout.ByIndex(i);
            Assert.Equal(new Rgb((byte)key.Col, (byte)key.Row, 7), target[i]);
        }
    }

    [Fact]
    public void GifLayer_AdvancesFrameWithElapsedTime()
    {
        var anim = new KbAnim(Cols, Rows, new[] { GradientFrame(100, 11), GradientFrame(100, 22) });
        var layer = new GifLayer("gif", anim, KeyMask.All);
        int gi = ScopeIILayout.IndexForKeyId(0x33); // 'G'

        var t0 = new Rgb[CoreConstants.LedCount];
        layer.Render(t0, new EffectContext(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(16), ReadOnlySpan<KeyHit>.Empty));
        Assert.Equal(11, t0[gi].B); // frame 0

        var t1 = new Rgb[CoreConstants.LedCount];
        layer.Render(t1, new EffectContext(TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(16), ReadOnlySpan<KeyHit>.Empty));
        Assert.Equal(22, t1[gi].B); // frame 1
    }
}
