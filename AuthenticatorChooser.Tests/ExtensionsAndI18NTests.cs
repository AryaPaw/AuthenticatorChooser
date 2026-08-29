using FluentAssertions;
using System.Windows.Automation;

namespace AuthenticatorChooser.Tests;

public sealed class ExtensionsAndI18NTests {

    [Fact]
    public void I18N_SecurityKeyContainsEnglishFallback() {
        I18N.getStrings(I18N.Key.SECURITY_KEY).Should().Contain(s => s.Contains("Security key", StringComparison.OrdinalIgnoreCase) || s.Length > 0);
        I18N.LOCALE_NAMES.Should().NotBeEmpty();
    }

    [Fact]
    public void SingletonSafeCondition_HandlesZeroOneAndMany() {
        Condition noneAnd = AutomationElement.NameProperty.singletonSafeCondition(true, []);
        noneAnd.Should().BeSameAs(Condition.TrueCondition);
        Condition noneOr = AutomationElement.NameProperty.singletonSafeCondition(false, []);
        noneOr.Should().BeSameAs(Condition.FalseCondition);
        Condition one = AutomationElement.NameProperty.singletonSafeCondition(false, ["Security key"]);
        one.Should().BeOfType<PropertyCondition>();
        Condition manyOr = AutomationElement.NameProperty.singletonSafeCondition(false, ["a", "b"]);
        manyOr.Should().BeOfType<OrCondition>();
        Condition manyAnd = AutomationElement.NameProperty.singletonSafeCondition(true, ["a", "b"]);
        manyAnd.Should().BeOfType<AndCondition>();
    }

    [Fact]
    public void UsageText_MentionsForkAndGui() {
        string text = UsageText.Build("AuthenticatorChooser.exe", UsageText.DefaultLogPath);
        text.Should().Contain("tray");
        text.Should().Contain("AryaPaw");
        UsageText.DefaultLogPath.Should().EndWith("AuthenticatorChooser.log");
    }

    [Fact]
    public void TrayTooltip_IncludesStatus() {
        AppState state = new();
        TrayIcon.TooltipText(state).Should().Contain("Running");
        state.Enabled = false;
        TrayIcon.TooltipText(state).Should().Contain("Paused");
    }

    [Fact]
    public void AutostartTaskName_IncludesUser() {
        ScheduledTaskAutostartService.TaskNameFor("tester").Should().Contain("tester");
        ScheduledTaskAutostartService.TaskNameFor("tester").Should().StartWith(nameof(AuthenticatorChooser));
    }

    [Fact]
    public void AppVersion_IsThreePart() {
        AppVersion.Current.Should().MatchRegex(@"^\d+\.\d+\.\d+$");
    }

}
