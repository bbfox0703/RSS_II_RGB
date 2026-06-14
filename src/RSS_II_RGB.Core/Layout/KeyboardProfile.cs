namespace RSS_II_RGB.Core.Layout;

/// <summary>
/// Everything device-specific about one keyboard's LED layout: its geometry,
/// the ordered key table (Direct-packet render order), and the keyId↔index map.
/// This is the per-device seam that lets the render path drive any TUF-family
/// keyboard from data alone — only the bytes in <see cref="Keys"/> change.
///
/// The Direct protocol itself (15 LEDs/packet, 65-byte report) is shared across
/// the family and stays in <see cref="CoreConstants"/>; only the packet count
/// (a function of <see cref="LedCount"/>) is per-device.
/// </summary>
public sealed class KeyboardProfile
{
    private readonly LedKey[] _keys;
    private readonly int[] _indexByKeyId; // keyId (0..255) -> render index, or -1

    public KeyboardProfile(string name, int rows, int cols, LedKey[] keys)
    {
        Name = name;
        Rows = rows;
        Cols = cols;
        _keys = keys;
        LedCount = keys.Length;

        _indexByKeyId = new int[256];
        Array.Fill(_indexByKeyId, -1);
        for (int i = 0; i < keys.Length; i++)
        {
            _indexByKeyId[keys[i].KeyId] = i;
        }
    }

    /// <summary>Human-readable model name (diagnostics only).</summary>
    public string Name { get; }

    /// <summary>Number of addressable LEDs (= <see cref="Keys"/> length).</summary>
    public int LedCount { get; }

    /// <summary>Matrix geometry, used by spatial effects (wave, ripple).</summary>
    public int Rows { get; }
    public int Cols { get; }

    /// <summary>All LEDs in Direct-packet render order.</summary>
    public ReadOnlySpan<LedKey> Keys => _keys;

    /// <summary>The LED at a render index (0..<see cref="LedCount"/>-1).</summary>
    public ref readonly LedKey ByIndex(int index) => ref _keys[index];

    /// <summary>Render index for a device key id, or -1 if not addressable.</summary>
    public int IndexForKeyId(byte keyId) => _indexByKeyId[keyId];

    /// <summary>Direct packets needed for one frame: ceil(LedCount / LedsPerPacket).</summary>
    public int PacketsPerFrame => (LedCount + CoreConstants.LedsPerPacket - 1) / CoreConstants.LedsPerPacket;

    /// <summary>Total bytes for one frame's packets.</summary>
    public int FrameBufferSize => PacketsPerFrame * CoreConstants.ReportLength;
}
