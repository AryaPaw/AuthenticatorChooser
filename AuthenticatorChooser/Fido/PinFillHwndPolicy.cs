namespace AuthenticatorChooser.Fido;

public static class PinFillHwndPolicy {

    public static IReadOnlyList<IntPtr> SearchOrder(IntPtr hostHwnd, IntPtr fieldHwnd, params IntPtr[] extras) {
        List<IntPtr> hwnds = [];
        Add(hwnds, fieldHwnd);
        foreach (IntPtr extra in extras) {
            Add(hwnds, extra);
        }

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
