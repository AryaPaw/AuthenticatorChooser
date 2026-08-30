using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class WindowTrustTests {

    [Fact]
    public void OnlyCredentialUiBrokerInSystem32_IsTrusted() {
        string system32 = Environment.SystemDirectory;
        WindowTrust.IsTrustedProcessPath(Path.Combine(system32, "CredentialUIBroker.exe")).Should().BeTrue();
        WindowTrust.IsTrustedProcessPath(Path.Combine(system32, "Consent.exe")).Should().BeFalse();
        WindowTrust.IsTrustedProcessPath(Path.Combine(system32, "LogonUI.exe")).Should().BeFalse();
        WindowTrust.IsTrustedProcessPath(Path.Combine(system32, "winlogon.exe")).Should().BeFalse();
        WindowTrust.IsTrustedProcessPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "CredentialUIBroker.exe")).Should().BeFalse();
        WindowTrust.IsTrustedProcessPath(@"C:\Program Files\Evil\CredentialUIBroker.exe").Should().BeFalse();
    }

    [Fact]
    public void IdentityMustKeepTheSameHwndAndPid() {
        WindowTrust.MatchesIdentity(1, 10, 1, 10).Should().BeTrue();
        WindowTrust.MatchesIdentity(1, 10, 2, 10).Should().BeFalse();
        WindowTrust.MatchesIdentity(1, 10, 1, 11).Should().BeFalse();
        WindowTrust.MatchesIdentity(IntPtr.Zero, 10, IntPtr.Zero, 10).Should().BeFalse();
    }

}
