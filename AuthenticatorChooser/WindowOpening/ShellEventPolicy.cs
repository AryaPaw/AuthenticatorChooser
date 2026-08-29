namespace AuthenticatorChooser.WindowOpening;

public static class ShellEventPolicy {

    public static bool IsWindowCreated(ShellEventArgs.ShellEvent shellEvent) =>
        shellEvent == ShellEventArgs.ShellEvent.HSHELL_WINDOWCREATED;

}
