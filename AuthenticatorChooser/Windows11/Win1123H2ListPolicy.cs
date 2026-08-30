namespace AuthenticatorChooser.Windows11;

public readonly record struct Win1123H2ListDecision(bool IsLocalWindowsHelloTpmPrompt, bool SelectChoice, bool TrySubmitNext);

public static class Win1123H2ListPolicy {

    public static Win1123H2ListDecision Decide(bool securityKeyFound, bool useAnotherDeviceFound, bool skipAllNonSecurityKeyOptions, bool shiftDown, SkipReason skipReason) {
        bool isTpm = !securityKeyFound && skipAllNonSecurityKeyOptions && useAnotherDeviceFound;
        bool haveChoice = securityKeyFound || isTpm;
        if (!haveChoice) {
            return new Win1123H2ListDecision(false, false, false);
        }

        bool blocked = skipReason is SkipReason.Paused or SkipReason.PinModeOff;
        bool selectChoice = haveChoice && !blocked && !(isTpm && shiftDown);
        bool trySubmit = haveChoice && !isTpm && skipReason == SkipReason.None;
        return new Win1123H2ListDecision(isTpm, selectChoice, trySubmit);
    }

}
