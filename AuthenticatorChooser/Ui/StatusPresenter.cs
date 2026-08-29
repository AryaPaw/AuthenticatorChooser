namespace AuthenticatorChooser;

public static class StatusPresenter {

    public static string StatusLabel(bool enabled) => enabled ? "Running" : "Paused";

    public static string PauseActionLabel(bool enabled) => enabled ? "Pause" : "Resume";

    public static string EventLabel(ChooserEventKind kind, string detail) {
        switch (kind) {
            case ChooserEventKind.Waiting:
            case ChooserEventKind.ChoseSecurityKey:
            case ChooserEventKind.Paused:
            case ChooserEventKind.ShiftHeld:
            case ChooserEventKind.ExtraOptions:
            case ChooserEventKind.DesiredChoiceMissing:
            case ChooserEventKind.UnsupportedDialog:
            case ChooserEventKind.Error:
                return detail;
            default:
                return Unreachable(kind);
        }
    }

    public static void ToggleEnabled(AppState state) => state.ToggleEnabled();

    private static string Unreachable(ChooserEventKind kind) {
        throw new InvalidOperationException($"Unhandled chooser event {kind}");
    }

}
