namespace AuthenticatorChooser;

public enum PromptFamily {

    Win1123H2,
    Win1125H2

}

public static class CaptionIconMapper {

    public const string WindowLogo = "WindowLogo";
    public const string WindowSecurityLogo = "WindowSecurityLogo";

    public static PromptFamily? FromAutomationId(string? captionIconAutomationId) {
        switch (captionIconAutomationId) {
            case WindowLogo:
                return PromptFamily.Win1123H2;
            case WindowSecurityLogo:
                return PromptFamily.Win1125H2;
            case null:
                return null;
            default:
                return null;
        }
    }

}
