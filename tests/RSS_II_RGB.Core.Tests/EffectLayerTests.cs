using RSS_II_RGB.Core.Effects;
using RSS_II_RGB.Core.Effects.Layers;
using RSS_II_RGB.Core.Layout;
using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.Core.Tests;

public class EffectLayerTests
{
    private static void Render(IEffectLayer layer, Span<Rgb> target, double elapsedSeconds, params KeyHit[] hits)
    {
        var ctx = new EffectContext(TimeSpan.FromSeconds(elapsedSeconds), TimeSpan.FromMilliseconds(16), hits);
        layer.Render(target, ctx);
    }

    [Fact]
    public void Solid_FillsColour()
    {
        var t = new Rgb[CoreConstants.LedCount];
        Render(new SolidLayer("s", new Rgb(7, 8, 9), KeyMask.All), t, 0);
        Assert.All(t, px => Assert.Equal(new Rgb(7, 8, 9), px));
    }

    [Fact]
    public void Breathing_FullAtQuarterPeriod_BlackAtThreeQuarter()
    {
        var layer = new BreathingLayer("b", new Rgb(200, 100, 0), KeyMask.All, periodSeconds: 2.0);
        var t = new Rgb[CoreConstants.LedCount];

        Render(layer, t, 0.5);  // phase pi/2 -> sin 1 -> full
        Assert.Equal(new Rgb(200, 100, 0), t[0]);

        Render(layer, t, 1.5);  // phase 3pi/2 -> sin -1 -> off
        Assert.Equal(Rgb.Black, t[0]);
    }

    [Fact]
    public void Rainbow_IsRedAtIndexZeroTimeZero()
    {
        var t = new Rgb[CoreConstants.LedCount];
        Render(new RainbowLayer("r", KeyMask.All), t, 0);
        Assert.Equal(new Rgb(255, 0, 0), t[0]);
    }

    [Fact]
    public void Wave_IsRedInLeftmostColumnAtTimeZero()
    {
        var t = new Rgb[CoreConstants.LedCount];
        Render(new WaveLayer("w", KeyMask.All), t, 0);
        // Index 0 (Escape) is column 0 -> hue 0 -> red.
        Assert.Equal(new Rgb(255, 0, 0), t[0]);
    }

    [Fact]
    public void KeypressFade_LightsOnHitThenFadesToBlack()
    {
        var layer = new KeypressFadeLayer("k", Rgb.White, fadeSeconds: 1.0);
        var t = new Rgb[CoreConstants.LedCount];

        Array.Clear(t);
        Render(layer, t, 0.0, new KeyHit(5, TimeSpan.Zero));
        Assert.Equal(Rgb.White, t[5]);              // age 0 -> full

        Array.Clear(t);
        Render(layer, t, 0.5);
        Assert.Equal(new Rgb(128, 128, 128), t[5]); // age 0.5 -> half (255*0.5 -> 128)

        Array.Clear(t);
        Render(layer, t, 1.1);
        Assert.Equal(Rgb.Black, t[5]);              // expired -> pruned, untouched
    }

    [Fact]
    public void Ripple_LightsCentreAtStart()
    {
        var layer = new RippleLayer("rp", Rgb.White, speedGridPerSec: 10, width: 1.0, fadeSeconds: 1.0);
        var t = new Rgb[CoreConstants.LedCount];
        int gi = ScopeIILayout.IndexForKeyId(0x33); // 'G', mid-keyboard

        Render(layer, t, 0.0001, new KeyHit(gi, TimeSpan.Zero));

        Assert.True(t[gi].R > 0, "the pressed key should light at ripple start");
    }

    [Fact]
    public void Ripple_RingMovesOutwardOverTime()
    {
        var layer = new RippleLayer("rp", Rgb.White, speedGridPerSec: 4, width: 1.0, fadeSeconds: 2.0);
        var t = new Rgb[CoreConstants.LedCount];
        int gi = ScopeIILayout.IndexForKeyId(0x33); // 'G' at (3,6)
        int fi = ScopeIILayout.IndexForKeyId(0x32); // 'T' at (2,6) — one row above

        // Seed the hit, then sample when the ring (radius = age*4) reaches distance ~1.
        Render(layer, t, 0.0, new KeyHit(gi, TimeSpan.Zero));
        Array.Clear(t);
        Render(layer, t, 0.25); // radius ~1.0 -> the neighbour one cell away lights

        Assert.True(t[fi].R > 0, "ring should have reached the adjacent key");
    }
}
