using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.Core.Device;

/// <summary>Firmware identity read back from the keyboard.</summary>
public readonly record struct DeviceInfo(string FirmwareVersion, int LayoutId);

/// <summary>A firmware (hardware) lighting mode — persists after the app closes.</summary>
public enum HardwareEffectKind : byte
{
    Static = 0x00,
    Breathing = 0x01,
    ColorCycle = 0x02,
}

/// <summary>Parameters for a firmware effect. Speed: 255 (slow)..0 (fast). Brightness: 0..100.</summary>
public readonly record struct HardwareEffect(HardwareEffectKind Kind, Rgb Color, byte Speed = 30, byte Brightness = 100);

/// <summary>
/// A connected keyboard we can drive. The single platform seam for LED output
/// (CLAUDE.md rule 4) — the only implementation lives in the Windows project.
/// </summary>
public interface IKeyboardDevice : IAsyncDisposable
{
    bool IsOpen { get; }

    /// <summary>Stream one software-rendered frame (the 8 Direct packets).</summary>
    Task WriteFrameAsync(LedFrame frame, CancellationToken ct = default);

    /// <summary>Switch the keyboard to a self-running firmware effect.</summary>
    Task ApplyHardwareEffectAsync(HardwareEffect effect, CancellationToken ct = default);

    /// <summary>Persist the current mode to flash so it survives power loss / app exit.</summary>
    Task SaveToFlashAsync(CancellationToken ct = default);

    /// <summary>Read firmware version and layout id.</summary>
    Task<DeviceInfo> ReadInfoAsync(CancellationToken ct = default);
}

/// <summary>Finds and opens the target keyboard.</summary>
public interface IKeyboardDeviceFactory
{
    /// <summary>Locate the Scope II control interface and open it, or null if not found / in use.</summary>
    Task<IKeyboardDevice?> FindAsync(CancellationToken ct = default);
}
