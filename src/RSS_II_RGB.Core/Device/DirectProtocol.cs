using RSS_II_RGB.Core.Layout;
using RSS_II_RGB.Core.Rendering;

namespace RSS_II_RGB.Core.Device;

/// <summary>
/// Pure builder for the "Direct" per-LED HID command (no I/O — unit-testable).
/// Ported from vendor/openrgb AsusAuraTUFKeyboardController.cpp (UpdateLeds).
///
/// One frame = <see cref="CoreConstants.PacketsPerFrame"/> packets of
/// <see cref="CoreConstants.ReportLength"/> bytes. Each packet:
///   [0] = 0x00 (HID report id)
///   [1] = 0xC0, [2] = 0x81, [3] = led count in this packet, [4] = 0x00
///   then 4 bytes per LED: keyId, R, G, B
/// LEDs are emitted in <see cref="ScopeIILayout.Keys"/> order, 15 per packet.
/// </summary>
public static class DirectProtocol
{
    /// <summary>Total bytes for one frame's packets (8 * 65 = 520).</summary>
    public const int FrameBufferSize = CoreConstants.PacketsPerFrame * CoreConstants.ReportLength;

    /// <summary>
    /// Serialise <paramref name="pixels"/> (107 colours, render order) into
    /// <paramref name="dest"/> (>= <see cref="FrameBufferSize"/> bytes) as the
    /// 8 ready-to-write Direct packets. Padding bytes are zeroed.
    /// </summary>
    public static void BuildFrame(ReadOnlySpan<Rgb> pixels, Span<byte> dest)
    {
        if (pixels.Length != CoreConstants.LedCount)
        {
            throw new ArgumentException($"Expected {CoreConstants.LedCount} pixels, got {pixels.Length}.", nameof(pixels));
        }
        if (dest.Length < FrameBufferSize)
        {
            throw new ArgumentException($"Destination needs at least {FrameBufferSize} bytes.", nameof(dest));
        }

        dest.Clear(); // report id 0x00 + all padding

        ReadOnlySpan<LedKey> keys = ScopeIILayout.Keys;

        for (int p = 0; p < CoreConstants.PacketsPerFrame; p++)
        {
            int offset = p * CoreConstants.LedsPerPacket;
            int count = Math.Min(CoreConstants.LedsPerPacket, keys.Length - offset);
            if (count <= 0)
            {
                break;
            }

            Span<byte> packet = dest.Slice(p * CoreConstants.ReportLength, CoreConstants.ReportLength);
            packet[1] = CoreConstants.DirectHeaderHi; // 0xC0
            packet[2] = CoreConstants.DirectHeaderLo; // 0x81
            packet[3] = (byte)count;
            packet[4] = 0x00;

            for (int j = 0; j < count; j++)
            {
                Rgb c = pixels[offset + j];
                int b = j * 4 + 5;
                packet[b] = keys[offset + j].KeyId;
                packet[b + 1] = c.R;
                packet[b + 2] = c.G;
                packet[b + 3] = c.B;
            }
        }
    }
}
