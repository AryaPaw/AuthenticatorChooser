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

    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr SysAllocString(IntPtr psz);

    [DllImport("oleaut32.dll")]
    internal static extern void SysFreeString(IntPtr bstr);

    public static bool DebuggerAttached() =>
        IsDebuggerPresent() || (CheckRemoteDebuggerPresent(GetCurrentProcess(), out bool remote) && remote);

}
