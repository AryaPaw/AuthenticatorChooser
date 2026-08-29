using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AuthenticatorChooser.Updates;

public sealed record GitHubReleaseAsset(string Name, string BrowserDownloadUrl);

public sealed record GitHubReleaseSnapshot(string? TagName, bool Prerelease, IReadOnlyList<GitHubReleaseAsset> Assets);

public sealed record ReleaseQuery(bool NotFound, GitHubReleaseSnapshot? Snapshot);

public interface IReleaseFeed {

    Task<ReleaseQuery> QueryLatest(CancellationToken cancellationToken);

    Task<bool> Download(Uri url, string destinationPath, CancellationToken cancellationToken);

}

public sealed class GitHubReleaseFeed: IReleaseFeed, IInternetProbe, IDisposable {

    public const string LatestApiUrl = "https://api.github.com/repos/AryaPaw/AuthenticatorChooser/releases/latest";

    public const string ProbeUrl = "https://github.com/AryaPaw/AuthenticatorChooser";

    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient httpClient;

    public GitHubReleaseFeed(HttpClient httpClient) {
        this.httpClient = httpClient;
    }

    public static HttpClient CreateClient(HttpMessageHandler? handler = null, TimeSpan? timeout = null, bool githubApi = true) {
        HttpClient client = handler is null ? new HttpClient() : new HttpClient(handler, true);
        client.Timeout = timeout ?? TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"AuthenticatorChooser/{AppVersion.Current}");
        if (githubApi) {
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        }

        return client;
    }

    public static bool TryParse(string json, [NotNullWhen(true)] out GitHubReleaseSnapshot? snapshot) {
        snapshot = null;
        try {
            GitHubReleaseJson? parsed = JsonSerializer.Deserialize<GitHubReleaseJson>(json, JsonOptions);
            if (parsed is null) {
                return false;
            }

            List<GitHubReleaseAsset> assets = [];
            foreach (GitHubAssetJson asset in parsed.Assets ?? []) {
                if (string.IsNullOrWhiteSpace(asset.Name) || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl)) {
                    continue;
                }

                if (!SafeWeb.TryCreateAllowedUrl(asset.BrowserDownloadUrl, out Uri? uri) || uri is null) {
                    continue;
                }

                assets.Add(new GitHubReleaseAsset(asset.Name, uri.AbsoluteUri));
            }

            snapshot = new GitHubReleaseSnapshot(parsed.TagName, parsed.Prerelease, assets);
            return true;
        } catch (JsonException) {
            return false;
        }
    }

    public async Task<ReleaseQuery> QueryLatest(CancellationToken cancellationToken) {
        try {
            using HttpResponseMessage response = await httpClient.GetAsync(LatestApiUrl, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) {
                return new ReleaseQuery(true, null);
            }

            if (!response.IsSuccessStatusCode || Exceeds(response.Content, SilentUpdatePolicy.MaxApiBytes)) {
                return new ReleaseQuery(false, null);
            }

            string json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (json.Length > SilentUpdatePolicy.MaxApiBytes || !TryParse(json, out GitHubReleaseSnapshot? snapshot)) {
                return new ReleaseQuery(false, null);
            }

            return new ReleaseQuery(false, snapshot);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            return new ReleaseQuery(false, null);
        }
    }

    public async Task<bool> Download(Uri url, string destinationPath, CancellationToken cancellationToken) {
        if (!SafeWeb.TryCreateAllowedUrl(url.AbsoluteUri, out Uri? allowed) || allowed is null) {
            return false;
        }

        try {
            using HttpResponseMessage response = await httpClient.GetAsync(allowed, cancellationToken);
            if (!response.IsSuccessStatusCode) {
                return false;
            }

            long maxBytes = destinationPath.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase)
                ? SilentUpdatePolicy.MaxSidecarBytes
                : SilentUpdatePolicy.MaxSetupBytes;
            if (Exceeds(response.Content, maxBytes)) {
                return false;
            }

            string? directory = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrWhiteSpace(directory)) {
                return false;
            }

            Directory.CreateDirectory(directory);
            bool copied = false;
            try {
                await using FileStream file = new(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                copied = await CopyBounded(response.Content, file, maxBytes, cancellationToken);
            } finally {
                if (!copied && File.Exists(destinationPath)) {
                    File.Delete(destinationPath);
                }
            }

            return copied;
        } catch (IOException) {
            return false;
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            return false;
        }
    }

    public async Task<bool> IsReachable(CancellationToken cancellationToken) {
        try {
            using HttpResponseMessage response = await httpClient.GetAsync(
                ProbeUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            _ = response.StatusCode;
            return true;
        } catch (HttpRequestException) {
            return false;
        } catch (TaskCanceledException) {
            return false;
        } catch (IOException) {
            return false;
        }
    }

    public void Dispose() => httpClient.Dispose();

    private static bool Exceeds(HttpContent content, long maxBytes) =>
        content.Headers.ContentLength is long length && length > maxBytes;

    private static async Task<bool> CopyBounded(HttpContent content, FileStream file, long maxBytes, CancellationToken cancellationToken) {
        byte[] buffer = new byte[81920];
        long written = 0;
        await using Stream source = await content.ReadAsStreamAsync(cancellationToken);
        while (true) {
            int read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0) {
                return true;
            }

            written += read;
            if (written > maxBytes) {
                return false;
            }

            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private sealed class GitHubReleaseJson {

        public string? TagName { get; set; }

        public bool Prerelease { get; set; }

        public List<GitHubAssetJson>? Assets { get; set; }

    }

    private sealed class GitHubAssetJson {

        public string? Name { get; set; }

        public string? BrowserDownloadUrl { get; set; }

    }

}
