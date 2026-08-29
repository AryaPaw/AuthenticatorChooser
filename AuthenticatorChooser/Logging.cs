using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;

namespace AuthenticatorChooser;

internal static class Logging {

    private static readonly SimpleLayout MESSAGE_FORMAT = new(
        " ${level:format=FirstCharacter:lowercase=true} | ${date:format=yyyy-MM-dd HH\\:mm\\:ss.fff} | ${logger:shortName=true:padding=-25} | ${message:withException=true:exceptionSeparator=\n}");

    private static readonly LogLevel LOG_LEVEL = LogLevel.Debug;

    public static readonly HashSet<string> ReservedLogFileNames = new(StringComparer.OrdinalIgnoreCase) {
        ".", "..", "settings.json", "con", "prn", "aux", "nul",
        "com1", "com2", "com3", "com4", "lpt1", "lpt2", "lpt3"
    };

    public static string? NormalizeLogFileName(string? logFilename) {
        if (string.IsNullOrWhiteSpace(logFilename)) {
            return null;
        }

        string expanded = Environment.ExpandEnvironmentVariables(logFilename.Trim());
        string fileName = Path.GetFileName(expanded);
        if (!string.Equals(fileName, expanded, StringComparison.OrdinalIgnoreCase) || fileName.Length == 0) {
            throw new InvalidOperationException("Log path must be a file name under AppData, not an absolute or nested path");
        }

        if (fileName.Contains('$', StringComparison.Ordinal) || fileName.Contains('{', StringComparison.Ordinal) || fileName.Contains('}', StringComparison.Ordinal)) {
            throw new InvalidOperationException("Log file name must not contain NLog layout tokens");
        }

        if (ReservedLogFileNames.Contains(fileName)) {
            throw new InvalidOperationException("Log file name is reserved");
        }

        return fileName;
    }

    public static string? TryNormalizeLogFileName(string? logFilename) {
        try {
            return NormalizeLogFileName(logFilename);
        } catch (InvalidOperationException) {
            return null;
        }
    }

    public static string ResolveLogPath(string? logFilename, string appDataRoot) {
        string root = Path.GetFullPath(appDataRoot);
        string resolved = string.IsNullOrWhiteSpace(logFilename)
            ? Path.GetFullPath(Path.Combine(root, Path.ChangeExtension(nameof(AuthenticatorChooser), ".log")))
            : Path.GetFullPath(Path.Combine(root, NormalizeLogFileName(logFilename)!));
        SettingsStore.EnsurePathAllowed(resolved, root);
        return resolved;
    }

    public static LoggingConfiguration CreateConfiguration(bool enableFileAppender, string logFilename) {
        LoggingConfiguration logConfig = new();

        if (enableFileAppender) {
            logConfig.AddRule(LOG_LEVEL, LogLevel.Fatal, new FileTarget("fileAppender") {
                Layout   = MESSAGE_FORMAT,
                FileName = logFilename
            });
        }

        logConfig.AddRule(LOG_LEVEL, LogLevel.Fatal, new ConsoleTarget("consoleAppender") {
            Layout                 = MESSAGE_FORMAT,
            DetectConsoleAvailable = true
        });

        return logConfig;
    }

    public static void initialize(bool enableFileAppender, string? logFilename) {
        LogManager.Configuration = CreateConfiguration(enableFileAppender, ResolveLogPath(logFilename, SettingsStore.DefaultDirectory));
    }

}
