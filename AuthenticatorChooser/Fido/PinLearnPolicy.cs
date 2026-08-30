namespace AuthenticatorChooser.Fido;

public static class PinLearnPolicy {

    public static bool IsCaptureForeground(IntPtr foreground, IntPtr dialogHwnd, int dialogPid, int foregroundPid) =>
        IsCaptureForeground(foreground, dialogHwnd, dialogPid, foregroundPid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

    public static bool IsCaptureForeground(
        IntPtr foreground,
        IntPtr dialogHwnd,
        int dialogPid,
        int foregroundPid,
        IntPtr foregroundRoot,
        IntPtr dialogRoot) =>
        IsCaptureForeground(foreground, dialogHwnd, dialogPid, foregroundPid, foregroundRoot, dialogRoot, IntPtr.Zero, IntPtr.Zero);

    public static bool IsCaptureForeground(
        IntPtr foreground,
        IntPtr dialogHwnd,
        int dialogPid,
        int foregroundPid,
        IntPtr foregroundRoot,
        IntPtr dialogRoot,
        IntPtr foregroundOwnerRoot,
        IntPtr dialogOwnerRoot) {
        if (foreground == IntPtr.Zero || dialogPid <= 0 || foregroundPid <= 0) {
            return false;
        }

        if (foreground == dialogHwnd) {
            return true;
        }

        if (dialogRoot != IntPtr.Zero && foregroundRoot == dialogRoot) {
            return true;
        }

        if (dialogOwnerRoot != IntPtr.Zero && foregroundOwnerRoot == dialogOwnerRoot) {
            return true;
        }

        return foregroundPid == dialogPid;
    }

    public static IntPtr ResolveDialogHwnd(IntPtr automationHwnd, IntPtr hostHwnd) =>
        automationHwnd != IntPtr.Zero ? automationHwnd : hostHwnd;

    public static bool LookForPinOnTitle(PinMode mode, int? autoSubmitPinLength, bool isMakingSureItsYou) {
        if (!PinFillPolicy.WantsPinDialog(mode, autoSubmitPinLength)) {
            return false;
        }

        return mode != PinMode.Cache || !isMakingSureItsYou;
    }

}
