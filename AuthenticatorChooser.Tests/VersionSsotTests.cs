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
        AppVersion.Current.Should().Be(version);

        string iss = File.ReadAllText(issPath);
        iss.Should().NotMatchRegex(@"#define MyAppVersion ""\d+\.\d+\.\d+""");
        iss.Should().Contain("#ifndef MyAppVersion");
        iss.Should().Contain("#error");

        string getter = File.ReadAllText(Path.Combine(repo, "scripts", "Get-AppVersion.ps1"));
        getter.Should().Contain("AuthenticatorChooser.csproj");
        getter.Should().Contain("<Version>");

        string workflow = File.ReadAllText(workflowPath);
        iss.Should().Contain("function PrepareToInstall");
        iss.Should().MatchRegex(@"function PrepareToInstall[\s\S]*WaitUntilAppExited");
        workflow.Should().Contain("Read version from csproj");
        workflow.Should().Contain("Get-AppVersion.ps1");
        workflow.Should().NotMatchRegex(@"APP_VERSION:\s*\d+\.\d+\.\d+");
        workflow.Should().Contain("innosetup --version=6.7.1");
        workflow.Should().NotContain(".sha256");
        workflow.Should().NotContain("Get-FileHash");

        string gate = File.ReadAllText(Path.Combine(repo, "scripts", "release-gate.ps1"));
        gate.Should().Contain("Get-AppVersion.ps1");
    }

}
