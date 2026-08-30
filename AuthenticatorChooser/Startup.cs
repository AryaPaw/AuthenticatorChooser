using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.Conventions;
using System.Diagnostics.CodeAnalysis;
using System.Security.Principal;
using System.Windows.Forms;

namespace AuthenticatorChooser;

public class Startup {

    private static readonly CancellationTokenSource EXITING_TRIGGER = new();
    public static readonly  CancellationToken       EXITING         = EXITING_TRIGGER.Token;

    [Option("--skip-all-non-security-key-options", CommandOptionType.NoValue)]
    public bool skipAllNonSecurityKeyOptions { get; }

    [Option("--autosubmit-pin-length", CommandOptionType.SingleValue)]
    public int? autosubmitPinLength { get; }

    [Option("--autostart-on-logon", CommandOptionType.NoValue)]
    public bool autostartOnLogon { get; }

    [Option("-l|--log", CommandOptionType.SingleOrNoValue)]
    public (bool enabled, string? filename) log { get; }

    [Option(DefaultHelpOptionConvention.DefaultHelpTemplate, CommandOptionType.NoValue)]
    public bool help { get; }

    [Option("--show-window", CommandOptionType.NoValue)]
    public bool showWindow { get; }

    [Option("--uninstall-cleanup", CommandOptionType.NoValue)]
    public bool uninstallCleanup { get; }

    public static void RequestExit() => EXITING_TRIGGER.Cancel();

    public static LaunchRequest ToLaunchRequest(
        bool help,
        bool autostartOnLogon,
        bool skipAll,
        int? pinLength,
        (bool enabled, string? filename) log,
        bool uninstallCleanup = false,
        bool showWindow = false) =>
        new() {
            Help = help,
            AutostartOnLogon = autostartOnLogon,
            UninstallCleanup = uninstallCleanup,
            ShowWindow = showWindow,
            Cli = new CliOverrides(skipAll, pinLength, log.enabled, log.filename, autostartOnLogon)
        };

    [ExcludeFromCodeCoverage]
    [STAThread]
    public static int Main(string[] args) {
        try {
            using var app = new CommandLineApplication<Startup> {
                UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.Throw
            };
            app.Conventions.UseDefaultConventions();
            return app.Execute(args);
        } catch (CommandParsingException e) {
            MessageBox.Show(e.Message, $"{AppSession.ProgramName} {AppVersion.Current}", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    [ExcludeFromCodeCoverage]
    public int OnExecute() {
        Application.SetCompatibleTextRenderingDefault(false);
        Application.EnableVisualStyles();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        using AppSession appSession = new(
            new WinFormsUserNotifier(),
            new ScheduledTaskAutostartService(identity.Name, Environment.UserName),
            MutexSingleInstanceService.ForCurrentUser(),
            new WinFormsUiLoop());

        try {
            return appSession.Run(ToLaunchRequest(help, autostartOnLogon, skipAllNonSecurityKeyOptions, autosubmitPinLength, log, uninstallCleanup, showWindow));
        } catch (Exception e) when (e is not OutOfMemoryException) {
            MessageBox.Show($"Uncaught exception: {e}", AppSession.ProgramName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        } finally {
            NLog.LogManager.Shutdown();
        }
    }

}
