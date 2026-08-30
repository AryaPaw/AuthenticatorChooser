using System.Diagnostics.CodeAnalysis;
using AuthenticatorChooser.WindowOpening;
using AuthenticatorChooser.Windows11;
using ManagedWinapi.Windows;
using Microsoft.Win32;
using NLog;
using System.Windows.Forms;

namespace AuthenticatorChooser;

public interface IUserNotifier {

    void Info(string title, string message);

    void Error(string title, string message);

}

[ExcludeFromCodeCoverage]
public sealed class WinFormsUserNotifier: IUserNotifier {

    public void Info(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public void Error(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

}

public interface IUiLoop {

    void Run(StatusForm form);

}

[ExcludeFromCodeCoverage]
public sealed class WinFormsUiLoop: IUiLoop {

    public void Run(StatusForm form) => Application.Run(form);

}

public sealed class LaunchRequest {

    public bool Help { get; init; }

    public bool AutostartOnLogon { get; init; }

    public bool UninstallCleanup { get; init; }

    public bool ShowWindow { get; init; }

    public CliOverrides Cli { get; init; }

}

public sealed class LaunchPreparation {

    public int ExitCode { get; init; }

    public AppState? State { get; init; }

    public string? MessageTitle { get; init; }

    public string? Message { get; init; }

    public bool IsError { get; init; }

}

public sealed class AppSession: IDisposable {

    public const string ProgramName = nameof(AuthenticatorChooser);

    private readonly IUserNotifier notifier;
    private readonly IAutostartService autostart;
    private readonly ISingleInstanceService singleInstance;
    private readonly IUiLoop uiLoop;
    private readonly Func<string> settingsPath;
    private readonly Func<string> allowedRoot;
    private readonly Func<string> processPath;
    private readonly Action<bool, string?> initializeLogging;

    public AppSession(
        IUserNotifier notifier,
        IAutostartService autostart,
        ISingleInstanceService singleInstance,
        IUiLoop uiLoop,
        Func<string>? settingsPath = null,
        Func<string>? allowedRoot = null,
        Func<string>? processPath = null,
        Action<bool, string?>? initializeLogging = null) {
        this.notifier = notifier;
        this.autostart = autostart;
        this.singleInstance = singleInstance;
        this.uiLoop = uiLoop;
        this.settingsPath = settingsPath ?? (() => SettingsStore.DefaultPath);
        this.allowedRoot = allowedRoot ?? (() => SettingsStore.DefaultDirectory);
        this.processPath = processPath ?? (() => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, ProgramName + ".exe"));
        this.initializeLogging = initializeLogging ?? Logging.initialize;
    }

    public LaunchPreparation Prepare(LaunchRequest request) {
        if (request.UninstallCleanup) {
            bool cleaned = UninstallCleanup.Execute(autostart, allowedRoot());
            return new LaunchPreparation {
                ExitCode = cleaned ? 0 : 1,
                IsError = !cleaned,
                MessageTitle = cleaned ? null : ProgramName,
                Message = cleaned ? null : $"Failed to remove {ProgramName} user data"
            };
        }

        string path = settingsPath();
        SettingsStore.EnsurePathAllowed(path, allowedRoot());
        AppSettings stored = SettingsStore.Load(path);
        AppSettings merged = SettingsStore.MergeCli(stored, request.Cli);
        AppState state = AppState.FromSettings(merged);
        initializeLogging(state.FileLogEnabled, state.LogFilename);

        if (request.Help) {
            string filename = Path.GetFileName(processPath());
            return new LaunchPreparation {
                ExitCode = 0,
                MessageTitle = $"{ProgramName} {AppVersion.Current} usage",
                Message = UsageText.Build(filename, Logging.ResolveLogPath(null, SettingsStore.DefaultDirectory))
            };
        }

        if (request.AutostartOnLogon) {
            state.AutostartOnLogon = true;
        }

        SettingsStore.Save(path, state.ToSettings());

        if (!singleInstance.TryAcquire()) {
            singleInstance.SignalShowWindow();
            return new LaunchPreparation {
                ExitCode = 2,
                State = state
            };
        }

        if (state.AutostartOnLogon && !autostart.Register(processPath(), null)) {
            return new LaunchPreparation {
                ExitCode = 1,
                IsError = true,
                MessageTitle = ProgramName,
                Message = $"Failed to register {ProgramName} to start automatically on Windows logon"
            };
        }

        return new LaunchPreparation {
            ExitCode = 0,
            State = state,
            MessageTitle = request.AutostartOnLogon ? ProgramName : null,
            Message = request.AutostartOnLogon
                ? $"{ProgramName} is now running in the background, and will also start automatically each time you log in to Windows."
                : null
        };
    }

    public int Run(LaunchRequest request) {
        LaunchPreparation preparation = Prepare(request);
        if (preparation.Message is not null && preparation.MessageTitle is not null) {
            if (preparation.IsError) {
                notifier.Error(preparation.MessageTitle, preparation.Message);
            } else {
                notifier.Info(preparation.MessageTitle, preparation.Message);
            }
        }

        if (preparation.State is null || preparation.ExitCode != 0) {
            return preparation.ExitCode;
        }

        return RunUi(preparation.State, request.ShowWindow);
    }

    private int RunUi(AppState state, bool showWindow) {
        Logger logger = LogManager.GetLogger(typeof(AppSession).FullName!);
        logger.Info("{name} {version} starting", ProgramName, AppVersion.Current);
            OsVersion os = OsVersion.getCurrent();
            logger.Info("Operating system is {name} {marketingVersion} {version} {arch}", os.name, os.marketingVersion, os.version, os.architecture);
            logger.Info("{Locales are} {locales}", I18N.LOCALE_NAMES.Count == 1 ? "Locale is" : "Locales are", string.Join(", ", I18N.LOCALE_NAMES));

            using PinCache pinCache = new();
            pinCache.Lifetime = state.PinCacheLifetime;
            ChooserOptions options = new(
                state,
                pinCache,
                WindowTrust.Shared,
                NativeUia.Shared,
                new Fido2Devices(),
                new NativeDebuggerProbe(),
                () => {
                    SettingsStore.EnsurePathAllowed(settingsPath(), allowedRoot());
                    SettingsStore.Save(settingsPath(), state.ToSettings());
                });
            using WindowOpeningListener windowOpeningListener = new WindowOpeningListenerImpl();
            WindowsSecurityKeyChooser securityKeyChooser = new(options);

            windowOpeningListener.windowOpened += (_, window) => securityKeyChooser.chooseUsbSecurityKey(window);
            foreach (SystemWindow fidoPromptWindow in SystemWindow.FilterToplevelWindows(securityKeyChooser.isFidoPromptWindow)) {
                securityKeyChooser.chooseUsbSecurityKey(fidoPromptWindow);
            }

            logger.Info("Waiting for Windows Security FIDO dialog boxes to open");
            _ = I18N.getStrings(I18N.Key.SMARTPHONE);
            state.Report(ChooserEventKind.Waiting, "Waiting for Windows Security FIDO dialog boxes");

            using TrayIcon trayIcon = new(state, () => { }, () => { });
            StatusForm form = new(state, autostart, processPath(), settingsPath(), allowedRoot(), trayIcon, ExitApp, pinCache);
            trayIcon.AttachWindowActions(form.Reveal, ExitApp);
            _ = form.Handle;
            if (!state.TrayHintShown) {
                trayIcon.ShowRunningInTrayHint();
                state.TrayHintShown = true;
                SettingsStore.EnsurePathAllowed(settingsPath(), allowedRoot());
                SettingsStore.Save(settingsPath(), state.ToSettings());
            }

            SilentUpdateRuntime.Start(state, settingsPath(), allowedRoot(), processPath(), ExitFromBackground);

            Console.CancelKeyPress += (_, args) => {
                args.Cancel = true;
                Startup.RequestExit();
                ExitApp();
            };

            SystemEvents.SessionEnding += onWindowsLogoff;
            SystemEvents.SessionSwitch += onSessionSwitch;
            SystemEvents.PowerModeChanged += onPowerModeChanged;
            singleInstance.WatchShowWindow(() => {
                if (form.IsDisposed) {
                    return;
                }

                if (form.IsHandleCreated) {
                    form.BeginInvoke(form.Reveal);
                    return;
                }

                form.HandleCreated += (_, _) => form.BeginInvoke(form.Reveal);
            });

            try {
                if (showWindow) {
                    form.Reveal();
                }

                uiLoop.Run(form);
            } finally {
                SystemEvents.SessionEnding -= onWindowsLogoff;
                SystemEvents.SessionSwitch -= onSessionSwitch;
                SystemEvents.PowerModeChanged -= onPowerModeChanged;
                pinCache.Clear();
            }

            return 0;

        void ExitFromBackground() {
            if (form.IsHandleCreated && form.InvokeRequired) {
                form.BeginInvoke(ExitApp);
                return;
            }

            ExitApp();
        }

        void ExitApp() {
            Startup.RequestExit();
            Application.Exit();
        }

        void onWindowsLogoff(object sender, SessionEndingEventArgs args) {
            logger.Info("Exiting due to Windows session ending for {0}", args.Reason);
            SystemEvents.SessionEnding -= onWindowsLogoff;
            pinCache.Clear();
            ExitApp();
        }

        void onSessionSwitch(object sender, SessionSwitchEventArgs args) {
            if (args.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionLogoff or SessionSwitchReason.RemoteDisconnect) {
                pinCache.Clear();
            }
        }

        void onPowerModeChanged(object sender, PowerModeChangedEventArgs args) {
            if (args.Mode is PowerModes.Suspend) {
                pinCache.Clear();
            }
        }
    }

    public void Dispose() => singleInstance.Dispose();

}

internal static class AppVersion {

    public static string Current => typeof(AppSession).Assembly.GetName().Version?.ToString(3) ?? "0.8.0";

}
