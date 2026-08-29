using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AuthenticatorChooser.Updates;

public interface ISetupInstaller {

    bool TryStartSilent(string setupPath);

}

public static class SilentSetupLauncher {

    public static string BuildCommand(string setupPath) {
        if (string.IsNullOrWhiteSpace(setupPath)
            || SilentUpdatePolicy.ContainsShellMetacharacters(setupPath)
            || !setupPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException("Setup path is not safe for a silent install command", nameof(setupPath));
        }

        string name = Path.GetFileName(setupPath);
        if (!string.Equals(name, SilentUpdatePolicy.SetupFileName("win-x64"), StringComparison.OrdinalIgnoreCase)
            && !string.Equals(name, SilentUpdatePolicy.SetupFileName("win-arm64"), StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException("Setup path is not safe for a silent install command", nameof(setupPath));
        }

        return "/C ping 127.0.0.1 -n 5 >NUL & \"" + Path.GetFullPath(setupPath)
            + "\" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /FORCECLOSEAPPLICATIONS";
    }

}

[ExcludeFromCodeCoverage]
public sealed class CmdSilentSetupInstaller: ISetupInstaller {

    public bool TryStartSilent(string setupPath) {
        ProcessStartInfo start = new() {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            Arguments = BuildCommand(setupPath),
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.SystemDirectory
        };
        using Process? process = Process.Start(start);
        return process is not null;
    }

    private static string BuildCommand(string setupPath) => SilentSetupLauncher.BuildCommand(setupPath);

}
