namespace AuthenticatorChooser.Windows11;

public static class TitlePolicy {

    public static bool EqualsAny(string? actualTitle, IEnumerable<string> expectedTitles) {
        if (actualTitle is null) {
            return false;
        }

        return expectedTitles.Any(expected => expected.Equals(actualTitle, StringComparison.CurrentCulture));
    }

    public static bool CanHandleWin1123H2(string? actualTitle, bool includeMakingSureItsYou, IEnumerable<string> signInTitles, IEnumerable<string> makingSureTitles) {
        IEnumerable<string> expected = includeMakingSureItsYou ? signInTitles.Concat(makingSureTitles) : signInTitles;
        return EqualsAny(actualTitle, expected);
    }

    public static bool CanHandleWin1125H2(string? actualTitle, bool includeSignInWithPasskey, IEnumerable<string> choosePasskeyTitles, IEnumerable<string> signInWithPasskeyTitles) {
        IEnumerable<string> expected = includeSignInWithPasskey ? choosePasskeyTitles.Concat(signInWithPasskeyTitles) : choosePasskeyTitles;
        return EqualsAny(actualTitle, expected);
    }

    public static bool IncludeAggressiveTitles(bool skipAllNonSecurityKeyOptions, int? autoSubmitPinLength) =>
        skipAllNonSecurityKeyOptions || PinPolicy.ShouldAutosubmit(autoSubmitPinLength);

}
