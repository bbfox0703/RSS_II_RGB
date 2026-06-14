namespace RSS_II_RGB.Core;

/// <summary>
/// Centralised magic values for the Core project (CLAUDE.md rule 8).
/// Protocol bytes, frame geometry, and timing. Nothing platform-specific lives here.
/// </summary>
public static class CoreConstants
{
    // ----- Frame geometry (Strix Scope II, ANSI) -----
    public const int LedCount = 107;
    public const int MatrixRows = 6;
    public const int MatrixCols = 24;

    // ----- Direct HID protocol -----
    // Ported from vendor/openrgb AsusAuraTUFKeyboardController.cpp (UpdateLeds).
    public const int ReportLength = 65;     // 1 report-id byte + 64 payload
    public const int LedsPerPacket = 15;    // 5-byte header, then 4 bytes/LED
    public const int PacketsPerFrame = 8;   // ceil(107 / 15)
    public const byte DirectHeaderHi = 0xC0;
    public const byte DirectHeaderLo = 0x81;

    // Other commands (vendor/openrgb AsusAuraTUFKeyboardController.cpp):
    public const byte CmdQuery = 0x12;  // 12 00 -> firmware version, 12 12 -> layout id
    public const byte QueryVersion = 0x00;
    public const byte QueryLayout = 0x12;
    public const byte CmdEffect = 0x51;  // 51 2C ... -> hardware effect
    public const byte EffectArg = 0x2C;
    public const byte EffectColorModeSpecific = 0x10; // breathing with a fixed colour
    public const byte EffectPerLedFlag = 0x02;
    public const byte CmdSave = 0x50;    // 50 55 -> persist current mode to flash
    public const byte SaveArg = 0x55;

    // ----- Render loop -----
    public const int DefaultTargetFps = 40; // device sustains ~42 FPS
}
