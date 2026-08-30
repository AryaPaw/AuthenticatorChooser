namespace AuthenticatorChooser;

public enum ChooserEventKind {

    Waiting,
    ChoseSecurityKey,
    Paused,
    ShiftHeld,
    ExtraOptions,
    PinModeOff,
    DesiredChoiceMissing,
    UnsupportedDialog,
    Error

}
