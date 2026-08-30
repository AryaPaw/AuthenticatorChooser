using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuthenticatorChooser.Settings;

public static class SettingsStore {

    private static readonly object SaveLock = new();

    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
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

    public static AppSettings Clone(AppSettings settings) => new() {
        SchemaVersion = settings.SchemaVersion,
        Enabled = settings.Enabled,
        SkipAllNonSecurityKeyOptions = settings.SkipAllNonSecurityKeyOptions,
        AutoSubmitPinLength = settings.AutoSubmitPinLength,
        PinMode = settings.PinMode,
        PinCacheLifetime = settings.PinCacheLifetime,
        PriorityRules = AuthenticatorPriorityCatalog.Clone(settings.PriorityRules),
        FileLogEnabled = settings.FileLogEnabled,
        LogFilename = settings.LogFilename,
        AutostartOnLogon = settings.AutostartOnLogon,
        TrayHintShown = settings.TrayHintShown,
        AutoUpdateEnabled = settings.AutoUpdateEnabled,
        LastUpdateCheckUtc = settings.LastUpdateCheckUtc
    };

    public static AppSettings Migrate(AppSettings settings) {
        AppSettings next = Clone(settings);
        if (settings.SchemaVersion < 2) {
            next.AutostartOnLogon = true;
            next.AutoUpdateEnabled = true;
        }

        if (settings.SchemaVersion < 3) {
            next.PinMode = settings.AutoSubmitPinLength > 0 ? PinMode.Length : PinMode.Off;
            next.PinCacheLifetime = PinCacheLifetime.TwoMinutes;
            next.PriorityRules = AuthenticatorPriorityCatalog.CreateDefaults().Select(rule => rule.Clone()).ToList();
            if (settings.SkipAllNonSecurityKeyOptions) {
                next.PriorityRules = AuthenticatorPriorityCatalog.ApplySkipAll(next.PriorityRules);
            }
        }

        next.PriorityRules = AuthenticatorPriorityCatalog.EnsureBuiltIns(next.PriorityRules);
        next.SchemaVersion = AppSettings.CurrentSchema;
        return next;
    }

    public static void Save(string path, AppSettings settings) {
        lock (SaveLock) {
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Settings path has no directory");
            Directory.CreateDirectory(directory);
            string temporary = fullPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temporary, fullPath, overwrite: true);
        }
    }

    public static AppSettings MergeCli(AppSettings stored, CliOverrides cli) {
        AppSettings merged = Clone(stored);

        if (cli.SkipAllNonSecurityKeyOptions) {
            merged.SkipAllNonSecurityKeyOptions = true;
            merged.PriorityRules = AuthenticatorPriorityCatalog.ApplySkipAll(merged.PriorityRules);
        }

        if (cli.AutoSubmitPinLength is { } pinLength) {
            merged.AutoSubmitPinLength = PinPolicy.Normalize(pinLength) ?? 0;
            if (merged.AutoSubmitPinLength > 0) {
                merged.PinMode = PinMode.Length;
            }
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
