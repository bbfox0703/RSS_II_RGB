using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using RSS_II_RGB.Core;
using RSS_II_RGB.Core.Device;
using RSS_II_RGB.Core.Rendering;
using RSS_II_RGB.Windows.Interop;

namespace RSS_II_RGB.Windows;

/// <summary>
/// Finds a supported Strix Scope II keyboard's vendor control interface (usage
/// page 0xFF00, interface 1) by walking the HID device interfaces with SetupAPI,
/// and opens it. See <see cref="WindowsConstants.ProductIdTokens"/> for the PIDs.
/// </summary>
public sealed class Win32KeyboardDeviceFactory : IKeyboardDeviceFactory
{
    public Task<IKeyboardDevice?> FindAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        string? path = FindControlInterfacePath();
        if (path is null)
        {
            return Task.FromResult<IKeyboardDevice?>(null);
        }

        SafeFileHandle handle = NativeMethods.CreateFileW(
            path,
            NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            0, NativeMethods.OPEN_EXISTING, 0, 0);

        if (handle.IsInvalid)
        {
            handle.Dispose();
            return Task.FromResult<IKeyboardDevice?>(null); // likely held by Armoury Crate / OpenRGB
        }

        return Task.FromResult<IKeyboardDevice?>(new Win32KeyboardDevice(handle));
    }

    private static string? FindControlInterfacePath()
    {
        NativeMethods.HidD_GetHidGuid(out Guid hidGuid);

        nint set = NativeMethods.SetupDiGetClassDevsW(in hidGuid, 0, 0,
            NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE);
        if (set == NativeMethods.INVALID_HANDLE_VALUE)
        {
            return null;
        }

        try
        {
            var ifData = new NativeMethods.SP_DEVICE_INTERFACE_DATA
            {
                cbSize = (uint)Unsafe.SizeOf<NativeMethods.SP_DEVICE_INTERFACE_DATA>(),
            };

            for (uint index = 0;
                 NativeMethods.SetupDiEnumDeviceInterfaces(set, 0, in hidGuid, index, ref ifData);
                 index++)
            {
                string? path = GetDetailPath(set, ref ifData);
                if (path is null)
                {
                    continue;
                }

                // Cheap path-token filter before opening anything.
                if (!ContainsToken(path, WindowsConstants.VendorIdToken) ||
                    !MatchesAnyProductId(path) ||
                    !ContainsToken(path, WindowsConstants.ControlInterfaceToken))
                {
                    continue;
                }

                // The same interface exposes several collections; confirm the
                // vendor control one via its HID usage page.
                if (MatchesControlUsage(path))
                {
                    return path;
                }
            }
        }
        finally
        {
            NativeMethods.SetupDiDestroyDeviceInfoList(set);
        }

        return null;
    }

    private static string? GetDetailPath(nint set, ref NativeMethods.SP_DEVICE_INTERFACE_DATA ifData)
    {
        // First call: discover the required buffer size.
        NativeMethods.SetupDiGetDeviceInterfaceDetailW(set, ref ifData, 0, 0, out uint required, 0);
        if (required == 0)
        {
            return null;
        }

        nint buffer = Marshal.AllocHGlobal((int)required);
        try
        {
            // SP_DEVICE_INTERFACE_DETAIL_DATA_W.cbSize: 8 on x64, 6 on x86. The
            // UTF-16 DevicePath follows the DWORD at byte offset 4.
            Marshal.WriteInt32(buffer, 0, nint.Size == 8 ? 8 : 6);
            if (!NativeMethods.SetupDiGetDeviceInterfaceDetailW(set, ref ifData, buffer, required, out _, 0))
            {
                return null;
            }
            return Marshal.PtrToStringUni(buffer + 4);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool MatchesControlUsage(string path)
    {
        using SafeFileHandle h = NativeMethods.CreateFileW(
            path, 0,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            0, NativeMethods.OPEN_EXISTING, 0, 0);
        if (h.IsInvalid)
        {
            return false;
        }

        if (!NativeMethods.HidD_GetPreparsedData(h, out nint preparsed) || preparsed == 0)
        {
            return false;
        }

        try
        {
            var caps = default(NativeMethods.HIDP_CAPS);
            if (NativeMethods.HidP_GetCaps(preparsed, ref caps) != NativeMethods.HIDP_STATUS_SUCCESS)
            {
                return false;
            }
            return caps.UsagePage == WindowsConstants.ControlUsagePage
                && caps.Usage == WindowsConstants.ControlUsage;
        }
        finally
        {
            NativeMethods.HidD_FreePreparsedData(preparsed);
        }
    }

    private static bool ContainsToken(string path, string token)
        => path.Contains(token, StringComparison.OrdinalIgnoreCase);

    // True when the path names any supported keyboard PID.
    private static bool MatchesAnyProductId(string path)
    {
        foreach (string token in WindowsConstants.ProductIdTokens)
        {
            if (ContainsToken(path, token))
            {
                return true;
            }
        }
        return false;
    }
}

/// <summary>
/// Drives the keyboard over the opened HID control interface. Synchronous Win32
/// calls run on the engine's background thread (never the UI thread), so the
/// async methods complete inline.
/// </summary>
public sealed class Win32KeyboardDevice : IKeyboardDevice
{
    private readonly SafeFileHandle _handle;
    private readonly byte[] _frameBuffer = new byte[DirectProtocol.FrameBufferSize];

    internal Win32KeyboardDevice(SafeFileHandle handle) => _handle = handle;

    public bool IsOpen => !_handle.IsInvalid && !_handle.IsClosed;

    public Task WriteFrameAsync(LedFrame frame, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        DirectProtocol.BuildFrame(frame.Pixels, _frameBuffer);
        for (int p = 0; p < CoreConstants.PacketsPerFrame; p++)
        {
            WriteReport(_frameBuffer.AsSpan(p * CoreConstants.ReportLength, CoreConstants.ReportLength));
        }
        return Task.CompletedTask;
    }

    public Task ApplyHardwareEffectAsync(HardwareEffect effect, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        Span<byte> buf = stackalloc byte[CoreConstants.ReportLength];
        buf.Clear();
        buf[1] = CoreConstants.CmdEffect;
        buf[2] = CoreConstants.EffectArg;
        buf[3] = (byte)effect.Kind;
        buf[4] = 0x00;
        buf[5] = effect.Speed;
        buf[6] = effect.Brightness;
        buf[7] = effect.Kind == HardwareEffectKind.Breathing ? CoreConstants.EffectColorModeSpecific : (byte)0x00;
        buf[8] = 0x00; // direction
        buf[9] = CoreConstants.EffectPerLedFlag;
        if (effect.Kind != HardwareEffectKind.ColorCycle)
        {
            buf[10] = effect.Color.R;
            buf[11] = effect.Color.G;
            buf[12] = effect.Color.B;
        }

        WriteReport(buf);
        return Task.CompletedTask;
    }

    public Task SaveToFlashAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        Span<byte> buf = stackalloc byte[CoreConstants.ReportLength];
        buf.Clear();
        buf[1] = CoreConstants.CmdSave;
        buf[2] = CoreConstants.SaveArg;
        WriteReport(buf);
        return Task.CompletedTask;
    }

    public Task<DeviceInfo> ReadInfoAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        Span<byte> cmd = stackalloc byte[CoreConstants.ReportLength];
        Span<byte> resp = stackalloc byte[CoreConstants.ReportLength];

        // Firmware version: 12 00. Raw ReadFile keeps the leading report-id byte
        // (hidapi stripped it), so every read offset is +1 vs the POC.
        NativeMethods.HidD_FlushQueue(_handle);
        cmd.Clear();
        cmd[1] = CoreConstants.CmdQuery;
        cmd[2] = CoreConstants.QueryVersion;
        WriteReport(cmd);

        string version = "unknown";
        if (ReadReport(resp))
        {
            version = $"{resp[7]:X2}.{resp[6]:X2}.{resp[5]:X2}";
        }

        // Layout id: 12 12 -> resp[5]*100 + resp[6]  (POC [4]*100 + [5], +1).
        NativeMethods.HidD_FlushQueue(_handle);
        cmd.Clear();
        cmd[1] = CoreConstants.CmdQuery;
        cmd[2] = CoreConstants.QueryLayout;
        WriteReport(cmd);

        int layout = -1;
        if (ReadReport(resp))
        {
            layout = resp[5] * 100 + resp[6];
        }

        return Task.FromResult(new DeviceInfo(version, layout));
    }

    public ValueTask DisposeAsync()
    {
        _handle.Dispose();
        return ValueTask.CompletedTask;
    }

    private void WriteReport(Span<byte> report)
    {
        if (!NativeMethods.WriteFile(_handle, ref MemoryMarshal.GetReference(report),
                (uint)report.Length, out _, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "WriteFile to keyboard failed.");
        }
    }

    private bool ReadReport(Span<byte> buffer)
    {
        buffer.Clear();
        if (!NativeMethods.ReadFile(_handle, ref MemoryMarshal.GetReference(buffer),
                (uint)buffer.Length, out uint read, 0))
        {
            return false;
        }
        return read > 0;
    }
}
