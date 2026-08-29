namespace AuthenticatorChooser.Fido;

public static class SkipPolicy {

    public static SkipReason Decide(bool enabled, bool shiftDown, bool skipAllNonSecurityKeyOptions, bool onlySecurityKeyAndNewPhone) {
        if (!enabled) {
            return SkipReason.Paused;
        }

        if (shiftDown) {
            return SkipReason.ShiftHeld;
        }

        if (!skipAllNonSecurityKeyOptions && !onlySecurityKeyAndNewPhone) {
            return SkipReason.ExtraOptions;
        }

        return SkipReason.None;
    }

    public static ChooserEventKind ToEvent(SkipReason reason) => reason switch {
        SkipReason.None => ChooserEventKind.ChoseSecurityKey,
        SkipReason.Paused => ChooserEventKind.Paused,
        SkipReason.ShiftHeld => ChooserEventKind.ShiftHeld,
        SkipReason.ExtraOptions => ChooserEventKind.ExtraOptions,
        _ => throw new InvalidOperationException($"Unhandled skip reason {reason}")
    };

}
