using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using NLog;

namespace AuthenticatorChooser.Updates;

[ExcludeFromCodeCoverage]
internal static class SilentUpdateRuntime {

    public static void Start(
        AppState state,
        string settingsPath,
        string allowedRoot,
        string processPath,
        Action requestExit,
        SemaphoreSlim updateGate) {
        if (!SilentUpdatePolicy.AllowsBackgroundProcess(Process.GetCurrentProcess().ProcessName)) {
            return;
        }

        _ = Task.Run(() => Loop(state, settingsPath, allowedRoot, processPath, requestExit, updateGate, Startup.EXITING));
    }

    public static async Task<SilentUpdateOutcome> CheckNow(
        AppState state,
        bool autoUpdateEnabled,
        string settingsPath,
        string allowedRoot,
        string processPath,
        Action requestExit,
        CancellationToken cancellationToken) {
        string? applicationDirectory = Path.GetDirectoryName(processPath);
        if (string.IsNullOrWhiteSpace(applicationDirectory)) {
            return SilentUpdateOutcome.Failed;
        }

        string downloadDirectory = Path.Combine(
            Path.GetTempPath(),
            nameof(AuthenticatorChooser),
            "updates",
            Guid.NewGuid().ToString("N"));
        using GitHubReleaseFeed probe = new(GitHubReleaseFeed.CreateClient(
            timeout: NetworkWaitPolicy.ProbeTimeout,
            githubApi: false));
        using GitHubReleaseFeed feed = new(GitHubReleaseFeed.CreateClient());
        return await SilentUpdateCoordinator.RunOnce(new SilentUpdateContext(
            state,
            autoUpdateEnabled,
            AppVersion.Current,
            DateTime.UtcNow,
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

    private static async Task Loop(
        AppState state,
        string settingsPath,
        string allowedRoot,
        string processPath,
        Action requestExit,
        SemaphoreSlim updateGate,
        CancellationToken cancellationToken) {
        Logger logger = LogManager.GetLogger(typeof(SilentUpdateRuntime).FullName!);
        string? applicationDirectory = Path.GetDirectoryName(processPath);
        if (string.IsNullOrWhiteSpace(applicationDirectory)) {
            return;
        }

        using GitHubReleaseFeed probe = new(GitHubReleaseFeed.CreateClient(
            timeout: NetworkWaitPolicy.ProbeTimeout,
            githubApi: false));
        Architecture architecture = RuntimeInformation.ProcessArchitecture;

        while (!cancellationToken.IsCancellationRequested) {
            try {
                await NetworkWaitPolicy.WaitUntilOnline(
                    probe,
                    Timeout.InfiniteTimeSpan,
                    NetworkWaitPolicy.OfflineRetry,
                    cancellationToken);

                await updateGate.WaitAsync(cancellationToken);
                SilentUpdateOutcome outcome;
                bool exitRequested = false;
                try {
                    outcome = await CheckNow(
                        state,
                        state.AutoUpdateEnabled,
                        settingsPath,
                        allowedRoot,
                        processPath,
                        () => exitRequested = true,
                        cancellationToken);
                } finally {
                    updateGate.Release();
                }

                if (exitRequested) {
                    requestExit();
                }

                logger.Info("Silent update check finished with {outcome}", outcome);
                if (outcome is SilentUpdateOutcome.Applied or SilentUpdateOutcome.NoUpdate) {
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
            case SilentUpdateOutcome.Applied:
                return TimeSpan.Zero;
            default:
                SilentUpdateOutcome unreachable = outcome;
                throw new InvalidOperationException($"Unhandled silent update outcome {unreachable}");
        }
    }

}
