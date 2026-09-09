using System.Runtime.InteropServices;

namespace AuthenticatorChooser.Updates;

public enum SilentUpdateOutcome {

    Skipped,
    Busy,
    Offline,
    NoUpdate,
    Applied,
    Failed

}

public sealed record SilentUpdateContext(
    AppState State,
    bool AutoUpdateEnabled,
    string CurrentVersion,
    DateTime UtcNow,
    string ProcessName,
    string ApplicationDirectory,
    string DownloadDirectory,
    Architecture ProcessArchitecture,
    IInternetProbe Probe,
    IReleaseFeed Feed,
    ISetupInstaller Installer,
    Action SaveSettings,
    Action RequestExit,
    CancellationToken CancellationToken);

public static class SilentUpdateCoordinator {

    public static async Task<SilentUpdateOutcome> RunOnce(SilentUpdateContext context) {
        if (!SilentUpdatePolicy.AllowsBackgroundProcess(context.ProcessName)
            || !context.AutoUpdateEnabled
            || !SilentUpdatePolicy.HasInnoUninstaller(context.ApplicationDirectory)
            || SilentUpdatePolicy.RidFor(context.ProcessArchitecture) is null) {
            return SilentUpdateOutcome.Skipped;
        }

        if (!SilentUpdatePolicy.IsSafeToRestart()) {
            return SilentUpdateOutcome.Busy;
        }

        if (!await context.Probe.IsReachable(context.CancellationToken)) {
            return SilentUpdateOutcome.Offline;
        }

        if (!Version.TryParse(UpdatePolicy.Normalize(context.CurrentVersion), out Version? current)) {
            return SilentUpdateOutcome.Failed;
        }

        ReleaseQuery query = await context.Feed.QueryLatest(context.CancellationToken);
        if (query.NotFound) {
            RememberCheck(context);
            return SilentUpdateOutcome.NoUpdate;
        }

        GitHubReleaseSnapshot? latest = query.Snapshot;
        if (latest is null) {
            return SilentUpdateOutcome.Failed;
        }

        if (latest.Prerelease
            || !SilentUpdatePolicy.TryParseTag(latest.TagName, out Version? candidate)
            || candidate is null
            || !SilentUpdatePolicy.IsNewer(current, candidate)) {
            RememberCheck(context);
            return SilentUpdateOutcome.NoUpdate;
        }

        string rid = SilentUpdatePolicy.RidFor(context.ProcessArchitecture)!;
        string fileName = SilentUpdatePolicy.SetupFileName(rid);
        GitHubReleaseAsset? asset = latest.Assets.FirstOrDefault(item =>
            string.Equals(item.Name, fileName, StringComparison.OrdinalIgnoreCase));
        if (asset is null
            || !SetupIntegrity.TryParseGitHubDigest(asset.Digest, out string expected)
            || !Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out Uri? downloadUrl)
            || downloadUrl is null
            || !UpdatePolicy.IsAllowedAssetUrl(downloadUrl)) {
            return SilentUpdateOutcome.Failed;
        }

        string destination = Path.Combine(context.DownloadDirectory, fileName);
        if (!SilentUpdatePolicy.IsSafeSetupPath(destination, context.DownloadDirectory)) {
            return SilentUpdateOutcome.Failed;
        }

        bool downloaded = await context.Feed.Download(downloadUrl, destination, context.CancellationToken);
        if (!downloaded) {
            return SilentUpdateOutcome.Failed;
        }

        if (!SetupIntegrity.HashesMatch(expected, SetupIntegrity.HashFile(destination))) {
            return SilentUpdateOutcome.Failed;
        }

        if (!SilentUpdatePolicy.IsSafeToRestart()) {
            return SilentUpdateOutcome.Busy;
        }

        if (!context.Installer.TryStartSilent(destination)) {
            return SilentUpdateOutcome.Failed;
        }

        RememberCheck(context);
        context.RequestExit();
        return SilentUpdateOutcome.Applied;
    }

    private static void RememberCheck(SilentUpdateContext context) {
        context.State.LastUpdateCheckUtc = context.UtcNow;
        context.SaveSettings();
    }

}
