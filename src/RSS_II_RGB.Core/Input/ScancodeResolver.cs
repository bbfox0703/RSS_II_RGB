using RSS_II_RGB.Core.Layout;

namespace RSS_II_RGB.Core.Input;

/// <summary>
/// Resolves a Windows keyboard scan code (Set 1) + extended flag to a render
/// index for a specific <see cref="KeyboardProfile"/>. Scan codes are physical
/// positions (unlike virtual-key codes, which remap), so the same press always
/// lights the same physical key.
///
/// The extended flag disambiguates keys that share a base scan code — e.g.
/// 0x52 is Insert when extended, Numpad-0 when not; 0x48 is Up vs Numpad-8.
///
/// Keys with no scan code (Fn, the ROG logo, Pause) resolve to -1: they are still
/// lit by base effects, just never react. The scan→keyId table below uses the
/// Scope II key-id scheme; a keyboard with a different scheme would supply its own.
/// </summary>
public sealed class ScancodeResolver
{
    private readonly record struct Entry(byte Scan, bool Extended, byte KeyId);

    // (scan code, extended, key id). Standard Set 1 make codes.
    private static readonly Entry[] _entries =
    {
        // Function row + Esc
        new(0x01, false, 0x00), // Escape
        new(0x3B, false, 0x18), // F1
        new(0x3C, false, 0x20), // F2
        new(0x3D, false, 0x28), // F3
        new(0x3E, false, 0x30), // F4
        new(0x3F, false, 0x40), // F5
        new(0x40, false, 0x48), // F6
        new(0x41, false, 0x50), // F7
        new(0x42, false, 0x58), // F8
        new(0x43, false, 0x60), // F9
        new(0x44, false, 0x68), // F10
        new(0x57, false, 0x70), // F11
        new(0x58, false, 0x78), // F12

        // Number row
        new(0x29, false, 0x01), // ` BACK_TICK
        new(0x02, false, 0x11), // 1
        new(0x03, false, 0x19), // 2
        new(0x04, false, 0x21), // 3
        new(0x05, false, 0x29), // 4
        new(0x06, false, 0x31), // 5
        new(0x07, false, 0x39), // 6
        new(0x08, false, 0x41), // 7
        new(0x09, false, 0x49), // 8
        new(0x0A, false, 0x51), // 9
        new(0x0B, false, 0x59), // 0
        new(0x0C, false, 0x61), // - MINUS
        new(0x0D, false, 0x69), // = EQUALS
        new(0x0E, false, 0x79), // Backspace

        // QWERTY row
        new(0x0F, false, 0x02), // Tab
        new(0x10, false, 0x12), // Q
        new(0x11, false, 0x1A), // W
        new(0x12, false, 0x22), // E
        new(0x13, false, 0x2A), // R
        new(0x14, false, 0x32), // T
        new(0x15, false, 0x3A), // Y
        new(0x16, false, 0x42), // U
        new(0x17, false, 0x4A), // I
        new(0x18, false, 0x52), // O
        new(0x19, false, 0x5A), // P
        new(0x1A, false, 0x62), // [ LEFT_BRACKET
        new(0x1B, false, 0x6A), // ] RIGHT_BRACKET
        new(0x2B, false, 0x7A), // \ ANSI_BACK_SLASH

        // Home row
        new(0x3A, false, 0x03), // Caps Lock
        new(0x1E, false, 0x13), // A
        new(0x1F, false, 0x1B), // S
        new(0x20, false, 0x23), // D
        new(0x21, false, 0x2B), // F
        new(0x22, false, 0x33), // G
        new(0x23, false, 0x3B), // H
        new(0x24, false, 0x43), // J
        new(0x25, false, 0x4B), // K
        new(0x26, false, 0x53), // L
        new(0x27, false, 0x5B), // ; SEMICOLON
        new(0x28, false, 0x63), // ' QUOTE
        new(0x1C, false, 0x7B), // Enter (ANSI_ENTER)

        // Bottom row
        new(0x2A, false, 0x04), // Left Shift
        new(0x2C, false, 0x14), // Z
        new(0x2D, false, 0x1C), // X
        new(0x2E, false, 0x24), // C
        new(0x2F, false, 0x2C), // V
        new(0x30, false, 0x34), // B
        new(0x31, false, 0x3C), // N
        new(0x32, false, 0x44), // M
        new(0x33, false, 0x4C), // , COMMA
        new(0x34, false, 0x54), // . PERIOD
        new(0x35, false, 0x5C), // / FORWARD_SLASH
        new(0x36, false, 0x7C), // Right Shift

        // Modifiers + space
        new(0x1D, false, 0x05), // Left Control
        new(0x5B, true,  0x0D), // Left Windows
        new(0x38, false, 0x15), // Left Alt
        new(0x39, false, 0x35), // Space (middle of 3 SPACE LEDs)
        new(0x38, true,  0x4D), // Right Alt
        new(0x5D, true,  0x65), // Menu (Apps)
        new(0x1D, true,  0x7D), // Right Control

        // Navigation cluster
        new(0x37, true,  0x80), // Print Screen
        new(0x46, false, 0x88), // Scroll Lock
        new(0x52, true,  0x81), // Insert
        new(0x47, true,  0x89), // Home
        new(0x49, true,  0x91), // Page Up
        new(0x53, true,  0x82), // Delete
        new(0x4F, true,  0x8A), // End
        new(0x51, true,  0x92), // Page Down

        // Arrows
        new(0x48, true,  0x8C), // Up
        new(0x4B, true,  0x85), // Left
        new(0x50, true,  0x8D), // Down
        new(0x4D, true,  0x95), // Right

        // Numpad
        new(0x45, false, 0x99), // Num Lock
        new(0x35, true,  0xA1), // Numpad /
        new(0x37, false, 0xA9), // Numpad *
        new(0x4A, false, 0xB1), // Numpad -
        new(0x47, false, 0x9A), // Numpad 7
        new(0x48, false, 0xA2), // Numpad 8
        new(0x49, false, 0xAA), // Numpad 9
        new(0x4E, false, 0xB2), // Numpad +
        new(0x4B, false, 0x9B), // Numpad 4
        new(0x4C, false, 0xA3), // Numpad 5
        new(0x4D, false, 0xAB), // Numpad 6
        new(0x4F, false, 0x9C), // Numpad 1
        new(0x50, false, 0xA4), // Numpad 2
        new(0x51, false, 0xAC), // Numpad 3
        new(0x52, false, 0x9D), // Numpad 0
        new(0x53, false, 0xAD), // Numpad .
        new(0x1C, true,  0xB4), // Numpad Enter
    };

    private readonly int[] _normal;
    private readonly int[] _extended;

    public ScancodeResolver(KeyboardProfile profile)
    {
        _normal = BuildTable(profile, extended: false);
        _extended = BuildTable(profile, extended: true);
    }

    /// <summary>Render index for a (scan code, extended) press, or -1 if none.</summary>
    public int ToKeyIndex(uint scanCode, bool extended)
    {
        if (scanCode > 0xFF)
        {
            return -1;
        }
        return (extended ? _extended : _normal)[(int)scanCode];
    }

    private static int[] BuildTable(KeyboardProfile profile, bool extended)
    {
        var table = new int[256];
        Array.Fill(table, -1);
        foreach (Entry e in _entries)
        {
            if (e.Extended == extended)
            {
                table[e.Scan] = profile.IndexForKeyId(e.KeyId);
            }
        }
        return table;
    }
}
