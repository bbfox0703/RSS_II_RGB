using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.Core.Tests;

public class RgbTests
{
    [Fact]
    public void Scale_HalvesChannels()
        => Assert.Equal(new Rgb(100, 50, 0), new Rgb(200, 100, 0).Scale(0.5f));

    [Fact]
    public void Scale_ClampsAboveMax()
        => Assert.Equal(new Rgb(255, 255, 255), new Rgb(200, 200, 200).Scale(2f));

    [Fact]
    public void Scale_ToZeroIsBlack()
        => Assert.Equal(Rgb.Black, Rgb.White.Scale(0f));

    [Fact]
    public void Lerp_ReturnsEndpoints()
    {
        Assert.Equal(Rgb.Black, Rgb.Lerp(Rgb.Black, Rgb.White, 0f));
        Assert.Equal(Rgb.White, Rgb.Lerp(Rgb.Black, Rgb.White, 1f));
    }

    [Fact]
    public void Lerp_ClampsOutOfRangeT()
    {
        Assert.Equal(Rgb.White, Rgb.Lerp(Rgb.Black, Rgb.White, 5f));
        Assert.Equal(Rgb.Black, Rgb.Lerp(Rgb.Black, Rgb.White, -1f));
    }

    [Fact]
    public void FromHsv_RedAtHueZero()
        => Assert.Equal(new Rgb(255, 0, 0), Rgb.FromHsv(0, 1, 1));

    [Fact]
    public void FromHsv_WrapsAtOne()
        => Assert.Equal(Rgb.FromHsv(0, 1, 1), Rgb.FromHsv(1, 1, 1));

    [Fact]
    public void FromHsv_ZeroValueIsBlack()
        => Assert.Equal(Rgb.Black, Rgb.FromHsv(0.4, 1, 0));
}
