namespace AuthenticatorChooser.Fido;

public enum PinCaptureKind {
    Unchanged,
    Saved,
    Rejected
}

public readonly record struct PinCaptureOutcome(PinCaptureKind Kind, int? Length);

public static class PinPolicy {

    public const int MinLength = 4;

    public const int MaxLength = 63;

    public static int? Normalize(int? rawLength) {
        if (rawLength is null or < MinLength or > MaxLength) {
            return null;
        }

        return rawLength;
    }

    public static bool ShouldAutosubmit(int? configuredLength) => configuredLength is >= MinLength and <= MaxLength;

    public static bool ShouldSubmitTypedPin(int typedLength, int? configuredLength) =>
        ShouldAutosubmit(configuredLength) && typedLength == configuredLength;

    public static PinCaptureOutcome CaptureFromTypedSecret(string? typed) {
        if (string.IsNullOrEmpty(typed)) {
            return new PinCaptureOutcome(PinCaptureKind.Unchanged, null);
        }

        int length = typed.Length;
        if (length is < MinLength or > MaxLength) {
            return new PinCaptureOutcome(PinCaptureKind.Rejected, null);
        }

        return new PinCaptureOutcome(PinCaptureKind.Saved, length);
    }

    public static string SavedLengthSummary(int? configuredLength) => PinModePolicy.View(configuredLength).Summary;

    public static string LiveCountLabel(int typedLength) =>
        typedLength == 0 ? "0 characters typed" : $"{typedLength} characters typed";

}
