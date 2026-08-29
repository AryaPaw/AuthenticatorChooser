namespace AuthenticatorChooser;

internal static class SafeWeb {

    public static bool TryCreateAllowedUrl(string url, out Uri? uri) {
        uri = null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed)) {
            return false;
        }

        if (parsed.Scheme != Uri.UriSchemeHttps) {
            return false;
        }

        if (!string.Equals(parsed.Host, "github.com", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        uri = parsed;
        return true;
    }

    public static bool OpenHttps(string url) {
        if (!TryCreateAllowedUrl(url, out Uri? uri) || uri is null) {
            return false;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        return true;
    }

}
