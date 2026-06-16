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

    // Advance a stateful layer across `ticks` frames of `step` seconds, invoking
    // `onFrame` after each render with the rendered buffer and the elapsed time.
    private static void RunOverTime(IEffectLayer layer, double step, int ticks,
                                    Action<Rgb[], double> onFrame)
    {
        var t = new Rgb[CoreConstants.LedCount];
        var delta = TimeSpan.FromSeconds(step);
        for (int i = 1; i <= ticks; i++)
        {
            Array.Clear(t);
            double elapsed = i * step;
            var ctx = new EffectContext(TimeSpan.FromSeconds(elapsed), delta, Array.Empty<KeyHit>());
            layer.Render(t, ctx);
            onFrame(t, elapsed);
        }
    }

    [Fact]
    public void Starlight_LightsStarsOverTime()
    {
        var layer = new StarlightLayer("star", KeyMask.All, rng: new Random(1234));
        bool everLit = false;
        RunOverTime(layer, step: 0.04, ticks: 100, (t, _) =>
        {
            foreach (Rgb px in t)
            {
                if ((px.R | px.G | px.B) != 0) { everLit = true; }
            }
        });
        Assert.True(everLit, "the starfield should light at least one key over time");
    }

    [Fact]
    public void Starlight_NeverLightsAdjacentKeysSimultaneously()
    {
        // Placement forbids a star within ~1 grid cell of a live one, so at no
        // instant should two lit keys be immediate neighbours. Holds for any seed.
        var layer = new StarlightLayer("star", KeyMask.All, rng: new Random(7));
        RunOverTime(layer, step: 0.03, ticks: 400, (t, _) =>
        {
            var lit = new List<int>();
            for (int i = 0; i < t.Length; i++)
            {
                if ((t[i].R | t[i].G | t[i].B) != 0) { lit.Add(i); }
            }
            for (int a = 0; a < lit.Count; a++)
            {
                for (int b = a + 1; b < lit.Count; b++)
                {
                    ref readonly LedKey ka = ref ScopeIILayout.ByIndex(lit[a]);
                    ref readonly LedKey kb = ref ScopeIILayout.ByIndex(lit[b]);
                    double dr = ka.Row - kb.Row, dc = ka.Col - kb.Col;
                    double dist = Math.Sqrt(dr * dr + dc * dc);
                    Assert.True(dist > 1.5,
                        $"keys {lit[a]} and {lit[b]} are adjacent (dist {dist:0.00}) yet both lit");
                }
            }
        });
    }

    [Fact]
    public void Starlight_StaysWithinItsMask()
    {
        // As a zone effect the starfield must never light a key outside its mask.
        int[] zone = { 10, 11, 16, 17, 50, 53, 90, 97 };
        var inMask = new HashSet<int>(zone);
        var layer = new StarlightLayer("star", KeyMask.FromIndices(zone), rng: new Random(99));
        RunOverTime(layer, step: 0.04, ticks: 200, (t, _) =>
        {
            for (int i = 0; i < t.Length; i++)
            {
                if ((t[i].R | t[i].G | t[i].B) != 0)
                {
                    Assert.Contains(i, inMask);
                }
            }
        });
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
