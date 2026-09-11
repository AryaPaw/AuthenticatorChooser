namespace AuthenticatorChooser.Updates;

internal static class ManualUpdateCopy {

    public const string Checking = "Checking...";

    public const string AlreadyRunning = "A check is already running.";

    public static string For(SilentUpdateOutcome outcome) {
        switch (outcome) {
            case SilentUpdateOutcome.NoUpdate:
                return "You already have the latest version.";
            case SilentUpdateOutcome.Applied:
                return "Update downloaded. Setup will install it now.";
            case SilentUpdateOutcome.Failed:
                return "Could not check or download the update.";
            case SilentUpdateOutcome.Offline:
                return "Could not reach GitHub.";
            case SilentUpdateOutcome.Skipped:
                return "Updates apply only to the Setup-installed copy.";
            case SilentUpdateOutcome.Busy:
                return "Cannot update during a FIDO prompt. Try again in a minute.";
            default:
                SilentUpdateOutcome unreachable = outcome;
                throw new InvalidOperationException($"Unhandled silent update outcome {unreachable}");
        }
    }

}
