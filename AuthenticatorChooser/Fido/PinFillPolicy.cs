namespace AuthenticatorChooser.Fido;

public enum PinFillDecision {
    DoNothing,
    WatchLength,
    FillCache,
    RefuseAndClear,
    ManualFallback
}

public static class PinFillPolicy {

    public static PinFillDecision Decide(
        PinMode mode,
        bool hasCachedPin,
        bool trustedWindow,
        int? ctapHidCount,
        bool alreadyFilledThisWindow,
        bool debuggerAttached) {
        switch (mode) {
            case PinMode.Off:
                return PinFillDecision.DoNothing;
            case PinMode.Length:
                return PinFillDecision.WatchLength;
            case PinMode.Cache:
                if (debuggerAttached || alreadyFilledThisWindow) {
                    return PinFillDecision.RefuseAndClear;
                }

                if (!trustedWindow || ctapHidCount != 1 || !hasCachedPin) {
                    return PinFillDecision.ManualFallback;
                }

                return PinFillDecision.FillCache;
            default:
                throw new InvalidOperationException($"Unhandled PIN mode {mode}");
        }
    }

    public static bool WantsPinDialog(PinMode mode, int? autoSubmitPinLength) => mode switch {
        PinMode.Off => false,
        PinMode.Length => PinPolicy.ShouldAutosubmit(autoSubmitPinLength),
        PinMode.Cache => true,
        _ => throw new InvalidOperationException($"Unhandled PIN mode {mode}")
    };

}
