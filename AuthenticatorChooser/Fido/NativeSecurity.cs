using System.Runtime.InteropServices;

namespace AuthenticatorChooser.Fido;

internal static class NativeSecurity {

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsDebuggerPresent();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, [MarshalAs(UnmanagedType.Bool)] out bool isDebuggerPresent);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    internal static extern void RtlZeroMemory(IntPtr destination, int length);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    internal static IntPtr GetAncestorRoot(IntPtr hWnd) {
        if (hWnd == IntPtr.Zero) {
            return IntPtr.Zero;
        }

        IntPtr root = GetAncestor(hWnd, 2);
        return root == IntPtr.Zero ? hWnd : root;
    }

    internal static IntPtr GetAncestorOwnerRoot(IntPtr hWnd) {
        if (hWnd == IntPtr.Zero) {
            return IntPtr.Zero;
        }

        IntPtr root = GetAncestor(hWnd, 3);
        return root == IntPtr.Zero ? hWnd : root;
    }

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr SysAllocString(IntPtr psz);

    [DllImport("oleaut32.dll")]
    internal static extern void SysFreeString(IntPtr bstr);

    public static bool DebuggerAttached() =>
        IsDebuggerPresent() || (CheckRemoteDebuggerPresent(GetCurrentProcess(), out bool remote) && remote);

}
