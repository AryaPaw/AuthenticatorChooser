using System.Runtime.InteropServices;
using System.Text;

namespace AuthenticatorChooser.Windows11;

internal interface IWindowTrust {
    bool IsTrustedFidoWindow(IntPtr hwnd, int expectedPid);
}

internal sealed class WindowTrust: IWindowTrust {

    private static readonly Guid WintrustActionGenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    private const uint WtdUiNone = 2;
    private const uint WtdRevokeNone = 0;
    private const uint WtdChoiceFile = 1;
    private const uint WtdStateActionIgnore = 0;
    private const uint WtdSaferFlag = 0x100;
    private const uint ProcessQueryLimitedInformation = 0x1000;

    public static WindowTrust Shared { get; } = new();

    public bool IsTrustedFidoWindow(IntPtr hwnd, int expectedPid) {
        if (hwnd == IntPtr.Zero || expectedPid <= 0) {
            return false;
        }

        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0 || pid != (uint) expectedPid) {
            return false;
        }

        string? path = GetProcessPath(pid);
        return path is not null && IsTrustedProcessPath(path) && IsMicrosoftSigned(path);
    }

    public static bool MatchesIdentity(IntPtr expectedHwnd, int expectedPid, IntPtr hwnd, int pid) =>
        expectedHwnd != IntPtr.Zero && expectedHwnd == hwnd && expectedPid > 0 && expectedPid == pid;

    public static bool IsTrustedProcessPath(string path) {
        string? directory = Path.GetDirectoryName(path);
        return string.Equals(directory, Environment.SystemDirectory, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetFileName(path), "CredentialUIBroker.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetProcessPath(uint pid) {
        IntPtr process = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (process == IntPtr.Zero) {
            return null;
        }

        try {
            StringBuilder fileName = new(32768);
            uint size = (uint) fileName.Capacity;
            return QueryFullProcessImageNameW(process, 0, fileName, ref size) ? fileName.ToString() : null;
        } finally {
            CloseHandle(process);
        }
    }

    private static bool IsMicrosoftSigned(string path) {
        IntPtr file = IntPtr.Zero;
        try {
            WintrustFileInfo fileInfo = new() {
                cbStruct = (uint) Marshal.SizeOf<WintrustFileInfo>(),
                pcwszFilePath = path,
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero
            };

            file = Marshal.AllocHGlobal(Marshal.SizeOf<WintrustFileInfo>());
            Marshal.StructureToPtr(fileInfo, file, false);

            WintrustData data = new() {
                cbStruct = (uint) Marshal.SizeOf<WintrustData>(),
                pPolicyCallbackData = IntPtr.Zero,
                pSipClientData = IntPtr.Zero,
                dwUIChoice = WtdUiNone,
                fdwRevocationChecks = WtdRevokeNone,
                dwUnionChoice = WtdChoiceFile,
                pFile = file,
                dwStateAction = WtdStateActionIgnore,
                hWvtStateData = IntPtr.Zero,
                pwszUrlReference = IntPtr.Zero,
                dwProvFlags = WtdSaferFlag,
                dwUIContext = 0,
                pSignatureSettings = IntPtr.Zero
            };

            Guid action = WintrustActionGenericVerifyV2;
            return WinVerifyTrust(IntPtr.Zero, ref action, ref data) == 0;
        } catch (Exception exception) when (exception is not OutOfMemoryException) {
            return false;
        } finally {
            if (file != IntPtr.Zero) {
                Marshal.FreeHGlobal(file);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WintrustFileInfo {
        public uint cbStruct;
        [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WintrustData {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSipClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWvtStateData;
        public IntPtr pwszUrlReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
    private static extern uint WinVerifyTrust(IntPtr hwnd, ref Guid pgActionID, ref WintrustData pWvtData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageNameW(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

}
