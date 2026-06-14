using RSS_II_RGB.Core.Input;
using RSS_II_RGB.Core.Layout;

namespace RSS_II_RGB.Core.Tests;

public class ScancodeMapTests
{
    [Theory]
    [InlineData(0x01, false, 0x00)] // Escape
    [InlineData(0x11, false, 0x1A)] // W
    [InlineData(0x1E, false, 0x13)] // A
    [InlineData(0x1F, false, 0x1B)] // S
    [InlineData(0x20, false, 0x23)] // D
    [InlineData(0x4B, true, 0x85)]  // Left arrow
    [InlineData(0x48, true, 0x8C)]  // Up arrow
    [InlineData(0x50, true, 0x8D)]  // Down arrow
    [InlineData(0x4D, true, 0x95)]  // Right arrow
    [InlineData(0x3B, false, 0x18)] // F1
    public void MapsKnownKeysToExpectedKeyId(uint scan, bool extended, byte expectedKeyId)
    {
        int index = ScancodeMap.ToKeyIndex(scan, extended);
        Assert.True(index >= 0, $"scan 0x{scan:X2} ext={extended} should map");
        Assert.Equal(expectedKeyId, ScopeIILayout.ByIndex(index).KeyId);
    }

    [Theory]
    [InlineData(0x52)] // Insert (ext) vs Numpad 0 (non-ext)
    [InlineData(0x48)] // Up vs Numpad 8
    [InlineData(0x4B)] // Left vs Numpad 4
    [InlineData(0x1C)] // Enter vs Numpad Enter
    [InlineData(0x35)] // / vs Numpad /
    [InlineData(0x1D)] // Left Ctrl vs Right Ctrl
    [InlineData(0x38)] // Left Alt vs Right Alt
    public void ExtendedFlagDisambiguatesSharedScanCodes(uint scan)
    {
        int normal = ScancodeMap.ToKeyIndex(scan, extended: false);
        int extended = ScancodeMap.ToKeyIndex(scan, extended: true);
        Assert.True(normal >= 0 && extended >= 0, $"both variants of 0x{scan:X2} should map");
        Assert.NotEqual(normal, extended);
    }

    [Fact]
    public void NoTwoScanCodesMapToTheSameKey()
    {
        var seen = new HashSet<int>();
        for (uint s = 0; s <= 0xFF; s++)
        {
            foreach (bool ext in new[] { false, true })
            {
                int idx = ScancodeMap.ToKeyIndex(s, ext);
                if (idx >= 0)
                {
                    Assert.True(seen.Add(idx), $"scan 0x{s:X2} ext={ext} collides on index {idx}");
                }
            }
        }
    }

    [Fact]
    public void MapsAtLeastTheFullAlphaNumericAndNavSet()
    {
        // We expect well over 100 physical keys to be reactive (a few — Fn, the
        // logo, Pause — legitimately have no scan code).
        int mapped = 0;
        for (uint s = 0; s <= 0xFF; s++)
        {
            if (ScancodeMap.ToKeyIndex(s, false) >= 0) mapped++;
            if (ScancodeMap.ToKeyIndex(s, true) >= 0) mapped++;
        }
        Assert.True(mapped >= 100, $"expected >= 100 mapped keys, got {mapped}");
    }

    [Fact]
    public void UnknownScanCodeReturnsMinusOne()
    {
        Assert.Equal(-1, ScancodeMap.ToKeyIndex(0x00, false)); // no key uses make code 0
        Assert.Equal(-1, ScancodeMap.ToKeyIndex(0x100, false)); // out of range
    }
}
