using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace ImpactSE.Core;

/// <summary>Feature-report access to the mouse's configuration collection through the Windows HID API.</summary>
[SupportedOSPlatform("windows")]
public sealed class HidTransport : IFeatureTransport
{
    public const ushort VendorId = 0x36B7;
    public const ushort ProductId = 0xFD16;

    // Interface 2, collection 2 (usage page FFA0) carries the feature reports.
    const string PathMarker = "vid_36b7&pid_fd16&mi_02&col02";
    const uint IoctlHidGetFeature = 0xB0192;

    readonly SafeFileHandle _handle;

    HidTransport(SafeFileHandle handle) => _handle = handle;

    /// <summary>Opens the first connected Impact SE, or returns null if none is plugged in.</summary>
    public static HidTransport? OpenFirst()
    {
        foreach (var path in DevicePaths())
        {
            if (!path.Contains(PathMarker, StringComparison.OrdinalIgnoreCase)) continue;
            // Windows requires read/write access to send any feature report.
            var handle = CreateFile(path, 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (!handle.IsInvalid) return new HidTransport(handle);
            handle.Dispose();
        }
        return null;
    }

    public bool SetFeature(byte[] report) => HidD_SetFeature(_handle, report, report.Length);

    public byte[]? GetFeature(byte reportId, byte command, int length)
    {
        var buffer = new byte[length];
        buffer[0] = reportId;
        buffer[1] = command;
        return DeviceIoControl(_handle, IoctlHidGetFeature, buffer, length, buffer, length, out _, IntPtr.Zero)
            ? buffer
            : null;
    }

    public void Dispose() => _handle.Dispose();

    static List<string> DevicePaths()
    {
        var paths = new List<string>();
        HidD_GetHidGuid(out var guid);
        IntPtr set = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, 0x12); // PRESENT | DEVICEINTERFACE
        try
        {
            for (int i = 0; ; i++)
            {
                var data = new SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref data)) break;
                SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out int size, IntPtr.Zero);
                IntPtr detail = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (SetupDiGetDeviceInterfaceDetail(set, ref data, detail, size, out _, IntPtr.Zero))
                        paths.Add(Marshal.PtrToStringUni(detail + 4) ?? "");
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return paths;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }

    [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll", SetLastError = true)] static extern bool HidD_SetFeature(SafeFileHandle h, byte[] buffer, int length);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool DeviceIoControl(SafeFileHandle h, uint code, byte[] inBuf, int inLen, byte[] outBuf, int outLen, out int returned, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("setupapi.dll", SetLastError = true)] static extern IntPtr SetupDiGetClassDevs(ref Guid guid, IntPtr enumerator, IntPtr parent, int flags);
    [DllImport("setupapi.dll", SetLastError = true)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid guid, int index, ref SP_DEVICE_INTERFACE_DATA data);
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detail, int size, out int required, IntPtr info);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
}
