namespace AuthenticatorChooser.Settings;

public sealed class AppSettings {

    public const int CurrentSchema = 3;

    public int SchemaVersion { get; set; } = CurrentSchema;

    public bool Enabled { get; set; } = true;

    public bool SkipAllNonSecurityKeyOptions { get; set; }

    public int AutoSubmitPinLength { get; set; }

    public int LearnedPinLength { get; set; }

    public PinMode PinMode { get; set; } = PinMode.Off;

    public PinCacheLifetime PinCacheLifetime { get; set; } = PinCacheLifetime.TwoMinutes;

    public List<AuthenticatorPriorityRule> PriorityRules { get; set; } = AuthenticatorPriorityCatalog.CreateDefaults().Select(rule => rule.Clone()).ToList();

    public bool FileLogEnabled { get; set; }

    public string? LogFilename { get; set; }

    public bool AutostartOnLogon { get; set; } = true;

    public bool TrayHintShown { get; set; }

    public bool AutoUpdateEnabled { get; set; } = true;

    public DateTime? LastUpdateCheckUtc { get; set; }

}
