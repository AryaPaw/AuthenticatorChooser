namespace AuthenticatorChooser;

public sealed class AppState {

    private readonly object gate = new();
    private bool enabled = true;
    private bool skipAllNonSecurityKeyOptions;
    private int? autoSubmitPinLength;
    private PinMode pinMode = PinMode.Off;
    private PinCacheLifetime pinCacheLifetime = PinCacheLifetime.TwoMinutes;
    private List<AuthenticatorPriorityRule> priorityRules = AuthenticatorPriorityCatalog.CreateDefaults().Select(rule => rule.Clone()).ToList();
    private bool fileLogEnabled;
    private string? logFilename;
    private bool autostartOnLogon = true;
    private bool trayHintShown;
    private bool autoUpdateEnabled = true;
    private DateTime? lastUpdateCheckUtc;
    private int schemaVersion = AppSettings.CurrentSchema;
    private ChooserEventKind lastEvent = ChooserEventKind.Waiting;
    private string lastEventDetail = "Waiting for Windows Security FIDO dialog boxes";

    public event EventHandler? Changed;

    public bool Enabled {
        get {
            lock (gate) {
                return enabled;
            }
        }
        set => Set(ref enabled, value);
    }

    public bool SkipAllNonSecurityKeyOptions {
        get {
            lock (gate) {
                return skipAllNonSecurityKeyOptions;
            }
        }
        set => Set(ref skipAllNonSecurityKeyOptions, value);
    }

    public int? AutoSubmitPinLength {
        get {
            lock (gate) {
                return autoSubmitPinLength;
            }
        }
        set => Set(ref autoSubmitPinLength, PinPolicy.Normalize(value));
    }

    public PinMode PinMode {
        get {
            lock (gate) {
                return pinMode;
            }
        }
        set => Set(ref pinMode, value);
    }

    public PinCacheLifetime PinCacheLifetime {
        get {
            lock (gate) {
                return pinCacheLifetime;
            }
        }
        set => Set(ref pinCacheLifetime, value);
    }

    public IReadOnlyList<AuthenticatorPriorityRule> PriorityRules {
        get {
            lock (gate) {
                return AuthenticatorPriorityCatalog.Clone(priorityRules);
            }
        }
        set {
            List<AuthenticatorPriorityRule> next = AuthenticatorPriorityCatalog.EnsureBuiltIns(value);
            lock (gate) {
                priorityRules = next;
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool FileLogEnabled {
        get {
            lock (gate) {
                return fileLogEnabled;
            }
        }
        set => Set(ref fileLogEnabled, value);
    }

    public string? LogFilename {
        get {
            lock (gate) {
                return logFilename;
            }
        }
        set => Set(ref logFilename, Logging.TryNormalizeLogFileName(value));
    }

    public bool AutostartOnLogon {
        get {
            lock (gate) {
                return autostartOnLogon;
            }
        }
        set => Set(ref autostartOnLogon, value);
    }

    public bool TrayHintShown {
        get {
            lock (gate) {
                return trayHintShown;
            }
        }
        set => Set(ref trayHintShown, value);
    }

    public bool AutoUpdateEnabled {
        get {
            lock (gate) {
                return autoUpdateEnabled;
            }
        }
        set => Set(ref autoUpdateEnabled, value);
    }

    public DateTime? LastUpdateCheckUtc {
        get {
            lock (gate) {
                return lastUpdateCheckUtc;
            }
        }
        set => Set(ref lastUpdateCheckUtc, value);
    }

    public ChooserEventKind LastEvent {
        get {
            lock (gate) {
                return lastEvent;
            }
        }
    }

    public string LastEventDetail {
        get {
            lock (gate) {
                return lastEventDetail;
            }
        }
    }

    public static AppState FromSettings(AppSettings settings) {
        AppState state = new();
        state.ApplySettings(settings);
        return state;
    }

    public void ApplySettings(AppSettings settings) {
        lock (gate) {
            enabled = settings.Enabled;
            skipAllNonSecurityKeyOptions = settings.SkipAllNonSecurityKeyOptions;
            autoSubmitPinLength = PinPolicy.Normalize(settings.AutoSubmitPinLength == 0 ? null : settings.AutoSubmitPinLength);
            pinMode = settings.PinMode;
            pinCacheLifetime = settings.PinCacheLifetime;
            priorityRules = AuthenticatorPriorityCatalog.EnsureBuiltIns(settings.PriorityRules);
            fileLogEnabled = settings.FileLogEnabled;
            logFilename = Logging.TryNormalizeLogFileName(settings.LogFilename);
            autostartOnLogon = settings.AutostartOnLogon;
            trayHintShown = settings.TrayHintShown;
            autoUpdateEnabled = settings.AutoUpdateEnabled;
            lastUpdateCheckUtc = settings.LastUpdateCheckUtc;
            schemaVersion = settings.SchemaVersion;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public AppSettings ToSettings() {
        lock (gate) {
            return new AppSettings {
                SchemaVersion = schemaVersion,
                Enabled = enabled,
                SkipAllNonSecurityKeyOptions = skipAllNonSecurityKeyOptions,
                AutoSubmitPinLength = autoSubmitPinLength ?? 0,
                PinMode = pinMode,
                PinCacheLifetime = pinCacheLifetime,
                PriorityRules = AuthenticatorPriorityCatalog.Clone(priorityRules),
                FileLogEnabled = fileLogEnabled,
                LogFilename = logFilename,
                AutostartOnLogon = autostartOnLogon,
                TrayHintShown = trayHintShown,
                AutoUpdateEnabled = autoUpdateEnabled,
                LastUpdateCheckUtc = lastUpdateCheckUtc
            };
        }
    }

    public void ToggleEnabled() {
        lock (gate) {
            enabled = !enabled;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Report(ChooserEventKind kind, string detail) {
        lock (gate) {
            lastEvent = kind;
            lastEventDetail = detail;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Set<T>(ref T field, T value) {
        lock (gate) {
            if (EqualityComparer<T>.Default.Equals(field, value)) {
                return;
            }

            field = value;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

}
