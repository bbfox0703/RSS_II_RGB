using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace RSS_II_RGB.Windows.Interop;

/// <summary>
/// Raw Win32 HID + SetupAPI bindings. All source-generated <see cref="LibraryImportAttribute"/>
/// (AOT-safe, CLAUDE.md rule 5). Native structs are read via blittable layouts —
/// no reflection-based marshalling.
/// </summary>
internal static partial class NativeMethods
{
    // ----- SetupAPI flags -----
    public const uint DIGCF_PRESENT = 0x00000002;
    public const uint DIGCF_DEVICEINTERFACE = 0x00000010;

    // ----- CreateFile -----
    public const uint GENERIC_READ = 0x80000000;
    public const uint GENERIC_WRITE = 0x40000000;
    public const uint FILE_SHARE_READ = 0x00000001;
    public const uint FILE_SHARE_WRITE = 0x00000002;
    public const uint OPEN_EXISTING = 3;

    // ----- HidP_GetCaps NTSTATUS -----
    public const int HIDP_STATUS_SUCCESS = 0x00110000;

    public static readonly nint INVALID_HANDLE_VALUE = -1;

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVICE_INTERFACE_DATA
    {
        public uint cbSize;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public nint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HIDD_ATTRIBUTES
    {
        public uint Size;
        public ushort VendorID;
        public ushort ProductID;
        public ushort VersionNumber;
    }

    // HIDP_CAPS is 64 bytes; we only read the first two USAGE fields. Size=64
    // guarantees HidP_GetCaps doesn't write past our buffer.
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct HIDP_CAPS
    {
        public ushort Usage;
        public ushort UsagePage;
    }

    [LibraryImport("hid.dll")]
    public static partial void HidD_GetHidGuid(out Guid hidGuid);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    public static partial nint SetupDiGetClassDevsW(in Guid classGuid, nint enumerator, nint hwndParent, uint flags);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetupDiEnumDeviceInterfaces(nint deviceInfoSet, nint deviceInfoData,
        in Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetupDiGetDeviceInterfaceDetailW(nint deviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, nint detailData, uint detailSize,
        out uint requiredSize, nint deviceInfoData);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetupDiDestroyDeviceInfoList(nint deviceInfoSet);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode,
        nint securityAttributes, uint creationDisposition, uint flagsAndAttributes, nint templateFile);

    [LibraryImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool HidD_GetAttributes(SafeFileHandle device, ref HIDD_ATTRIBUTES attributes);

    [LibraryImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool HidD_GetPreparsedData(SafeFileHandle device, out nint preparsedData);

    [LibraryImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool HidD_FreePreparsedData(nint preparsedData);

    [LibraryImport("hid.dll")]
    public static partial int HidP_GetCaps(nint preparsedData, ref HIDP_CAPS capabilities);

    [LibraryImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool HidD_FlushQueue(SafeFileHandle device);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool WriteFile(SafeFileHandle handle, ref byte buffer, uint numberOfBytesToWrite,
        out uint numberOfBytesWritten, nint overlapped);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ReadFile(SafeFileHandle handle, ref byte buffer, uint numberOfBytesToRead,
        out uint numberOfBytesRead, nint overlapped);
}
