namespace AuthenticatorChooser.Updates;

internal static class UpdatePolicy {

    public const string Owner = "AryaPaw";

    public const string Repository = "AuthenticatorChooser";

    public static Uri LatestApi { get; } = new($"https://api.github.com/repos/{Owner}/{Repository}/releases/latest");

    public static bool IsAllowedAssetUrl(Uri url) => IsGitHubReleaseDownload(url);

    public static bool IsAllowedRedirectUrl(Uri url) {
        if (url.Scheme != Uri.UriSchemeHttps) {
            return false;
        }

        if (IsAllowedReleaseCdnHost(url.Host)) {
            return true;
        }

        return IsGitHubReleaseDownload(url);
    }

    public static string Normalize(string version) {
        string value = version.Trim();
        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase)) {
            value = value[1..];
        }

        int plus = value.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0) {
            value = value[..plus];
        }

        return value;
    }

    public static bool IsInsideRoot(string root, string candidate) {
        string rootFull = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(candidate);
        return full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedReleaseCdnHost(string host) =>
        host.Equals("objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("github-releases.githubusercontent.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsGitHubReleaseDownload(Uri url) {
        if (url.Scheme != Uri.UriSchemeHttps
            || !url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        string prefix = $"/{Owner}/{Repository}/releases/download/";
        string path = url.AbsolutePath;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && !path.Contains("..", StringComparison.Ordinal)
            && path.Length > prefix.Length;
    }

}
