using System.Net;
using System.Net.Http;
using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class GitHubReleaseFeedTests {

    private const string LatestJson = """
        {
          "tag_name": "v0.8.0",
          "prerelease": false,
          "assets": [
            { "name": "AuthenticatorChooser-Setup-win-x64.exe", "browser_download_url": "https://github.com/AryaPaw/AuthenticatorChooser/releases/download/v0.8.0/AuthenticatorChooser-Setup-win-x64.exe" },
            { "name": "AuthenticatorChooser.exe", "browser_download_url": "https://github.com/AryaPaw/AuthenticatorChooser/releases/download/v0.8.0/AuthenticatorChooser.exe" }
          ]
        }
        """;

    [Fact]
    public void TryParse_ReadsTagAndHttpsGithubAssets() {
        GitHubReleaseFeed.TryParse(LatestJson, out GitHubReleaseSnapshot? snapshot).Should().BeTrue();
        snapshot!.TagName.Should().Be("v0.8.0");
        snapshot.Prerelease.Should().BeFalse();
        snapshot.Assets.Should().Contain(asset => asset.Name == "AuthenticatorChooser-Setup-win-x64.exe");
    }

    [Fact]
    public void TryParse_DropsNonGithubDownloadUrls() {
        const string json = """
            {
              "tag_name": "v0.8.0",
              "prerelease": false,
              "assets": [
                { "name": "AuthenticatorChooser-Setup-win-x64.exe", "browser_download_url": "https://evil.example/setup.exe" }
              ]
            }
            """;
        GitHubReleaseFeed.TryParse(json, out GitHubReleaseSnapshot? snapshot).Should().BeTrue();
        snapshot!.Assets.Should().BeEmpty();
    }

    [Fact]
    public void TryParse_RejectsPrerelease() {
        const string json = """{ "tag_name": "v0.9.0", "prerelease": true, "assets": [] }""";
        GitHubReleaseFeed.TryParse(json, out GitHubReleaseSnapshot? snapshot).Should().BeTrue();
        snapshot!.Prerelease.Should().BeTrue();
    }

    [Fact]
    public void TryParse_RejectsGarbage() {
        GitHubReleaseFeed.TryParse("not-json", out _).Should().BeFalse();
        GitHubReleaseFeed.TryParse("{}", out GitHubReleaseSnapshot? empty).Should().BeTrue();
        empty!.TagName.Should().BeNull();
    }

    [Fact]
    public async Task GetLatest_UsesApiAndParsesBody() {
        using GitHubReleaseFeed feed = new(GitHubReleaseFeed.CreateClient(new ScriptedHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(LatestJson)
        })));
        GitHubReleaseSnapshot? snapshot = (await feed.QueryLatest(CancellationToken.None)).Snapshot;
        snapshot!.TagName.Should().Be("v0.8.0");
    }

    [Fact]
    public async Task QueryLatest_MarksNotFoundOn404() {
        using GitHubReleaseFeed feed = new(GitHubReleaseFeed.CreateClient(new ScriptedHandler(new HttpResponseMessage(HttpStatusCode.NotFound))));
        ReleaseQuery query = await feed.QueryLatest(CancellationToken.None);
        query.NotFound.Should().BeTrue();
        query.Snapshot.Should().BeNull();
    }

    [Fact]
    public async Task QueryLatest_FailedOnServerError() {
        using GitHubReleaseFeed feed = new(GitHubReleaseFeed.CreateClient(new ScriptedHandler(new HttpResponseMessage(HttpStatusCode.BadGateway))));
        ReleaseQuery query = await feed.QueryLatest(CancellationToken.None);
        query.NotFound.Should().BeFalse();
        query.Snapshot.Should().BeNull();
    }

    [Fact]
    public async Task Download_WritesBytesForAllowedUrl() {
        string dest = Path.Combine(Path.GetTempPath(), "AuthenticatorChooserSilentTests", Guid.NewGuid().ToString("N"), "AuthenticatorChooser-Setup-win-x64.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        try {
            using GitHubReleaseFeed feed = new(GitHubReleaseFeed.CreateClient(new ScriptedHandler(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new ByteArrayContent([1, 2, 3])
            })));
            bool ok = await feed.Download(
                new Uri("https://github.com/AryaPaw/AuthenticatorChooser/releases/download/v0.8.0/AuthenticatorChooser-Setup-win-x64.exe"),
                dest,
                CancellationToken.None);
            ok.Should().BeTrue();
            File.ReadAllBytes(dest).Should().Equal(1, 2, 3);
        } finally {
            if (File.Exists(dest)) {
                File.Delete(dest);
            }
        }
    }

    [Fact]
    public async Task Download_RejectsNonGithubUrl() {
        using GitHubReleaseFeed feed = new(GitHubReleaseFeed.CreateClient(new ScriptedHandler(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new ByteArrayContent([1])
        })));
        (await feed.Download(new Uri("https://example.com/a.exe"), Path.GetTempFileName(), CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task QueryLatest_TimeoutWithoutCancel_IsFailedNotNotFound() {
        using GitHubReleaseFeed feed = new(GitHubReleaseFeed.CreateClient(new TimeoutHandler()));
        ReleaseQuery query = await feed.QueryLatest(CancellationToken.None);
        query.NotFound.Should().BeFalse();
        query.Snapshot.Should().BeNull();
    }

    [Fact]
    public async Task IsReachable_TrueWhenHttpResponds() {
        CapturingHandler handler = new(new HttpResponseMessage(HttpStatusCode.NotFound));
        using GitHubReleaseFeed feed = new(GitHubReleaseFeed.CreateClient(handler, githubApi: false));
        (await feed.IsReachable(CancellationToken.None)).Should().BeTrue();
        handler.RequestUri.Should().Be(GitHubReleaseFeed.ProbeUrl);
    }

    [Fact]
    public async Task IsReachable_FalseWhenNetworkFails() {
        using GitHubReleaseFeed feed = new(GitHubReleaseFeed.CreateClient(new ThrowingHandler()));
        (await feed.IsReachable(CancellationToken.None)).Should().BeFalse();
    }

    private sealed class TimeoutHandler: HttpMessageHandler {

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new TaskCanceledException("timeout");

    }

    private sealed class ThrowingHandler: HttpMessageHandler {

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("offline");

    }

    private sealed class CapturingHandler: HttpMessageHandler {

        private readonly HttpResponseMessage response;

        public CapturingHandler(HttpResponseMessage response) {
            this.response = response;
        }

        public string? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            RequestUri = request.RequestUri?.AbsoluteUri;
            return Task.FromResult(response);
        }

    }

    private sealed class ScriptedHandler: HttpMessageHandler {

        private readonly HttpResponseMessage response;

        public ScriptedHandler(HttpResponseMessage response) {
            this.response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);

    }

}
