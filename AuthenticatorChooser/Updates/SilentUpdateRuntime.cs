using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using NLog;

namespace AuthenticatorChooser.Updates;

[ExcludeFromCodeCoverage]
internal static class SilentUpdateRuntime {

    public static void Start(AppState state, string settingsPath, string allowedRoot, string processPath, Action requestExit) {
        if (!SilentUpdatePolicy.AllowsBackgroundProcess(Process.GetCurrentProcess().ProcessName)) {
            return;
        }

        _ = Task.Run(() => Loop(state, settingsPath, allowedRoot, processPath, requestExit, Startup.EXITING));
    }

    private static async Task Loop(
        AppState state,
        string settingsPath,
        string allowedRoot,
        string processPath,
        Action requestExit,
        CancellationToken cancellationToken) {
        Logger logger = LogManager.GetLogger(typeof(SilentUpdateRuntime).FullName!);
        string? applicationDirectory = Path.GetDirectoryName(processPath);
        if (string.IsNullOrWhiteSpace(applicationDirectory)) {
            return;
        }

        using GitHubReleaseFeed probe = new(GitHubReleaseFeed.CreateClient(
            timeout: NetworkWaitPolicy.ProbeTimeout,
            githubApi: false));
        using GitHubReleaseFeed feed = new(GitHubReleaseFeed.CreateClient());
        Architecture architecture = RuntimeInformation.ProcessArchitecture;

        while (!cancellationToken.IsCancellationRequested) {
            try {
                await NetworkWaitPolicy.WaitUntilOnline(
                    probe,
                    Timeout.InfiniteTimeSpan,
                    NetworkWaitPolicy.OfflineRetry,
                    cancellationToken);

                SilentUpdateOutcome outcome = await RunOnce(
                    state,
                    settingsPath,
                    allowedRoot,
                    processPath,
                    applicationDirectory,
                    feed,
                    probe,
                    requestExit,
                    SilentUpdatePolicy.CheckInterval,
                    cancellationToken);

                logger.Info("Silent update check finished with {outcome}", outcome);
                if (outcome == SilentUpdateOutcome.Applied) {
                    return;
                }

                if (outcome == SilentUpdateOutcome.Skipped
                    && (!state.AutoUpdateEnabled
                        || !SilentUpdatePolicy.HasInnoUninstaller(applicationDirectory)
                        || SilentUpdatePolicy.RidFor(architecture) is null)) {
                    return;
                }

                await Task.Delay(DelayAfter(outcome), cancellationToken);
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                return;
            } catch (Exception exception) when (exception is not OutOfMemoryException) {
                logger.Error(exception, "Silent update check failed");
                await Task.Delay(SilentUpdatePolicy.FailedRetry, cancellationToken);
            }
        }
    }

    private static Task<SilentUpdateOutcome> RunOnce(
        AppState state,
        string settingsPath,
        string allowedRoot,
        string processPath,
        string applicationDirectory,
        IReleaseFeed feed,
        IInternetProbe probe,
        Action requestExit,
        TimeSpan minInterval,
        CancellationToken cancellationToken) {
        string downloadDirectory = Path.Combine(
            Path.GetTempPath(),
            nameof(AuthenticatorChooser),
            "updates",
            Guid.NewGuid().ToString("N"));
        return SilentUpdateCoordinator.RunOnce(new SilentUpdateContext(
            state,
            AppVersion.Current,
            DateTime.UtcNow,
            minInterval,
            Process.GetCurrentProcess().ProcessName,
            applicationDirectory,
            downloadDirectory,
            RuntimeInformation.ProcessArchitecture,
            probe,
            feed,
            new CmdSilentSetupInstaller(),
            () => {
                SettingsStore.EnsurePathAllowed(settingsPath, allowedRoot);
                SettingsStore.Save(settingsPath, state.ToSettings());
            },
            requestExit,
            cancellationToken));
    }

    private static TimeSpan DelayAfter(SilentUpdateOutcome outcome) {
        switch (outcome) {
            case SilentUpdateOutcome.Busy:
                return SilentUpdatePolicy.BusyRetry;
            case SilentUpdateOutcome.Offline:
                return NetworkWaitPolicy.OfflineRetry;
            case SilentUpdateOutcome.Failed:
                return SilentUpdatePolicy.FailedRetry;
            case SilentUpdateOutcome.NoUpdate:
            case SilentUpdateOutcome.Skipped:
                return SilentUpdatePolicy.CheckInterval;
            case SilentUpdateOutcome.Applied:
                return TimeSpan.Zero;
            default:
                SilentUpdateOutcome unreachable = outcome;
                throw new InvalidOperationException($"Unhandled silent update outcome {unreachable}");
        }
    }

}
