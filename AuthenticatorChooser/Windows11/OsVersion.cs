using Microsoft.Win32;
using System.Management;

namespace AuthenticatorChooser.Windows11;

public interface IOperatingSystemInfo {

    OsWmiData? QueryWmi();

    string? RegistryDisplayVersion();

    int RegistryUbr();

    string Architecture();

}

public readonly record struct OsWmiData(string Caption, string Version);

internal sealed class LiveOperatingSystemInfo: IOperatingSystemInfo {

    private const string NtCurrentVersionKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    public OsWmiData? QueryWmi() {
        try {
            using ManagementObjectSearcher search = new(new SelectQuery("Win32_OperatingSystem", null, ["Caption", "Version"]));
            using ManagementObjectCollection results = search.Get();
            using ManagementObject result = results.Cast<ManagementObject>().First();
            return new OsWmiData((string) result["Caption"], (string) result["Version"]);
        } catch (ManagementException) {
            return null;
        } catch (InvalidOperationException) {
            return null;
        }
    }

    public string? RegistryDisplayVersion() => Registry.GetValue(NtCurrentVersionKey, "DisplayVersion", null) as string;

    public int RegistryUbr() => Registry.GetValue(NtCurrentVersionKey, "UBR", 0) as int? ?? 0;

    public string Architecture() => Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? string.Empty;

}

/// <param name="name">Microsoft Windows 11 Pro</param>
/// <param name="marketingVersion">24H2</param>
/// <param name="version">10.0.26100.3775 (major version is 10 on Windows 11)</param>
/// <param name="architecture">AMD64</param>
internal readonly record struct OsVersion(string name, string marketingVersion, Version version, string architecture) {

    public static OsVersion getCurrent(IOperatingSystemInfo? info = null) {
        IOperatingSystemInfo source = info ?? new LiveOperatingSystemInfo();
        OsWmiData? wmi = source.QueryWmi();
        string name = wmi?.Caption ?? "Microsoft Windows";
        string versionText = wmi?.Version ?? Environment.OSVersion.Version.ToString(3);
        int ubr = source.RegistryUbr();
        return new OsVersion(
            name,
            source.RegistryDisplayVersion() ?? string.Empty,
            Version.Parse($"{versionText}.{ubr}"),
            source.Architecture());
    }

}
