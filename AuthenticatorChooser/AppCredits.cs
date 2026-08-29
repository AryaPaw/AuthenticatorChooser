namespace AuthenticatorChooser;

public static class AppCredits {

    public const string OriginalAuthor = "Ben Hutchison";

    public const string ForkAuthor = "AryaPaw";

    public const string OriginalRepositoryUrl = "https://github.com/Aldaviva/AuthenticatorChooser";

    public const string ForkRepositoryUrl = "https://github.com/AryaPaw/AuthenticatorChooser";

    public static string ReleasesUrl => ForkRepositoryUrl + "/releases";

    public static string ProductSubtitle =>
        "Chooses the USB security key in Windows FIDO prompts.";

    public static string CopyrightLine =>
        $"Original program © {OriginalAuthor}. This fork © {ForkAuthor}.";

    public static string Attribution =>
        $"{CopyrightLine} Based on {OriginalAuthor}'s AuthenticatorChooser. {ForkAuthor} maintains this independent fork.";

    public static string VersionLine => $"Version {AppVersion.Current}";

}
