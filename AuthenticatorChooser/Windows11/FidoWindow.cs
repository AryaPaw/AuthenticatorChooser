namespace AuthenticatorChooser.Windows11;

public static class FidoWindow {

    public const string ClassName = "Credential Dialog Xaml Host";
    public const string AltTabClassName = "XamlExplorerHostIslandWindow";

    public static bool IsFidoPromptClass(string? className) => className == ClassName;

    public static bool IsAltTabHeld(string? foregroundClassName) => foregroundClassName == AltTabClassName;

}
