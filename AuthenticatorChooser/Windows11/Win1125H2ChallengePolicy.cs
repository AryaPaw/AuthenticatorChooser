namespace AuthenticatorChooser.Windows11;

public enum Win1125H2ChallengeAction {

    IgnoreMissingName,
    AutosubmitSecurityKeyPin,
    AlreadySecurityKey,
    InvokeChooseDifferentPasskey,
    LeaveAlone

}

public static class Win1125H2ChallengePolicy {

    public static bool IsChooseAPasskeyTitle(string actualTitle, IEnumerable<string> choosePasskeyTitles) =>
        TitlePolicy.EqualsAny(actualTitle, choosePasskeyTitles);

    public static Win1125H2ChallengeAction DecideChallenge(
        string? authenticatorName,
        IEnumerable<string> securityKeyNames,
        PinMode pinMode,
        int? autoSubmitPinLength,
        bool skipNonSecurityKey) {
        if (authenticatorName is null) {
            return Win1125H2ChallengeAction.IgnoreMissingName;
        }

        if (!PinFillPolicy.AllowsChoiceAutosubmit(pinMode)) {
            return TitlePolicy.EqualsAny(authenticatorName, securityKeyNames)
                ? Win1125H2ChallengeAction.AlreadySecurityKey
                : Win1125H2ChallengeAction.LeaveAlone;
        }

        if (TitlePolicy.EqualsAny(authenticatorName, securityKeyNames)) {
            return PinFillPolicy.WantsPinDialog(pinMode, autoSubmitPinLength)
                ? Win1125H2ChallengeAction.AutosubmitSecurityKeyPin
                : Win1125H2ChallengeAction.AlreadySecurityKey;
        }

        return skipNonSecurityKey
            ? Win1125H2ChallengeAction.InvokeChooseDifferentPasskey
            : Win1125H2ChallengeAction.LeaveAlone;
    }

}
