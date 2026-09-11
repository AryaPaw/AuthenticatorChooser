using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AuthenticatorChooser.Updates;

public sealed record GitHubReleaseAsset(string Name, string BrowserDownloadUrl, string? Digest);

public sealed record GitHubReleaseSnapshot(string? TagName, bool Prerelease, IReadOnlyList<GitHubReleaseAsset> Assets);

public sealed record ReleaseQuery(bool NotFound, GitHubReleaseSnapshot? Snapshot);

public interface IReleaseFeed {

    Task<ReleaseQuery> QueryLatest(CancellationToken cancellationToken);

    Task<bool> Download(Uri url, string destinationPath, CancellationToken cancellationToken);

}

public sealed class GitHubReleaseFeed: IReleaseFeed, IInternetProbe, IDisposable {

    public const string LatestApiUrl = "https://api.github.com/repos/AryaPaw/AuthenticatorChooser/releases/latest";

    public static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(20);

    public static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient httpClient;

    public GitHubReleaseFeed(HttpClient httpClient) {
        this.httpClient = httpClient;
    }

    public static HttpClient CreateClient(HttpMessageHandler? handler = null, TimeSpan? timeout = null, bool githubApi = true) {
        HttpMessageHandler inner = handler ?? new SocketsHttpHandler {
            AllowAutoRedirect = false,
            UseCookies = false
        };
        HttpClient client = new(inner, true);
        client.Timeout = timeout ?? DownloadTimeout;
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

                if (!Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out Uri? uri)
                    || uri is null
                    || !UpdatePolicy.IsAllowedAssetUrl(uri)) {
                    continue;
                }

                assets.Add(new GitHubReleaseAsset(asset.Name, uri.AbsoluteUri, asset.Digest));
            }

            snapshot = new GitHubReleaseSnapshot(parsed.TagName, parsed.Prerelease, assets);
            return true;
        } catch (JsonException) {
            return false;
        }
    }

    public async Task<ReleaseQuery> QueryLatest(CancellationToken cancellationToken) {
        try {
            using CancellationTokenSource queryTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            queryTimeout.CancelAfter(QueryTimeout);
            using HttpResponseMessage response = await httpClient.GetAsync(UpdatePolicy.LatestApi, queryTimeout.Token);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) {
                return new ReleaseQuery(true, null);
            }

            int status = (int) response.StatusCode;
            if (status is 429 or >= 500 || !response.IsSuccessStatusCode || Exceeds(response.Content, SilentUpdatePolicy.MaxApiBytes)) {
                return new ReleaseQuery(false, null);
            }

            string json = await response.Content.ReadAsStringAsync(queryTimeout.Token);
            if (json.Length > SilentUpdatePolicy.MaxApiBytes || !TryParse(json, out GitHubReleaseSnapshot? snapshot)) {
                return new ReleaseQuery(false, null);
            }

            return new ReleaseQuery(false, snapshot);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            return new ReleaseQuery(false, null);
        } catch (HttpRequestException) {
            return new ReleaseQuery(false, null);
        }
    }

    public async Task<bool> Download(Uri url, string destinationPath, CancellationToken cancellationToken) {
        if (!UpdatePolicy.IsAllowedAssetUrl(url)) {
            return false;
        }

        try {
            string? directory = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrWhiteSpace(directory) || !UpdatePolicy.IsInsideRoot(directory, destinationPath)) {
                return false;
            }

            Directory.CreateDirectory(directory);
            if (File.Exists(destinationPath)) {
                File.Delete(destinationPath);
            }

            bool copied = false;
            try {
                await using FileStream file = new(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using HttpResponseMessage response = await GetFollowingRedirectsAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode) {
                    return false;
                }

                if (Exceeds(response.Content, SilentUpdatePolicy.MaxSetupBytes)) {
                    return false;
                }

                copied = await CopyBounded(response.Content, file, SilentUpdatePolicy.MaxSetupBytes, cancellationToken);
            } finally {
                if (!copied && File.Exists(destinationPath)) {
                    File.Delete(destinationPath);
                }
            }

            return copied;
        } catch (IOException) {
            return false;
        } catch (HttpRequestException) {
            return false;
        } catch (InvalidOperationException) {
            return false;
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            return false;
        }
    }

    public async Task<bool> IsReachable(CancellationToken cancellationToken) {
        try {
            using CancellationTokenSource probeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            probeTimeout.CancelAfter(QueryTimeout);
            using HttpResponseMessage response = await httpClient.GetAsync(
                UpdatePolicy.LatestApi,
                HttpCompletionOption.ResponseHeadersRead,
                probeTimeout.Token);
            _ = response.StatusCode;
            return true;
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            return false;
        } catch (HttpRequestException) {
            return false;
        } catch (IOException) {
            return false;
        }
    }

    public void Dispose() => httpClient.Dispose();

    private async Task<HttpResponseMessage> GetFollowingRedirectsAsync(Uri url, CancellationToken cancellationToken) {
        Uri current = url;
        for (int hop = 0; hop <= 5; hop++) {
            bool allowed = hop == 0
                ? UpdatePolicy.IsAllowedAssetUrl(current)
                : UpdatePolicy.IsAllowedRedirectUrl(current);
            if (!allowed) {
                throw new InvalidOperationException("Download URL is not allowed.");
            }

            HttpResponseMessage response = await httpClient.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            int status = (int) response.StatusCode;
            if (status is not (301 or 302 or 303 or 307 or 308)) {
                return response;
            }

            Uri? next = response.Headers.Location;
            response.Dispose();
            if (next is null) {
                throw new InvalidOperationException("Redirect without Location.");
            }

            if (!next.IsAbsoluteUri) {
                next = new Uri(current, next);
            }

            current = next;
        }

        throw new InvalidOperationException("Too many redirects.");
    }

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

        public string? Digest { get; set; }

    }

}
