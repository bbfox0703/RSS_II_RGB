// ScopeIIProbe — hardware verification harness for the ASUS ROG Strix Scope II.
//
// Throwaway JIT console (NOT production). P/Invokes the SAME hidapi.dll OpenRGB
// ships, so every packet byte offset maps 1:1 to the ported C++
// (vendor/openrgb AsusAuraTUFKeyboardController.cpp). Production will replace
// this with raw Win32 P/Invoke behind a Core interface (CLAUDE.md rules 4 & 5).
//
// Commands:
//   solid [RRGGBB]      Direct-light every key one colour (default: 00FF66)
//   off                 Direct-light every key black
//   pattern             Map check: WASD=red, arrows=blue, Esc=green, F1-F12=white
//   anim [seconds]      Software rainbow over Direct; reports achieved FPS
//   effect static [RRGGBB]
//   effect breathing [RRGGBB]
//   effect colorcycle
//   effect wave         Hardware effects via the 0x51 0x2C path
//
//   dotnet run --project tools/ScopeIIProbe -- <command> [args]

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace ScopeIIProbe;

internal static class Program
{
    // ----- Device identity ------------------------------------------------

    private const ushort AuraVendorId = 0x0B05; // ASUS

    private static readonly (ushort Pid, string Name)[] KnownPids =
    {
        (0x1AB3, "ROG Strix Scope II"),
        (0x1AB5, "ROG Strix Scope II RX"),
        (0x1AAE, "ROG Strix Scope II 96 Wireless (USB)"),
        (0x1B78, "ROG Strix Scope II 96 RX Wireless (USB)"),
    };

    private const ushort ControlUsagePage = 0xFF00;
    private const int    ControlInterface = 1;

    // ANSI (US) Scope II key ids, verbatim from OpenRGB
    // AsusAuraTUFKeyboardLayouts.h -> AsusROGStrixScopeIILayouts (LAYOUT_US).
    private static readonly byte[] KeyIdsAnsi =
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

    // A few named key ids (ANSI) for the pattern map-check.
    private static readonly byte[] Wasd   = { 0x1A, 0x13, 0x1B, 0x23 };             // W A S D
    private static readonly byte[] Arrows = { 0x85, 0x8C, 0x8D, 0x95 };             // L U D R
    private const byte EscId = 0x00;
    private static readonly byte[] FnRow  = { 0x18,0x20,0x28,0x30,0x40,0x48,        // F1..F6
                                              0x50,0x58,0x60,0x68,0x70,0x78 };      // F7..F12

    private const int ReportLength = 65; // 1 report-id byte + 64 payload

    // Scope II effect tuning (from RGBController_AsusAuraTUFKeyboard).
    private const byte EffectSpeed      = 30;   // range 255(slow)..0(fast)
    private const byte EffectBrightness = 100;  // brightness 4 * 25

    private static int Main(string[] args)
    {
        if (hid_init() != 0)
        {
            Console.Error.WriteLine("hid_init() failed.");
            return 1;
        }

        try
        {
            IntPtr dev = OpenDevice(out string name);
            if (dev == IntPtr.Zero) return 1;

            try
            {
                Console.WriteLine($"\nOpened: {name}");
                Console.WriteLine($"  Firmware version : {GetVersion(dev)}");
                int layout = GetLayout(dev);
                Console.WriteLine($"  Layout id        : {(layout < 0 ? "(no response)" : layout.ToString())}\n");

                return Dispatch(dev, args);
            }
            finally
            {
                hid_close(dev);
            }
        }
        finally
        {
            hid_exit();
        }
    }

    private static int Dispatch(IntPtr dev, string[] args)
    {
        string cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "solid";

        switch (cmd)
        {
            case "solid":
            {
                (byte r, byte g, byte b) = args.Length > 1 ? ParseColor(args[1]) : ((byte)0x00, (byte)0xFF, (byte)0x66);
                Console.WriteLine($"Direct solid 0x{r:X2}{g:X2}{b:X2} -> {KeyIdsAnsi.Length} keys");
                return Report(LightAll(dev, _ => (r, g, b)));
            }

            case "off":
                Console.WriteLine("Direct off (black)");
                return Report(LightAll(dev, _ => (0, 0, 0)));

            case "pattern":
                return Report(RunPattern(dev));

            case "anim":
            {
                int seconds = args.Length > 1 && int.TryParse(args[1], out int s) ? s : 5;
                return Report(RunAnimation(dev, seconds));
            }

            case "effect":
            {
                string fx = args.Length > 1 ? args[1].ToLowerInvariant() : "";
                (byte r, byte g, byte b) = args.Length > 2 ? ParseColor(args[2]) : ((byte)0xFF, (byte)0x00, (byte)0x00);
                return Report(RunEffect(dev, fx, r, g, b));
            }

            default:
                Console.Error.WriteLine($"Unknown command '{cmd}'. Try: solid | off | pattern | anim | effect");
                return 2;
        }
    }

    private static int Report(bool ok)
    {
        Console.WriteLine(ok ? "OK." : "FAILED (a write returned < 0).");
        return ok ? 0 : 1;
    }

    // ----- Verifications --------------------------------------------------

    // Map check: colour a handful of recognisable keys, everything else off.
    // If WASD is red, the arrow cluster blue, Esc green and the F-row white,
    // the key-id -> physical-key mapping is correct.
    private static bool RunPattern(IntPtr dev)
    {
        Console.WriteLine("Map check: WASD=red, arrows=blue, Esc=green, F1-F12=white, rest off.");

        var red   = new HashSet<byte>(Wasd);
        var blue  = new HashSet<byte>(Arrows);
        var white = new HashSet<byte>(FnRow);

        return LightAll(dev, id =>
        {
            if (id == EscId)     return ((byte)0x00, (byte)0xFF, (byte)0x00);
            if (red.Contains(id))   return ((byte)0xFF, (byte)0x00, (byte)0x00);
            if (blue.Contains(id))  return ((byte)0x00, (byte)0x00, (byte)0xFF);
            if (white.Contains(id)) return ((byte)0xFF, (byte)0xFF, (byte)0xFF);
            return ((byte)0x00, (byte)0x00, (byte)0x00);
        });
    }

    // Software rainbow over Direct — proves we can drive per-frame effects
    // ourselves (the whole reason for a custom app) and measures throughput.
    private static bool RunAnimation(IntPtr dev, int seconds)
    {
        Console.WriteLine($"Software rainbow over Direct for {seconds}s — measuring FPS...");

        var sw = Stopwatch.StartNew();
        long deadline = seconds * 1000L;
        int frames = 0;
        bool ok = true;

        while (sw.ElapsedMilliseconds < deadline)
        {
            double t = sw.Elapsed.TotalSeconds;
            ok &= LightAll(dev, id =>
            {
                int idx = Array.IndexOf(KeyIdsAnsi, id);
                double hue = (idx / (double)KeyIdsAnsi.Length + t * 0.5) % 1.0;
                return HsvToRgb(hue, 1.0, 1.0);
            });
            frames++;
        }

        sw.Stop();
        double secs = sw.Elapsed.TotalSeconds;
        double fps = frames / secs;
        Console.WriteLine($"  {frames} frames in {secs:F2}s = {fps:F1} FPS " +
                          $"({1000.0 / fps:F1} ms/frame, {KeyIdsAnsi.Length} LEDs, " +
                          $"{(KeyIdsAnsi.Length + 14) / 15} packets/frame)");
        return ok;
    }

    private static bool RunEffect(IntPtr dev, string fx, byte r, byte g, byte b)
    {
        switch (fx)
        {
            case "static":
                Console.WriteLine($"Hardware Static 0x{r:X2}{g:X2}{b:X2}");
                return SendGenericEffect(dev, mode: 0x00, colorMode: 0x00, r, g, b, hasColor: true);

            case "breathing":
                Console.WriteLine($"Hardware Breathing 0x{r:X2}{g:X2}{b:X2}");
                return SendGenericEffect(dev, mode: 0x01, colorMode: 0x10, r, g, b, hasColor: true);

            case "colorcycle":
                Console.WriteLine("Hardware Color Cycle (rainbow)");
                return SendGenericEffect(dev, mode: 0x02, colorMode: 0x00, 0, 0, 0, hasColor: false);

            case "wave":
                Console.WriteLine("Hardware Wave (rainbow, Scope II 3-packet path)");
                return SendScopeIIWave(dev);

            default:
                Console.Error.WriteLine($"Unknown effect '{fx}'. Try: static | breathing | colorcycle | wave");
                return false;
        }
    }

    // ----- Protocol -------------------------------------------------------

    // Direct per-LED: header C0 81 <count> 00, then 4 bytes/LED (id,R,G,B),
    // 15 LEDs per packet. colorFor maps a key id to its colour.
    private static bool LightAll(IntPtr dev, Func<byte, (byte r, byte g, byte b)> colorFor)
    {
        const int perPacket = 15;
        int total = KeyIdsAnsi.Length;
        int packets = (total + perPacket - 1) / perPacket;
        bool allOk = true;

        for (int p = 0; p < packets; p++)
        {
            byte[] buf = NewReport();
            int offset = p * perPacket;
            int count = Math.Min(perPacket, total - offset);

            buf[1] = 0xC0;
            buf[2] = 0x81;
            buf[3] = (byte)count;
            buf[4] = 0x00;

            for (int j = 0; j < count; j++)
            {
                byte id = KeyIdsAnsi[offset + j];
                (byte r, byte g, byte b) = colorFor(id);
                buf[j * 4 + 5] = id;
                buf[j * 4 + 6] = r;
                buf[j * 4 + 7] = g;
                buf[j * 4 + 8] = b;
            }

            Drain(dev);
            if (Write(dev, buf) < 0) allOk = false;
            ReadTimeout(dev, _scratch, 20); // AwaitResponse(20)
        }

        return allOk;
    }

    // Generic 0x51 0x2C effect packet (Static / Breathing / Color Cycle).
    private static bool SendGenericEffect(IntPtr dev, byte mode, byte colorMode,
                                          byte r, byte g, byte b, bool hasColor)
    {
        byte[] buf = NewReport();
        buf[1] = 0x51;
        buf[2] = 0x2C;
        buf[3] = mode;
        buf[4] = 0x00;
        buf[5] = EffectSpeed;
        buf[6] = EffectBrightness;
        buf[7] = colorMode;
        buf[8] = 0x00;          // direction
        buf[9] = 0x02;          // per-led keyboard
        if (hasColor)
        {
            buf[10] = r;
            buf[11] = g;
            buf[12] = b;
        }

        Drain(dev);
        bool ok = Write(dev, buf) >= 0;
        ReadTimeout(dev, _scratch, 20);
        return ok;
    }

    // Scope II rainbow Wave: 3 chained packets, no colours (firmware rainbow).
    // Ported from UpdateScopeIIRainbowRipple with an empty colour list.
    private static bool SendScopeIIWave(IntPtr dev)
    {
        bool ok = true;

        byte[] p1 = NewReport();
        p1[1] = 0x51; p1[2] = 0x2C;
        p1[3] = 0x04;           // WAVE
        p1[4] = 0x02;
        p1[5] = EffectSpeed;
        p1[6] = EffectBrightness;
        p1[7] = 0x00;           // color_mode
        p1[8] = 0x00;           // direction
        p1[9] = 0x02;
        p1[10] = 0x00;          // colour count
        ok &= SendRaw(dev, p1);

        byte[] p2 = NewReport();
        p2[1] = 0x51; p2[2] = 0x2C; p2[3] = 0x04; p2[4] = 0x01;
        ok &= SendRaw(dev, p2);

        byte[] p3 = NewReport();
        p3[1] = 0x51; p3[2] = 0x2C; p3[3] = 0x04; p3[4] = 0x00;
        ok &= SendRaw(dev, p3);

        return ok;
    }

    private static bool SendRaw(IntPtr dev, byte[] buf)
    {
        Drain(dev);
        bool ok = Write(dev, buf) >= 0;
        ReadTimeout(dev, _scratch, 20);
        return ok;
    }

    // GetVersion: cmd 0x12 0x00 -> bytes [6][5][4].
    private static string GetVersion(IntPtr dev)
    {
        byte[] w = NewReport();
        w[1] = 0x12;
        w[2] = 0x00;
        Drain(dev);
        if (Write(dev, w) < 0) return "(write failed)";

        byte[] resp = new byte[ReportLength];
        if (ReadTimeout(dev, resp, 300) <= 0) return "(no response)";
        return $"{resp[6]:X2}.{resp[5]:X2}.{resp[4]:X2}";
    }

    // GetLayout: cmd 0x12 0x12 -> out[4]*100 + out[5].
    private static int GetLayout(IntPtr dev)
    {
        byte[] w = NewReport();
        w[1] = 0x12;
        w[2] = 0x12;
        Drain(dev);
        if (Write(dev, w) < 0) return -1;

        byte[] resp = new byte[ReportLength];
        if (ReadTimeout(dev, resp, 300) <= 0) return -1;
        return resp[4] * 100 + resp[5];
    }

    // ----- Device open ----------------------------------------------------

    private static IntPtr OpenDevice(out string name)
    {
        name = "";
        Console.WriteLine("Scanning for ASUS HID interfaces (VID 0x0B05)...");

        IntPtr enumeration = hid_enumerate(AuraVendorId, 0x0000);
        IntPtr matchedPath = IntPtr.Zero;
        int asusCount = 0;

        for (IntPtr cur = enumeration; cur != IntPtr.Zero;)
        {
            HidDeviceInfo info = Marshal.PtrToStructure<HidDeviceInfo>(cur);
            asusCount++;

            string? known = KnownPids.FirstOrDefault(p => p.Pid == info.ProductId).Name;
            Console.WriteLine(
                $"  PID 0x{info.ProductId:X4}  iface {info.InterfaceNumber,2}  " +
                $"usage 0x{info.UsagePage:X4}/0x{info.Usage:X2}  {(known ?? "(other)")}");

            if (known != null && info.UsagePage == ControlUsagePage
                              && info.InterfaceNumber == ControlInterface
                              && matchedPath == IntPtr.Zero)
            {
                matchedPath = info.Path;
                name = known;
            }

            cur = info.Next;
        }

        if (asusCount == 0)
        {
            Console.Error.WriteLine("No ASUS (0x0B05) HID devices found. Is the keyboard plugged in?");
            hid_free_enumeration(enumeration);
            return IntPtr.Zero;
        }

        if (matchedPath == IntPtr.Zero)
        {
            Console.Error.WriteLine("\nNo Strix Scope II control interface (usage 0xFF00, iface 1) found above.");
            hid_free_enumeration(enumeration);
            return IntPtr.Zero;
        }

        IntPtr dev = hid_open_path(matchedPath);
        hid_free_enumeration(enumeration);

        if (dev == IntPtr.Zero)
            Console.Error.WriteLine($"\nFound {name} but could not open it — Armoury Crate / OpenRGB may be holding it.");

        return dev;
    }

    // ----- HID helpers ----------------------------------------------------

    private static readonly byte[] _scratch = new byte[ReportLength];

    private static byte[] NewReport() => new byte[ReportLength];
    private static int Write(IntPtr dev, byte[] buf) => hid_write(dev, buf, (nuint)buf.Length);
    private static int ReadTimeout(IntPtr dev, byte[] buf, int ms) => hid_read_timeout(dev, buf, (nuint)buf.Length, ms);

    private static void Drain(IntPtr dev)
    {
        while (ReadTimeout(dev, _scratch, 0) > 0) { }
    }

    private static (byte r, byte g, byte b) ParseColor(string s)
    {
        s = s.TrimStart('#');
        if (s.Length == 6
            && byte.TryParse(s.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r)
            && byte.TryParse(s.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g)
            && byte.TryParse(s.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
        {
            return (r, g, b);
        }
        Console.Error.WriteLine($"Bad colour '{s}', using white.");
        return (0xFF, 0xFF, 0xFF);
    }

    private static (byte r, byte g, byte b) HsvToRgb(double h, double s, double v)
    {
        double r = 0, g = 0, b = 0;
        int i = (int)(h * 6) % 6;
        double f = h * 6 - Math.Floor(h * 6);
        double p = v * (1 - s);
        double q = v * (1 - f * s);
        double t = v * (1 - (1 - f) * s);
        switch (i)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            case 5: r = v; g = p; b = q; break;
        }
        return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    // ----- hidapi P/Invoke ------------------------------------------------

    private const string Lib = "hidapi";

    [DllImport(Lib)] private static extern int hid_init();
    [DllImport(Lib)] private static extern int hid_exit();
    [DllImport(Lib)] private static extern IntPtr hid_enumerate(ushort vendorId, ushort productId);
    [DllImport(Lib)] private static extern void hid_free_enumeration(IntPtr devs);
    [DllImport(Lib)] private static extern IntPtr hid_open_path(IntPtr path);
    [DllImport(Lib)] private static extern int hid_write(IntPtr device, byte[] data, nuint length);
    [DllImport(Lib)] private static extern int hid_read_timeout(IntPtr device, byte[] data, nuint length, int milliseconds);
    [DllImport(Lib)] private static extern void hid_close(IntPtr device);

    [StructLayout(LayoutKind.Sequential)]
    private struct HidDeviceInfo
    {
        public IntPtr Path;
        public ushort VendorId;
        public ushort ProductId;
        public IntPtr SerialNumber;
        public ushort ReleaseNumber;
        public IntPtr ManufacturerString;
        public IntPtr ProductString;
        public ushort UsagePage;
        public ushort Usage;
        public int    InterfaceNumber;
        public IntPtr Next;
    }
}
