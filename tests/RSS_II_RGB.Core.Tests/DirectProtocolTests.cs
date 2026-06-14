using RSS_II_RGB.Core;
using RSS_II_RGB.Core.Device;
using RSS_II_RGB.Core.Layout;
using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.Core.Tests;

public class DirectProtocolTests
{
    private static Rgb[] MakePixels(Func<int, Rgb> f)
    {
        var px = new Rgb[CoreConstants.LedCount];
        for (int i = 0; i < px.Length; i++)
        {
            px[i] = f(i);
        }
        return px;
    }

    [Fact]
    public void BuildFrame_HasEightPacketsWithCorrectHeaders()
    {
        var dest = new byte[DirectProtocol.FrameBufferSize];
        DirectProtocol.BuildFrame(MakePixels(_ => Rgb.Black), dest);

        for (int p = 0; p < CoreConstants.PacketsPerFrame; p++)
        {
            int b = p * CoreConstants.ReportLength;
            Assert.Equal(0x00, dest[b + 0]);          // HID report id
            Assert.Equal(0xC0, dest[b + 1]);
            Assert.Equal(0x81, dest[b + 2]);
            int expectedCount = p < 7 ? 15 : 2;       // 15*7 + 2 = 107
            Assert.Equal(expectedCount, dest[b + 3]);
            Assert.Equal(0x00, dest[b + 4]);
        }
    }

    [Fact]
    public void BuildFrame_WritesKeyIdAndColourQuadsInRenderOrder()
    {
        // Unique colour per LED to catch any mis-ordering.
        var pixels = MakePixels(i => new Rgb((byte)i, (byte)(i + 1), (byte)(i + 2)));
        var dest = new byte[DirectProtocol.FrameBufferSize];
        DirectProtocol.BuildFrame(pixels, dest);

        for (int i = 0; i < CoreConstants.LedCount; i++)
        {
            int p = i / CoreConstants.LedsPerPacket;
            int j = i % CoreConstants.LedsPerPacket;
            int b = p * CoreConstants.ReportLength + j * 4 + 5;

            Assert.Equal(ScopeIILayout.Keys[i].KeyId, dest[b]);
            Assert.Equal((byte)i, dest[b + 1]);
            Assert.Equal((byte)(i + 1), dest[b + 2]);
            Assert.Equal((byte)(i + 2), dest[b + 3]);
        }
    }

    [Fact]
    public void BuildFrame_LastPacketTailIsZeroPadded()
    {
        var dest = new byte[DirectProtocol.FrameBufferSize];
        DirectProtocol.BuildFrame(MakePixels(_ => Rgb.White), dest);

        // Packet 7 carries 2 LEDs: data ends at offset 5 + 2*4 = 13, rest is padding.
        int b = 7 * CoreConstants.ReportLength;
        for (int k = 13; k < CoreConstants.ReportLength; k++)
        {
            Assert.Equal(0, dest[b + k]);
        }
    }

    [Fact]
    public void BuildFrame_RejectsWrongPixelCount()
    {
        var dest = new byte[DirectProtocol.FrameBufferSize];
        Assert.Throws<ArgumentException>(() => DirectProtocol.BuildFrame(new Rgb[10], dest));
    }

    [Fact]
    public void BuildFrame_RejectsTooSmallDestination()
    {
        Assert.Throws<ArgumentException>(() =>
            DirectProtocol.BuildFrame(MakePixels(_ => Rgb.Black), new byte[10]));
    }
}
