using RSS_II_RGB.Core;
using RSS_II_RGB.Core.Effects;
using RSS_II_RGB.Core.Effects.Layers;
using RSS_II_RGB.Core.Ipc;
using RSS_II_RGB.Core.Rendering;
using RSS_II_RGB.Core.Sensors;

namespace RSS_II_RGB.Core.Tests;

public class AudioBarsLayerTests
{
    // Bands 0..7 fall in the bass region, 8..15 in mid, 16..23 in treble — matching
    // the layer's frequency bucketing (30 Hz..250..2000..16000 over 24 log bands).
    private static double[] Bands(double bass, double mid, double treble)
    {
        var b = new double[CoreConstants.AudioBandCount];
        for (int i = 0; i < b.Length; i++)
        {
            b[i] = i < 8 ? bass : i < 16 ? mid : treble;
        }
        return b;
    }

    private static SensorState StateWith(double bass, double mid, double treble)
    {
        var s = new SensorState();
        s.Apply(new SensorSample(SensorKind.AudioBands, Bands(bass, mid, treble), 0));
        return s;
    }

    private static readonly int[] BassRow = { 0, 1, 2, 3 };
    private static readonly int[] MidRow = { 4, 5, 6, 7 };
    private static readonly int[] TrebleRow = { 8, 9, 10, 11 };

    private static Rgb[] Render(SensorState state, double multiplier = 1.0)
    {
        var layer = new AudioBarsLayer("bars", state, BassRow, MidRow, TrebleRow, multiplier);
        var target = new Rgb[20];
        layer.Render(target, new EffectContext(TimeSpan.Zero, TimeSpan.Zero, ReadOnlySpan<KeyHit>.Empty));
        return target;
    }

    [Fact]
    public void StrongBass_FillsWholeBassRow_OthersDark()
    {
        Rgb[] t = Render(StateWith(bass: 1.0, mid: 0, treble: 0));

        foreach (int i in BassRow) Assert.True(t[i].R > 0, $"bass key {i} should be lit (red)");
        foreach (int i in MidRow) Assert.Equal(Rgb.Black, t[i]);
        foreach (int i in TrebleRow) Assert.Equal(Rgb.Black, t[i]);
    }

    [Fact]
    public void HalfBass_LightsHalfTheRowFromTheLeft()
    {
        Rgb[] t = Render(StateWith(bass: 0.5, mid: 0, treble: 0));

        // round(0.5 * 4) = 2 → first two keys lit, last two dark.
        Assert.True(t[BassRow[0]].R > 0);
        Assert.True(t[BassRow[1]].R > 0);
        Assert.Equal(Rgb.Black, t[BassRow[2]]);
        Assert.Equal(Rgb.Black, t[BassRow[3]]);
    }

    [Fact]
    public void Multiplier_ScalesBarLength()
    {
        // bass 0.5 × 2 clamps to a full row.
        Rgb[] t = Render(StateWith(bass: 0.5, mid: 0, treble: 0), multiplier: 2.0);
        foreach (int i in BassRow) Assert.True(t[i].R > 0);
    }

    [Fact]
    public void Treble_LightsTrebleRowBlue()
    {
        Rgb[] t = Render(StateWith(bass: 0, mid: 0, treble: 1.0));

        foreach (int i in TrebleRow) Assert.True(t[i].B > 0, $"treble key {i} should be lit (blue)");
        foreach (int i in BassRow) Assert.Equal(Rgb.Black, t[i]);
    }

    [Fact]
    public void Silence_LeavesEverythingDark()
    {
        Rgb[] t = Render(StateWith(0, 0, 0));
        foreach (Rgb px in t) Assert.Equal(Rgb.Black, px);
    }
}
