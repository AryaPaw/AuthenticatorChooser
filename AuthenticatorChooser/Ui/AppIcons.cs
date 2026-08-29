using System.Drawing;
using System.Reflection;

namespace AuthenticatorChooser.Ui;

internal static class AppIcons {

    internal const string KeyResourceName = "AuthenticatorChooser.YubiKey.ico";

    public static Icon CreateKeyIcon() {
        Assembly assembly = typeof(AppIcons).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(KeyResourceName);
        if (stream is not null) {
            using Icon loaded = new(stream);
            return (Icon) loaded.Clone();
        }

        string? processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath)) {
            Icon? fromExe = Icon.ExtractAssociatedIcon(processPath);
            if (fromExe is not null) {
                return fromExe;
            }
        }

        return (Icon) SystemIcons.Shield.Clone();
    }

}
