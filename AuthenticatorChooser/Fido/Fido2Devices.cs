using System.Runtime.InteropServices;

namespace AuthenticatorChooser.Fido;

internal sealed class Fido2Devices: IFido2DeviceCounter {

    public const ushort CtapUsagePage = 0xF1D0;

    private const uint RidiDeviceInfo = 0x2000000b;
    private const uint RimTypeHid = 2;

    public int? CountCtapHid() {
        uint deviceCount = 0;
        uint itemSize = (uint) Marshal.SizeOf<RawInputDeviceList>();
        if (GetRawInputDeviceList(IntPtr.Zero, ref deviceCount, itemSize) == uint.MaxValue) {
            return null;
        }

        if (deviceCount == 0) {
            return 0;
        }

        IntPtr list = Marshal.AllocHGlobal((int) (deviceCount * itemSize));
        try {
            if (GetRawInputDeviceList(list, ref deviceCount, itemSize) == uint.MaxValue) {
                return null;
            }

            int fido = 0;
            for (uint i = 0; i < deviceCount; i++) {
                RawInputDeviceList device = Marshal.PtrToStructure<RawInputDeviceList>(list + (int) (i * itemSize));
                if (device.dwType != RimTypeHid) {
                    continue;
                }

                if (GetUsagePage(device.hDevice) == CtapUsagePage) {
                    fido++;
                }
            }

            return fido;
        } finally {
            Marshal.FreeHGlobal(list);
        }
    }

    public static int CountCtap(IEnumerable<ushort> usagePages) =>
        usagePages.Count(page => page == CtapUsagePage);

    private static ushort GetUsagePage(IntPtr device) {
        RidDeviceInfo info = new() { cbSize = (uint) Marshal.SizeOf<RidDeviceInfo>() };
        IntPtr pointer = Marshal.AllocHGlobal((int) info.cbSize);
        try {
            Marshal.StructureToPtr(info, pointer, false);
            uint size = info.cbSize;
            if (GetRawInputDeviceInfoW(device, RidiDeviceInfo, pointer, ref size) != 0) {
                return Marshal.PtrToStructure<RidDeviceInfo>(pointer).u.hid.usUsagePage;
            }

            return 0;
        } catch (Exception exception) when (exception is not OutOfMemoryException) {
            return 0;
        } finally {
            Marshal.FreeHGlobal(pointer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDeviceList {
        public IntPtr hDevice;
        public uint dwType;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RidDeviceInfoMouse {
        public uint dwId;
        public uint dwNumberOfButtons;
        public uint dwSampleRate;
        public int fHasHorizontalWheel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RidDeviceInfoKeyboard {
        public uint dwType;
        public uint dwSubType;
        public uint dwKeyboardMode;
        public uint dwNumberOfFunctionKeys;
        public uint dwNumberOfIndicators;
        public uint dwNumberOfKeysTotal;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RidDeviceInfoHid {
        public uint dwVendorId;
        public uint dwProductId;
        public uint dwVersionNumber;
        public ushort usUsagePage;
        public ushort usUsage;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct RidDeviceInfoUnion {
        [FieldOffset(0)] public RidDeviceInfoMouse mouse;
        [FieldOffset(0)] public RidDeviceInfoKeyboard keyboard;
        [FieldOffset(0)] public RidDeviceInfoHid hid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RidDeviceInfo {
        public uint cbSize;
        public uint dwType;
        public RidDeviceInfoUnion u;
    }

    [DllImport("user32.dll")]
    private static extern uint GetRawInputDeviceList(IntPtr pRawInputDeviceList, ref uint puiNumDevices, uint cbSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetRawInputDeviceInfoW(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

}
