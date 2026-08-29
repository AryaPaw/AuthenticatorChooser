namespace AuthenticatorChooser;

public enum Win1125H2ChallengeAction {

    IgnoreMissingName,
    AutosubmitSecurityKeyPin,
    AlreadySecurityKey,
    InvokeChooseDifferentPasskey

}

public static class Win1125H2ChallengePolicy {

    public static bool IsChooseAPasskeyTitle(string actualTitle, IEnumerable<string> choosePasskeyTitles) =>
        TitlePolicy.EqualsAny(actualTitle, choosePasskeyTitles);

    public static Win1125H2ChallengeAction DecideChallenge(string? authenticatorName, IEnumerable<string> securityKeyNames, int? autoSubmitPinLength) {
        if (authenticatorName is null) {
            return Win1125H2ChallengeAction.IgnoreMissingName;
        }

        if (TitlePolicy.EqualsAny(authenticatorName, securityKeyNames)) {
            return PinPolicy.ShouldAutosubmit(autoSubmitPinLength)
                ? Win1125H2ChallengeAction.AutosubmitSecurityKeyPin
                : Win1125H2ChallengeAction.AlreadySecurityKey;
        }

        return Win1125H2ChallengeAction.InvokeChooseDifferentPasskey;
    }

}
