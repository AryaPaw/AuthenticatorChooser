using System.Text.Json;

namespace AuthenticatorChooser.Settings;

public static class SettingsStore {

    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), nameof(AuthenticatorChooser));

    public static string DefaultPath => Path.Combine(DefaultDirectory, "settings.json");

    public static void EnsurePathAllowed(string path, string allowedRoot) {
        string fullPath = Path.GetFullPath(path);
        string fullRoot = Path.GetFullPath(allowedRoot);
        bool underRoot = fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase);
        if (!underRoot) {
            throw new InvalidOperationException("Settings path must stay under the application data directory");
        }
    }

    public static AppSettings Load(string path) {
        if (!File.Exists(path)) {
            return new AppSettings();
        }

        string json = File.ReadAllText(path);
        return Migrate(JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings());
    }

    public static AppSettings Migrate(AppSettings settings) {
        if (settings.SchemaVersion >= AppSettings.CurrentSchema) {
            return settings;
        }

        return new AppSettings {
            SchemaVersion = AppSettings.CurrentSchema,
            Enabled = settings.Enabled,
            SkipAllNonSecurityKeyOptions = settings.SkipAllNonSecurityKeyOptions,
            AutoSubmitPinLength = settings.AutoSubmitPinLength,
            FileLogEnabled = settings.FileLogEnabled,
            LogFilename = settings.LogFilename,
            AutostartOnLogon = true,
            TrayHintShown = settings.TrayHintShown,
            AutoUpdateEnabled = true,
            LastUpdateCheckUtc = settings.LastUpdateCheckUtc
        };
    }

    public static void Save(string path, AppSettings settings) {
        string directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? throw new InvalidOperationException("Settings path has no directory");
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public static AppSettings MergeCli(AppSettings stored, CliOverrides cli) {
        AppSettings merged = new() {
            SchemaVersion = stored.SchemaVersion,
            Enabled = stored.Enabled,
            SkipAllNonSecurityKeyOptions = stored.SkipAllNonSecurityKeyOptions,
            AutoSubmitPinLength = stored.AutoSubmitPinLength,
            FileLogEnabled = stored.FileLogEnabled,
            LogFilename = stored.LogFilename,
            AutostartOnLogon = stored.AutostartOnLogon,
            TrayHintShown = stored.TrayHintShown,
            AutoUpdateEnabled = stored.AutoUpdateEnabled,
            LastUpdateCheckUtc = stored.LastUpdateCheckUtc
        };

        if (cli.SkipAllNonSecurityKeyOptions) {
            merged.SkipAllNonSecurityKeyOptions = true;
        }

        if (cli.AutoSubmitPinLength is { } pinLength) {
            merged.AutoSubmitPinLength = PinPolicy.Normalize(pinLength) ?? 0;
        }

        if (cli.FileLogEnabled) {
            merged.FileLogEnabled = true;
            if (cli.LogFilename is not null) {
                merged.LogFilename = cli.LogFilename;
            }
        }

        if (cli.AutostartOnLogon) {
            merged.AutostartOnLogon = true;
        }

        return merged;
    }

}

public readonly record struct CliOverrides(
    bool SkipAllNonSecurityKeyOptions,
    int? AutoSubmitPinLength,
    bool FileLogEnabled,
    string? LogFilename,
    bool AutostartOnLogon);
