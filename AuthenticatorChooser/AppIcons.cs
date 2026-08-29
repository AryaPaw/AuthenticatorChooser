using System.Drawing;
using System.Reflection;

namespace AuthenticatorChooser;

internal static class AppIcons {

    internal const string KeyResourceName = "AuthenticatorChooser.YubiKey.ico";

    public static Icon CreateKeyIcon() {
        string? processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath)) {
            Icon? fromExe = Icon.ExtractAssociatedIcon(processPath);
            if (fromExe is not null) {
                return fromExe;
            }
        }

        Assembly assembly = typeof(AppIcons).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(KeyResourceName);
        if (stream is not null) {
            using Icon loaded = new(stream);
            return (Icon) loaded.Clone();
        }

        return (Icon) SystemIcons.Shield.Clone();
    }

}
