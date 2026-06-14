using RSS_II_RGB.Core.Layout;

namespace RSS_II_RGB.App;

/// <summary>
/// The three horizontal key rows for the "bars" audio layout, defined by device
/// key id and resolved through the connected keyboard's profile. Each row is
/// ordered left-to-right so a bar fills from the left edge.
/// </summary>
internal static class AudioBarRows
{
    // Treble — Tab row: Tab Q W E R T Y U I O P [ ] \
    private static readonly byte[] Treble =
        { 0x02, 0x12, 0x1A, 0x22, 0x2A, 0x32, 0x3A, 0x42, 0x4A, 0x52, 0x5A, 0x62, 0x6A, 0x7A };

    // Mid — Caps row: Caps A S D F G H J K L ; ' Enter
    private static readonly byte[] Mid =
        { 0x03, 0x13, 0x1B, 0x23, 0x2B, 0x33, 0x3B, 0x43, 0x4B, 0x53, 0x5B, 0x63, 0x7B };

    // Bass — Shift row: LShift Z X C V B N M , . / RShift
    private static readonly byte[] Bass =
        { 0x04, 0x14, 0x1C, 0x24, 0x2C, 0x34, 0x3C, 0x44, 0x4C, 0x54, 0x5C, 0x7C };

    public static int[] BassRow(KeyboardProfile profile) => Resolve(profile, Bass);
    public static int[] MidRow(KeyboardProfile profile) => Resolve(profile, Mid);
    public static int[] TrebleRow(KeyboardProfile profile) => Resolve(profile, Treble);

    private static int[] Resolve(KeyboardProfile profile, byte[] keyIds)
    {
        var indices = new List<int>(keyIds.Length);
        foreach (byte keyId in keyIds)
        {
            int index = profile.IndexForKeyId(keyId);
            if (index >= 0)
            {
                indices.Add(index);
            }
        }
        return indices.ToArray();
    }
}
