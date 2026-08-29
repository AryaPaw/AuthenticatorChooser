namespace AuthenticatorChooser.Fido;

public enum PinToggleKind {
    TurnOn,
    TurnOff,
    RejectedNeedLength,
    RejectedEmpty
}

public readonly record struct PinModeView(string ButtonText, string Summary, string Hint, bool FieldEnabled);

public readonly record struct PinToggleDecision(PinToggleKind Kind, int? LengthAfter, PinModeView View);

public static class PinModePolicy {

    public static bool IsArmed(int? savedLength) => PinPolicy.ShouldAutosubmit(savedLength);

    public static PinModeView View(int? savedLength, string? liveMessage = null) {
        if (IsArmed(savedLength)) {
            return new PinModeView(
                "Turn off",
                $"On — OK will be pressed after {savedLength} characters on the USB-key PIN dialog. The PIN is not stored.",
                liveMessage ?? "Press Turn off to stop autosubmit.",
                false);
        }

        return new PinModeView(
            "Turn on",
            "Off — type your PIN, then press Turn on. Only the character count is kept.",
            liveMessage ?? PinPolicy.LiveCountLabel(0),
            true);
    }

    public static PinToggleDecision Press(int? savedLength, string? typedSecret) {
        if (IsArmed(savedLength)) {
            return new PinToggleDecision(PinToggleKind.TurnOff, null, View(null));
        }

        PinCaptureOutcome capture = PinPolicy.CaptureFromTypedSecret(typedSecret);
        switch (capture.Kind) {
            case PinCaptureKind.Saved:
                return new PinToggleDecision(PinToggleKind.TurnOn, capture.Length, View(capture.Length));
            case PinCaptureKind.Rejected:
                return new PinToggleDecision(
                    PinToggleKind.RejectedNeedLength,
                    savedLength,
                    View(savedLength, $"Need {PinPolicy.MinLength}–{PinPolicy.MaxLength} characters. Nothing was saved."));
            case PinCaptureKind.Unchanged:
                return new PinToggleDecision(
                    PinToggleKind.RejectedEmpty,
                    savedLength,
                    View(savedLength, "Type your PIN first, then press Turn on."));
            default:
                return Unreachable(capture.Kind);
        }
    }

    private static PinToggleDecision Unreachable(PinCaptureKind kind) =>
        throw new InvalidOperationException($"Unhandled pin capture {kind}");

}
