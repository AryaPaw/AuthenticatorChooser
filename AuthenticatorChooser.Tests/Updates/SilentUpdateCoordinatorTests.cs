using System.Runtime.InteropServices;
using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class SilentUpdateCoordinatorTests: IDisposable {

    private readonly string appDir;
    private readonly string tempDir;
    private readonly string settingsPath;
    private readonly AppState state;

    public SilentUpdateCoordinatorTests() {
        string root = Path.Combine(Path.GetTempPath(), "AuthenticatorChooserSilentCoord", Guid.NewGuid().ToString("N"));
        appDir = Path.Combine(root, "app");
        tempDir = Path.Combine(root, "tmp");
        Directory.CreateDirectory(appDir);
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(appDir, "unins000.exe"), "x");
        settingsPath = Path.Combine(root, "settings.json");
        state = AppState.FromSettings(new AppSettings());
    }

    [Fact]
    public async Task RunOnce_SkipsTestHost() {
        FakeFeed feed = new();
        SilentUpdateOutcome outcome = await SilentUpdateCoordinator.RunOnce(Context("testhost", feed, new FakeInstaller()));
        outcome.Should().Be(SilentUpdateOutcome.Skipped);
        feed.LatestCalls.Should().Be(0);
    }

    [Fact]
    public async Task RunOnce_SkipsPortableLayout() {
        File.Delete(Path.Combine(appDir, "unins000.exe"));
        FakeFeed feed = new();
        SilentUpdateOutcome outcome = await SilentUpdateCoordinator.RunOnce(Context("AuthenticatorChooser", feed, new FakeInstaller()));
        outcome.Should().Be(SilentUpdateOutcome.Skipped);
        feed.LatestCalls.Should().Be(0);
    }

    [Fact]
    public async Task RunOnce_SkipsWhenDisabled() {
        state.AutoUpdateEnabled = false;
        FakeFeed feed = new();
        SilentUpdateOutcome outcome = await SilentUpdateCoordinator.RunOnce(Context("AuthenticatorChooser", feed, new FakeInstaller()));
        outcome.Should().Be(SilentUpdateOutcome.Skipped);
        feed.LatestCalls.Should().Be(0);
    }

    [Fact]
    public async Task RunOnce_SkipsWhenFidoIsBusy() {
        using IDisposable _ = FidoActivity.Begin();
        FakeFeed feed = new();
        SilentUpdateOutcome outcome = await SilentUpdateCoordinator.RunOnce(Context("AuthenticatorChooser", feed, new FakeInstaller()));
        outcome.Should().Be(SilentUpdateOutcome.Busy);
        feed.LatestCalls.Should().Be(0);
    }

    [Fact]
    public async Task RunOnce_AllowsApplyAfterCompletedFidoEvent() {
        DateTime now = new(2026, 8, 29, 18, 0, 0, DateTimeKind.Utc);
        state.Report(ChooserEventKind.ChoseSecurityKey, "chose");
        FakeFeed feed = new() {
            Latest = new GitHubReleaseSnapshot("v0.8.0", false, ReleaseAssets("0.8.0")),
            DownloadOk = true
        };
        FakeInstaller installer = new();
        SilentUpdateOutcome outcome = await SilentUpdateCoordinator.RunOnce(Context("AuthenticatorChooser", feed, installer, now, "0.7.0"));
        outcome.Should().Be(SilentUpdateOutcome.Applied);
        installer.Started.Should().EndWith("AuthenticatorChooser-Setup-win-x64.exe");
    }

    [Fact]
    public async Task RunOnce_SkipsInstallIfFidoStartsDuringDownload() {
        DateTime now = new(2026, 8, 29, 18, 0, 0, DateTimeKind.Utc);
        IDisposable? held = null;
        FakeFeed feed = new() {
            Latest = new GitHubReleaseSnapshot("v0.8.0", false, ReleaseAssets("0.8.0")),
            DownloadOk = true,
            AfterDownload = _ => {
                held = FidoActivity.Begin();
            }
        };
        FakeInstaller installer = new();
        try {
            SilentUpdateOutcome outcome = await SilentUpdateCoordinator.RunOnce(Context("AuthenticatorChooser", feed, installer, now, "0.7.0"));
            outcome.Should().Be(SilentUpdateOutcome.Busy);
            installer.Started.Should().BeNull();
        } finally {
            held?.Dispose();
        }
    }

    [Fact]
    public async Task RunOnce_Offline_DoesNotCallFeed() {
        FakeFeed feed = new();
        SilentUpdateOutcome outcome = await SilentUpdateCoordinator.RunOnce(
            Context("AuthenticatorChooser", feed, new FakeInstaller(), probe: new FakeProbe(false)));
        outcome.Should().Be(SilentUpdateOutcome.Offline);
        feed.LatestCalls.Should().Be(0);
        state.LastUpdateCheckUtc.Should().BeNull();
    }

    [Fact]
    public async Task RunOnce_SkipsWhenCheckedRecently() {
        DateTime now = new(2026, 8, 29, 18, 0, 0, DateTimeKind.Utc);
        state.LastUpdateCheckUtc = now.AddHours(-1);
        FakeFeed feed = new();
        SilentUpdateOutcome outcome = await SilentUpdateCoordinator.RunOnce(Context("AuthenticatorChooser", feed, new FakeInstaller(), now));
        outcome.Should().Be(SilentUpdateOutcome.Skipped);
        feed.LatestCalls.Should().Be(0);
    }

    [Fact]
    public async Task RunOnce_SavesCheckWhenAlreadyCurrent() {
        DateTime now = new(2026, 8, 29, 18, 0, 0, DateTimeKind.Utc);
        FakeFeed feed = new() {
            Latest = new GitHubReleaseSnapshot("v0.7.0", false, ReleaseAssets("0.7.0"))
        };
        FakeInstaller installer = new();
        SilentUpdateOutcome outcome = await SilentUpdateCoordinator.RunOnce(Context("AuthenticatorChooser", feed, installer, now, "0.7.0"));
        outcome.Should().Be(SilentUpdateOutcome.NoUpdate);
        state.LastUpdateCheckUtc.Should().Be(now);
        installer.Started.Should().BeNull();
    }

    [Fact]
    public async Task RunOnce_IgnoresPrerelease() {
        DateTime now = new(2026, 8, 29, 18, 0, 0, DateTimeKind.Utc);
        FakeFeed feed = new() {
            Latest = new GitHubReleaseSnapshot("v0.9.0", true, ReleaseAssets("0.9.0"))
        };
        SilentUpdateOutcome outcome = await SilentUpdateCoordinator.RunOnce(Context("AuthenticatorChooser", feed, new FakeInstaller(), now, "0.7.0"));
        outcome.Should().Be(SilentUpdateOutcome.NoUpdate);
    }

    [Fact]
    public async Task RunOnce_DownloadsAndStartsSilentSetup() {
        DateTime now = new(2026, 8, 29, 18, 0, 0, DateTimeKind.Utc);
        string url = "https://github.com/AryaPaw/AuthenticatorChooser/releases/download/v0.8.0/AuthenticatorChooser-Setup-win-x64.exe";
        FakeFeed feed = new() {
            Latest = new GitHubReleaseSnapshot("v0.8.0", false, ReleaseAssets("0.8.0")),
            DownloadOk = true
        };
        FakeInstaller installer = new();
        int exits = 0;
        SilentUpdateOutcome outcome = await SilentUpdateCoordinator.RunOnce(Context("AuthenticatorChooser", feed, installer, now, "0.7.0", () => exits++));
        outcome.Should().Be(SilentUpdateOutcome.Applied);
        feed.DownloadedUrls.Should().Contain(url);
        installer.Started.Should().EndWith("AuthenticatorChooser-Setup-win-x64.exe");
        exits.Should().Be(1);
        state.LastUpdateCheckUtc.Should().Be(now);
    }

    [Fact]
    public async Task RunOnce_FailsWhenDownloadFails() {
        DateTime now = new(2026, 8, 29, 18, 0, 0, DateTimeKind.Utc);
        FakeFeed feed = new() {
            Latest = new GitHubReleaseSnapshot("v0.8.0", false, ReleaseAssets("0.8.0")),
            DownloadOk = false
        };
        FakeInstaller installer = new();
        SilentUpdateOutcome outcome = await SilentUpdateCoordinator.RunOnce(Context("AuthenticatorChooser", feed, installer, now, "0.7.0"));
        outcome.Should().Be(SilentUpdateOutcome.Failed);
        installer.Started.Should().BeNull();
        state.LastUpdateCheckUtc.Should().BeNull();
    }

    [Fact]
    public async Task RunOnce_NoUpdateWhenLatestReleaseMissing() {
        DateTime now = new(2026, 8, 29, 18, 0, 0, DateTimeKind.Utc);
        FakeFeed feed = new() { NotFound = true };
        SilentUpdateOutcome outcome = await SilentUpdateCoordinator.RunOnce(Context("AuthenticatorChooser", feed, new FakeInstaller(), now, "0.7.0"));
        outcome.Should().Be(SilentUpdateOutcome.NoUpdate);
        state.LastUpdateCheckUtc.Should().Be(now);
    }

    [Fact]
    public async Task RunOnce_FailsWhenShaDoesNotMatch() {
        DateTime now = new(2026, 8, 29, 18, 0, 0, DateTimeKind.Utc);
        FakeFeed feed = new() {
            Latest = new GitHubReleaseSnapshot("v0.8.0", false, ReleaseAssets("0.8.0", "sha256:" + new string('0', 64))),
            DownloadOk = true
        };
        FakeInstaller installer = new();
        SilentUpdateOutcome outcome = await SilentUpdateCoordinator.RunOnce(Context("AuthenticatorChooser", feed, installer, now, "0.7.0"));
        outcome.Should().Be(SilentUpdateOutcome.Failed);
        installer.Started.Should().BeNull();
    }

    [Fact]
    public async Task RunOnce_FailsWhenDigestMissing() {
        DateTime now = new(2026, 8, 29, 18, 0, 0, DateTimeKind.Utc);
        string setup = "https://github.com/AryaPaw/AuthenticatorChooser/releases/download/v0.8.0/AuthenticatorChooser-Setup-win-x64.exe";
        FakeFeed feed = new() {
            Latest = new GitHubReleaseSnapshot("v0.8.0", false, [
                new GitHubReleaseAsset("AuthenticatorChooser-Setup-win-x64.exe", setup, null)
            ]),
            DownloadOk = true
        };
        FakeInstaller installer = new();
        SilentUpdateOutcome outcome = await SilentUpdateCoordinator.RunOnce(Context("AuthenticatorChooser", feed, installer, now, "0.7.0"));
        outcome.Should().Be(SilentUpdateOutcome.Failed);
        installer.Started.Should().BeNull();
    }

    [Fact]
    public async Task RunOnce_FailsWhenDownloadUrlIsNotGithub() {
        DateTime now = new(2026, 8, 29, 18, 0, 0, DateTimeKind.Utc);
        FakeFeed feed = new() {
            Latest = new GitHubReleaseSnapshot("v0.8.0", false, [
                new GitHubReleaseAsset("AuthenticatorChooser-Setup-win-x64.exe", "https://evil.example/setup.exe", MatchingDigest())
            ]),
            DownloadOk = true
        };
        FakeInstaller installer = new();
        SilentUpdateOutcome outcome = await SilentUpdateCoordinator.RunOnce(Context("AuthenticatorChooser", feed, installer, now, "0.7.0"));
        outcome.Should().Be(SilentUpdateOutcome.Failed);
        installer.Started.Should().BeNull();
        feed.DownloadedUrls.Should().BeEmpty();
    }

    [Fact]
    public async Task RunOnce_FailsWhenFeedMissing() {
        DateTime now = new(2026, 8, 29, 18, 0, 0, DateTimeKind.Utc);
        FakeFeed feed = new() { Latest = null };
        SilentUpdateOutcome outcome = await SilentUpdateCoordinator.RunOnce(Context("AuthenticatorChooser", feed, new FakeInstaller(), now, "0.7.0"));
        outcome.Should().Be(SilentUpdateOutcome.Failed);
        state.LastUpdateCheckUtc.Should().BeNull();
    }

    public void Dispose() {
        string? root = Path.GetDirectoryName(appDir);
        if (root is not null && Directory.Exists(root)) {
            Directory.Delete(root, true);
        }
    }

    private SilentUpdateContext Context(
        string processName,
        IReleaseFeed feed,
        ISetupInstaller installer,
        DateTime? now = null,
        string currentVersion = "0.7.0",
        Action? exit = null,
        IInternetProbe? probe = null) =>
        new(
            state,
            currentVersion,
            now ?? new DateTime(2026, 8, 29, 18, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromHours(24),
            processName,
            appDir,
            tempDir,
            Architecture.X64,
            probe ?? new FakeProbe(true),
            feed,
            installer,
            Save,
            exit ?? (() => { }),
            CancellationToken.None);

    private static string MatchingDigest() =>
        "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData([1])).ToLowerInvariant();

    private static GitHubReleaseAsset[] ReleaseAssets(string version, string? digest = null) {
        string setup = $"https://github.com/AryaPaw/AuthenticatorChooser/releases/download/v{version}/AuthenticatorChooser-Setup-win-x64.exe";
        return [
            new GitHubReleaseAsset("AuthenticatorChooser-Setup-win-x64.exe", setup, digest ?? MatchingDigest())
        ];
    }

    private void Save() {
        SettingsStore.Save(settingsPath, state.ToSettings());
    }

    private sealed class FakeProbe: IInternetProbe {

        private readonly bool online;

        public FakeProbe(bool online) {
            this.online = online;
        }

        public Task<bool> IsReachable(CancellationToken cancellationToken) => Task.FromResult(online);

    }

    private sealed class FakeFeed: IReleaseFeed {

        public GitHubReleaseSnapshot? Latest { get; set; }

        public bool DownloadOk { get; set; }

        public int LatestCalls { get; private set; }

        public List<string> DownloadedUrls { get; } = [];

        public bool NotFound { get; set; }

        public Action<Uri>? AfterDownload { get; set; }

        public Task<ReleaseQuery> QueryLatest(CancellationToken cancellationToken) {
            LatestCalls++;
            if (NotFound) {
                return Task.FromResult(new ReleaseQuery(true, null));
            }

            return Task.FromResult(new ReleaseQuery(false, Latest));
        }

        public Task<bool> Download(Uri url, string destinationPath, CancellationToken cancellationToken) {
            DownloadedUrls.Add(url.AbsoluteUri);
            AfterDownload?.Invoke(url);
            if (!DownloadOk) {
                return Task.FromResult(false);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.WriteAllBytes(destinationPath, [1]);
            return Task.FromResult(true);
        }

    }

    private sealed class FakeInstaller: ISetupInstaller {

        public string? Started { get; private set; }

        public bool TryStartSilent(string setupPath) {
            Started = setupPath;
            return true;
        }

    }

}
