using Microsoft.Win32;

namespace AuthenticatorChooser.Autostart;

public static class UninstallCleanup {

    public static bool Execute(IAutostartService autostart, string dataDirectory, Action? deleteCurrentUserRunValue = null) {
        string fullRoot = Path.GetFullPath(dataDirectory);
        if (!string.Equals(Path.GetFileName(fullRoot), nameof(AuthenticatorChooser), StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        autostart.Unregister();
        (deleteCurrentUserRunValue ?? DeleteCurrentUserRunValue).Invoke();
        if (Directory.Exists(fullRoot)) {
            Directory.Delete(fullRoot, true);
        }

        return true;
    }

    public static void DeleteCurrentUserRunValue() {
        using RegistryKey? userRun = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (userRun is null) {
            return;
        }

        try {
            userRun.DeleteValue(nameof(AuthenticatorChooser), true);
        } catch (ArgumentException) {
            // value already absent
        }
    }

}
