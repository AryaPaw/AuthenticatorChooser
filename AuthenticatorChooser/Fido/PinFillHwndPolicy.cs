namespace AuthenticatorChooser.Fido;

public static class PinFillHwndPolicy {

    public static IReadOnlyList<IntPtr> SearchOrder(IntPtr hostHwnd, IntPtr fieldHwnd) {
        List<IntPtr> hwnds = [];
        Add(hwnds, fieldHwnd);
        Add(hwnds, hostHwnd);
        return hwnds;
    }

    private static void Add(List<IntPtr> hwnds, IntPtr hwnd) {
        if (hwnd == IntPtr.Zero || hwnds.Contains(hwnd)) {
            return;
        }

        hwnds.Add(hwnd);
    }

}
