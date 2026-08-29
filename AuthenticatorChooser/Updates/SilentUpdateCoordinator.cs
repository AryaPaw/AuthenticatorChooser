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
    string CurrentVersion,
    DateTime UtcNow,
    TimeSpan MinInterval,
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
            || !context.State.AutoUpdateEnabled
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

        if (!SilentUpdatePolicy.ShouldPoll(context.State.LastUpdateCheckUtc, context.UtcNow, context.MinInterval)) {
            return SilentUpdateOutcome.Skipped;
        }

        if (!Version.TryParse(context.CurrentVersion, out Version? current)) {
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
        string sidecarName = SetupIntegrity.SidecarFileName(fileName);
        GitHubReleaseAsset? asset = latest.Assets.FirstOrDefault(item =>
            string.Equals(item.Name, fileName, StringComparison.OrdinalIgnoreCase));
        GitHubReleaseAsset? sidecar = latest.Assets.FirstOrDefault(item =>
            string.Equals(item.Name, sidecarName, StringComparison.OrdinalIgnoreCase));
        if (asset is null
            || sidecar is null
            || !SafeWeb.TryCreateAllowedUrl(asset.BrowserDownloadUrl, out Uri? downloadUrl)
            || downloadUrl is null
            || !SafeWeb.TryCreateAllowedUrl(sidecar.BrowserDownloadUrl, out Uri? sidecarUrl)
            || sidecarUrl is null) {
            return SilentUpdateOutcome.Failed;
        }

        string destination = Path.Combine(context.DownloadDirectory, fileName);
        string sidecarPath = Path.Combine(context.DownloadDirectory, sidecarName);
        if (!SilentUpdatePolicy.IsSafeSetupPath(destination, context.DownloadDirectory)) {
            return SilentUpdateOutcome.Failed;
        }

        bool downloaded = await context.Feed.Download(downloadUrl, destination, context.CancellationToken);
        bool sidecarOk = await context.Feed.Download(sidecarUrl, sidecarPath, context.CancellationToken);
        if (!downloaded || !sidecarOk) {
            return SilentUpdateOutcome.Failed;
        }

        if (!SetupIntegrity.TryParseSidecar(File.ReadAllText(sidecarPath), fileName, out string expected)
            || !SetupIntegrity.HashesMatch(expected, SetupIntegrity.HashFile(destination))) {
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
