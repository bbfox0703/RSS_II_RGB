using RSS_II_RGB.Core.Layout;

namespace RSS_II_RGB.Core.Tests;

public class ScopeIILayoutTests
{
    // The 107 ANSI key ids in render order — independently verified on real
    // hardware via the POC. Acts as a drift guard for the baked table.
    private static readonly byte[] ExpectedKeyIds =
    {
        0x00,0x01,0x02,0x03,0x04,0x05,0x11,0x0D,0x18,0x19,0x12,0x13,0x14,0x15,
        0x20,0x21,0x1A,0x1B,0x1C,0x28,0x29,0x22,0x23,0x24,0x30,0x31,0x2A,0x2B,
        0x2C,0x2D,0x39,0x32,0x33,0x34,0x35,0x40,0x41,0x3A,0x3B,0x3C,0x3D,0x48,
        0x49,0x42,0x43,0x44,0x50,0x51,0x4A,0x4B,0x4C,0x58,0x59,0x52,0x53,0x54,
        0x4D,0x60,0x61,0x5A,0x5B,0x5C,0x5D,0x68,0x69,0x62,0x63,0x65,0x70,0x79,
        0x6A,0x7C,0x78,0x7A,0x7B,0x7D,0x80,0x81,0x82,0x85,0x88,0x89,0x8A,0x8C,
        0x8D,0x90,0x91,0x92,0x95,0x99,0x9A,0x9B,0x9C,0x9D,0xA0,0xA1,0xA2,0xA3,
        0xA4,0xA9,0xAA,0xAB,0xAC,0xAD,0xB1,0xB2,0xB4,
    };

    [Fact]
    public void Has107Keys() => Assert.Equal(107, ScopeIILayout.Keys.Length);

    [Fact]
    public void KeyIdsMatchVerifiedSetInOrder()
    {
        var actual = new byte[ScopeIILayout.Keys.Length];
        for (int i = 0; i < actual.Length; i++)
        {
            actual[i] = ScopeIILayout.Keys[i].KeyId;
        }
        Assert.Equal(ExpectedKeyIds, actual);
    }

    [Fact]
    public void IndexForKeyIdIsInverseOfByIndex()
    {
        for (int i = 0; i < ScopeIILayout.Keys.Length; i++)
        {
            ref readonly LedKey key = ref ScopeIILayout.ByIndex(i);
            Assert.Equal(i, key.Index);
            Assert.Equal(i, ScopeIILayout.IndexForKeyId(key.KeyId));
        }
    }

    [Fact]
    public void UnknownKeyIdReturnsMinusOne()
    {
        Assert.Equal(-1, ScopeIILayout.IndexForKeyId(0xFF));
        Assert.Equal(-1, ScopeIILayout.IndexForKeyId(0x06)); // a gap in the id space
    }

    [Fact]
    public void RowsAndColsInRange()
    {
        foreach (LedKey k in ScopeIILayout.Keys)
        {
            Assert.InRange(k.Row, 0, ScopeIILayout.Rows - 1);
            Assert.InRange(k.Col, 0, ScopeIILayout.Cols - 1);
        }
    }

    [Fact]
    public void EachGridCellUsedAtMostOnce()
    {
        var seen = new HashSet<(int Row, int Col)>();
        foreach (LedKey k in ScopeIILayout.Keys)
        {
            Assert.True(seen.Add((k.Row, k.Col)), $"duplicate grid cell ({k.Row},{k.Col}) for {k.Name}");
        }
    }

    [Theory]
    [InlineData("ESCAPE", 0x00, 0, 0)]
    [InlineData("W", 0x1A, 2, 3)]
    [InlineData("LEFT_ARROW", 0x85, 5, 16)]
    [InlineData("Logo", 0xA0, 0, 21)]
    public void KnownKeysHaveExpectedIdAndPosition(string name, byte keyId, int row, int col)
    {
        ref readonly LedKey k = ref ScopeIILayout.ByIndex(ScopeIILayout.IndexForKeyId(keyId));
        Assert.Equal(name, k.Name);
        Assert.Equal(row, k.Row);
        Assert.Equal(col, k.Col);
    }
}
