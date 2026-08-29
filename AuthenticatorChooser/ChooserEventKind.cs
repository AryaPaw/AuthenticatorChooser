namespace AuthenticatorChooser;

public enum ChooserEventKind {

    Waiting,
    ChoseSecurityKey,
    Paused,
    ShiftHeld,
    ExtraOptions,
    DesiredChoiceMissing,
    UnsupportedDialog,
    Error

}
