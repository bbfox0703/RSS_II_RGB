namespace RSS_II_RGB.Core.Layout;

/// <summary>
/// One addressable LED on the keyboard: its device key id, a human name, its
/// position in the 6x24 matrix, and its render-order index.
/// </summary>
public readonly record struct LedKey(byte KeyId, string Name, int Row, int Col, int Index);

/// <summary>
/// Static, baked layout for the ASUS ROG Strix Scope II (ANSI / US, 107 LEDs).
///
/// Generated from the read-only vendor data
/// vendor/openrgb/.../AsusAuraTUFKeyboardLayouts.h
/// (AsusROGStrixScopeIILayouts, LAYOUT_US: led_names + the 6x24 matrix).
/// Baked as committed C# so there is NO runtime dependency on vendor/ (rule 8).
///
/// The <see cref="Keys"/> order IS the Direct-packet render order. Each entry's
/// (Row, Col) is its only spatial coordinate (grid, not pixels) — used by
/// spatial effects (wave, ripple).
/// </summary>
public static class ScopeIILayout
{
    // Render order = vendor led_names order. KeyId is the byte addressed in the
    // Direct packet; (Row, Col) come from the vendor matrix.
    private static readonly LedKey[] _keys =
    {
        new(0x00, "ESCAPE", 0, 0, 0),
        new(0x01, "BACK_TICK", 1, 0, 1),
        new(0x02, "TAB", 2, 0, 2),
        new(0x03, "CAPS_LOCK", 3, 0, 3),
        new(0x04, "LEFT_SHIFT", 4, 0, 4),
        new(0x05, "LEFT_CONTROL", 5, 0, 5),
        new(0x11, "1", 1, 1, 6),
        new(0x0D, "LEFT_WINDOWS", 5, 1, 7),
        new(0x18, "F1", 0, 2, 8),
        new(0x19, "2", 1, 2, 9),
        new(0x12, "Q", 2, 2, 10),
        new(0x13, "A", 3, 2, 11),
        new(0x14, "Z", 4, 2, 12),
        new(0x15, "LEFT_ALT", 5, 2, 13),
        new(0x20, "F2", 0, 3, 14),
        new(0x21, "3", 1, 3, 15),
        new(0x1A, "W", 2, 3, 16),
        new(0x1B, "S", 3, 3, 17),
        new(0x1C, "X", 4, 3, 18),
        new(0x28, "F3", 0, 4, 19),
        new(0x29, "4", 1, 4, 20),
        new(0x22, "E", 2, 4, 21),
        new(0x23, "D", 3, 4, 22),
        new(0x24, "C", 4, 4, 23),
        new(0x30, "F4", 0, 5, 24),
        new(0x31, "5", 1, 5, 25),
        new(0x2A, "R", 2, 5, 26),
        new(0x2B, "F", 3, 5, 27),
        new(0x2C, "V", 4, 5, 28),
        new(0x2D, "SPACE", 5, 5, 29),
        new(0x39, "6", 1, 6, 30),
        new(0x32, "T", 2, 6, 31),
        new(0x33, "G", 3, 6, 32),
        new(0x34, "B", 4, 6, 33),
        new(0x35, "SPACE", 5, 6, 34),
        new(0x40, "F5", 0, 7, 35),
        new(0x41, "7", 1, 7, 36),
        new(0x3A, "Y", 2, 7, 37),
        new(0x3B, "H", 3, 7, 38),
        new(0x3C, "N", 4, 7, 39),
        new(0x3D, "SPACE", 5, 7, 40),
        new(0x48, "F6", 0, 8, 41),
        new(0x49, "8", 1, 8, 42),
        new(0x42, "U", 2, 8, 43),
        new(0x43, "J", 3, 8, 44),
        new(0x44, "M", 4, 8, 45),
        new(0x50, "F7", 0, 9, 46),
        new(0x51, "9", 1, 9, 47),
        new(0x4A, "I", 2, 9, 48),
        new(0x4B, "K", 3, 9, 49),
        new(0x4C, "COMMA", 4, 9, 50),
        new(0x58, "F8", 0, 10, 51),
        new(0x59, "0", 1, 10, 52),
        new(0x52, "O", 2, 10, 53),
        new(0x53, "L", 3, 10, 54),
        new(0x54, "PERIOD", 4, 10, 55),
        new(0x4D, "RIGHT_ALT", 5, 10, 56),
        new(0x60, "F9", 0, 11, 57),
        new(0x61, "MINUS", 1, 11, 58),
        new(0x5A, "P", 2, 11, 59),
        new(0x5B, "SEMICOLON", 3, 11, 60),
        new(0x5C, "FORWARD_SLASH", 4, 11, 61),
        new(0x5D, "RIGHT_FUNCTION", 5, 11, 62),
        new(0x68, "F10", 0, 12, 63),
        new(0x69, "EQUALS", 1, 12, 64),
        new(0x62, "LEFT_BRACKET", 2, 12, 65),
        new(0x63, "QUOTE", 3, 12, 66),
        new(0x65, "MENU", 5, 12, 67),
        new(0x70, "F11", 0, 13, 68),
        new(0x79, "BACKSPACE", 1, 13, 69),
        new(0x6A, "RIGHT_BRACKET", 2, 13, 70),
        new(0x7C, "RIGHT_SHIFT", 4, 13, 71),
        new(0x78, "F12", 0, 14, 72),
        new(0x7A, "ANSI_BACK_SLASH", 2, 14, 73),
        new(0x7B, "ANSI_ENTER", 3, 14, 74),
        new(0x7D, "RIGHT_CONTROL", 5, 14, 75),
        new(0x80, "PRINT_SCREEN", 0, 16, 76),
        new(0x81, "INSERT", 1, 16, 77),
        new(0x82, "DELETE", 2, 16, 78),
        new(0x85, "LEFT_ARROW", 5, 16, 79),
        new(0x88, "SCROLL_LOCK", 0, 17, 80),
        new(0x89, "HOME", 1, 17, 81),
        new(0x8A, "END", 2, 17, 82),
        new(0x8C, "UP_ARROW", 4, 17, 83),
        new(0x8D, "DOWN_ARROW", 5, 17, 84),
        new(0x90, "PAUSE_BREAK", 0, 18, 85),
        new(0x91, "PAGE_UP", 1, 18, 86),
        new(0x92, "PAGE_DOWN", 2, 18, 87),
        new(0x95, "RIGHT_ARROW", 5, 18, 88),
        new(0x99, "NUMPAD_LOCK", 1, 20, 89),
        new(0x9A, "NUMPAD_7", 2, 20, 90),
        new(0x9B, "NUMPAD_4", 3, 20, 91),
        new(0x9C, "NUMPAD_1", 4, 20, 92),
        new(0x9D, "NUMPAD_0", 5, 20, 93),
        new(0xA0, "Logo", 0, 21, 94),
        new(0xA1, "NUMPAD_DIVIDE", 1, 21, 95),
        new(0xA2, "NUMPAD_8", 2, 21, 96),
        new(0xA3, "NUMPAD_5", 3, 21, 97),
        new(0xA4, "NUMPAD_2", 4, 21, 98),
        new(0xA9, "NUMPAD_TIMES", 1, 22, 99),
        new(0xAA, "NUMPAD_9", 2, 22, 100),
        new(0xAB, "NUMPAD_6", 3, 22, 101),
        new(0xAC, "NUMPAD_3", 4, 22, 102),
        new(0xAD, "NUMPAD_PERIOD", 5, 22, 103),
        new(0xB1, "NUMPAD_MINUS", 1, 23, 104),
        new(0xB2, "NUMPAD_PLUS", 2, 23, 105),
        new(0xB4, "NUMPAD_ENTER", 4, 23, 106),
    };

    /// <summary>
    /// The Scope II keyboard profile (107-LED ANSI). Shared by Scope II RX and NX,
    /// which are layout-identical. This is the canonical profile the render path
    /// uses; the static members below delegate to it for tests / reference.
    /// </summary>
    public static KeyboardProfile Profile { get; } =
        new("Strix Scope II", CoreConstants.MatrixRows, CoreConstants.MatrixCols, _keys);

    /// <summary>All 107 LEDs in Direct-packet render order.</summary>
    public static ReadOnlySpan<LedKey> Keys => Profile.Keys;

    /// <summary>The LED at a render index (0..106).</summary>
    public static ref readonly LedKey ByIndex(int index) => ref Profile.ByIndex(index);

    /// <summary>Render index for a device key id, or -1 if not addressable.</summary>
    public static int IndexForKeyId(byte keyId) => Profile.IndexForKeyId(keyId);

    /// <summary>Matrix geometry (6 rows × 24 cols).</summary>
    public static int Rows => Profile.Rows;
    public static int Cols => Profile.Cols;
}
