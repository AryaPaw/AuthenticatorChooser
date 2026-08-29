using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class VersionSsotTests {

    [Fact]
    public void IssFallbackAndWorkflow_ReadCsprojVersion() {
        string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string csprojPath = Path.Combine(repo, "AuthenticatorChooser", "AuthenticatorChooser.csproj");
        string issPath = Path.Combine(repo, "installer", "AuthenticatorChooser.iss");
        string workflowPath = Path.Combine(repo, ".github", "workflows", "dotnet.yml");

        string? version = XDocument.Load(csprojPath)
            .Descendants("Version")
            .Select(node => node.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        version.Should().MatchRegex(@"^\d+\.\d+\.\d+$");

        string iss = File.ReadAllText(issPath);
        Regex.Match(iss, @"#define MyAppVersion ""(\d+\.\d+\.\d+)""").Groups[1].Value.Should().Be(version);

        string workflow = File.ReadAllText(workflowPath);
        iss.Should().Contain("function PrepareToInstall");
        iss.Should().MatchRegex(@"function PrepareToInstall[\s\S]*WaitUntilAppExited");
        workflow.Should().Contain("Read version from csproj");
        workflow.Should().Contain("AuthenticatorChooser.csproj");
        workflow.Should().NotContain("APP_VERSION: 0.7.0");
        workflow.Should().Contain("AuthenticatorChooser-Setup-win-x64.exe.sha256");
        workflow.Should().Contain("AuthenticatorChooser-Setup-win-arm64.exe.sha256");
        workflow.Should().Contain("innosetup --version=6.7.1");
        workflow.Should().Contain("Get-FileHash $setup -Algorithm SHA256");
    }

}
