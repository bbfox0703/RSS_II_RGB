namespace RSS_II_RGB.Windows;

/// <summary>
/// Centralised magic values for the Windows platform project (CLAUDE.md rule 8).
/// Native DLL names, device identifiers, and Win32 flags — the only place these live.
/// </summary>
internal static class WindowsConstants
{
    // ----- Native libraries -----
    public const string Hid = "hid.dll";
    public const string SetupApi = "setupapi.dll";
    public const string User32 = "user32.dll";
    public const string Kernel32 = "kernel32.dll";

    // ----- Target device (Strix Scope II RX), verified on hardware -----
    public const ushort AsusVendorId = 0x0B05;
    public const ushort ScopeIIRxProductId = 0x1AB5;
    public const ushort ControlUsagePage = 0xFF00;
    public const ushort ControlUsage = 0x0001;

    // Tokens that appear in the HID device interface path, e.g.
    // \\?\hid#vid_0b05&pid_1ab5&mi_01&col02#... — used as a cheap pre-filter.
    public const string VendorIdToken = "vid_0b05";
    public const string ProductIdToken = "pid_1ab5";

    // The vendor control collection appears as interface 1 in the device path.
    public const string ControlInterfaceToken = "mi_01";
}
