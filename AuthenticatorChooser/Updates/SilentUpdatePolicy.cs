using System.Runtime.InteropServices;

namespace AuthenticatorChooser.Updates;

internal static class SilentUpdatePolicy {

    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    public static readonly TimeSpan BusyRetry = TimeSpan.FromMinutes(2);

    public static readonly TimeSpan FailedRetry = TimeSpan.FromHours(6);

    public const long MaxApiBytes = 1_048_576;

    public const long MaxSetupBytes = 80L * 1024 * 1024;

    public const long MaxSidecarBytes = 4096;

    public static bool TryParseTag(string? tag, out Version? version) {
        version = null;
        if (string.IsNullOrWhiteSpace(tag)) {
            return false;
        }

        string trimmed = tag.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V')) {
            trimmed = trimmed[1..];
        }

        return Version.TryParse(trimmed, out version);
    }

    public static bool IsNewer(Version current, Version candidate) => candidate > current;

    public static bool AllowsBackgroundProcess(string processName) => TrayIcon.AllowsDesktopNotifications(processName);

    public static bool IsSafeToRestart() => !FidoActivity.IsInProgress;

    public static string? RidFor(Architecture architecture) {
        if (architecture == Architecture.X64) {
            return "win-x64";
        }

        if (architecture == Architecture.Arm64) {
            return "win-arm64";
        }

        return null;
    }

    public static string SetupFileName(string rid) => $"AuthenticatorChooser-Setup-{rid}.exe";

    public static bool ShouldPoll(DateTime? lastCheckUtc, DateTime nowUtc, TimeSpan minInterval) {
        if (lastCheckUtc is null) {
            return true;
        }

        return nowUtc - lastCheckUtc.Value >= minInterval;
    }

    public static bool HasInnoUninstaller(string applicationDirectory) =>
        File.Exists(Path.Combine(applicationDirectory, "unins000.exe"));

    public static bool IsSafeSetupPath(string path, string downloadDirectory) {
        string fullPath = Path.GetFullPath(path);
        string fullRoot = Path.GetFullPath(downloadDirectory);
        bool underRoot = fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        if (!underRoot) {
            return false;
        }

        string name = Path.GetFileName(fullPath);
        return string.Equals(name, SetupFileName("win-x64"), StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, SetupFileName("win-arm64"), StringComparison.OrdinalIgnoreCase);
    }

    public static bool ContainsShellMetacharacters(string path) =>
        path.IndexOfAny(['&', '|', '^', '%', '"', '<', '>']) >= 0;

}
