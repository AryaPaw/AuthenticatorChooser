namespace AuthenticatorChooser.Fido;

public static class PinCacheUxPolicy {

    public static int? RememberedLength(int pinLength) => PinPolicy.Normalize(pinLength);

    public static int? SubmitLength(PinMode mode, int? lengthModeLength, int? learnedLength) =>
        mode == PinMode.Cache ? learnedLength : lengthModeLength;

    public static bool AutosubmitFirstTypedPin(int? learnedLength) => PinPolicy.ShouldAutosubmit(learnedLength);

    public static string WaitingStatus(int? learnedLength) =>
        AutosubmitFirstTypedPin(learnedLength)
            ? $"PIN not cached — type {learnedLength} characters in the next Windows prompt; OK is pressed automatically"
            : "PIN not cached — type your PIN in the next Windows prompt and press Enter once";

}
