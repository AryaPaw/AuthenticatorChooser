namespace AuthenticatorChooser.Autostart;

internal static class AutostartPolicy {

    public static bool CanOwnLogonTask(string executablePath) {
        if (string.IsNullOrWhiteSpace(executablePath)) {
            return false;
        }

        string? directory = Path.GetDirectoryName(Path.GetFullPath(executablePath));
        return !string.IsNullOrWhiteSpace(directory) && SilentUpdatePolicy.HasInnoUninstaller(directory);
    }

}
