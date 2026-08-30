using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class AuthenticatorPriorityPolicyTests {

    private static readonly LocaleAuthenticatorNames Locale = new(
        ["Security key"],
        ["iPhone, iPad, or Android device"],
        ["This Windows device"]);

    [Fact]
    public void UsbAndNewPhone_SelectsUsb() {
        AuthenticatorDecision decision = AuthenticatorPriorityPolicy.Decide(
            [new ChoiceMatch("usb", "Security key"), new ChoiceMatch("phone", "iPhone, iPad, or Android device")],
            AuthenticatorPriorityCatalog.CreateDefaults(),
            Locale,
            false);
        decision.Kind.Should().Be(AuthenticatorDecisionKind.Select);
        decision.Choice!.Value.Name.Should().Be("Security key");
    }

    [Fact]
    public void UsbAndWindowsHello_Asks() {
        AuthenticatorDecision decision = AuthenticatorPriorityPolicy.Decide(
            [new ChoiceMatch("usb", "Security key"), new ChoiceMatch("hello", "This Windows device")],
            AuthenticatorPriorityCatalog.CreateDefaults(),
            Locale,
            false);
        decision.Kind.Should().Be(AuthenticatorDecisionKind.Ask);
    }

    [Fact]
    public void UnknownProvider_AsksEvenIfUsbIsSelect() {
        AuthenticatorDecision decision = AuthenticatorPriorityPolicy.Decide(
            [new ChoiceMatch("usb", "Security key"), new ChoiceMatch("plugin", "1Password")],
            AuthenticatorPriorityCatalog.CreateDefaults(),
            Locale,
            false);
        decision.Kind.Should().Be(AuthenticatorDecisionKind.Ask);
    }

    [Fact]
    public void SkipAll_SelectsUsbDespiteUnknown() {
        AuthenticatorDecision decision = AuthenticatorPriorityPolicy.Decide(
            [new ChoiceMatch("usb", "Security key"), new ChoiceMatch("plugin", "Bitwarden")],
            AuthenticatorPriorityCatalog.ApplySkipAll(AuthenticatorPriorityCatalog.CreateDefaults()),
            Locale,
            true);
        decision.Kind.Should().Be(AuthenticatorDecisionKind.Select);
        decision.Choice!.Value.Name.Should().Be("Security key");
    }

    [Fact]
    public void LearnedAsk_StopsAutomation() {
        List<AuthenticatorPriorityRule> rules = AuthenticatorPriorityCatalog.CreateDefaults().Select(rule => rule.Clone()).ToList();
        rules.Add(new AuthenticatorPriorityRule {
            Id = "learned:1",
            Kind = AuthenticatorKind.External,
            DisplayName = "1Password",
            Action = AuthenticatorRuleAction.Ask,
            BuiltIn = false
        });
        AuthenticatorDecision decision = AuthenticatorPriorityPolicy.Decide(
            [new ChoiceMatch("usb", "Security key"), new ChoiceMatch("plugin", "1Password")],
            rules,
            Locale,
            false);
        decision.Kind.Should().Be(AuthenticatorDecisionKind.Ask);
    }

    [Fact]
    public void Classify_UsesLocalizedBuiltInsThenExactExternal() {
        List<AuthenticatorPriorityRule> rules = AuthenticatorPriorityCatalog.CreateDefaults().Select(rule => rule.Clone()).ToList();
        rules.Add(new AuthenticatorPriorityRule {
            Id = "learned:phone",
            Kind = AuthenticatorKind.External,
            DisplayName = "Pixel 8",
            Action = AuthenticatorRuleAction.Ask
        });
        AuthenticatorPriorityPolicy.Classify("Security key", Locale, rules).Should().Be(AuthenticatorKind.Usb);
        AuthenticatorPriorityPolicy.Classify("iPhone, iPad, or Android device", Locale, rules).Should().Be(AuthenticatorKind.PairNewPhone);
        AuthenticatorPriorityPolicy.Classify("This Windows device", Locale, rules).Should().Be(AuthenticatorKind.WindowsHello);
        AuthenticatorPriorityPolicy.Classify("Pixel 8", Locale, rules).Should().Be(AuthenticatorKind.External);
        AuthenticatorPriorityPolicy.Classify("pixel 8", Locale, rules).Should().Be(AuthenticatorKind.External);
    }

    [Fact]
    public void Learn_AddsUnknownNamesAsAskAndNeverPrefersThem() {
        AppSettings settings = new();
        AppSettings learned = AuthenticatorPriorityPolicy.Learn(settings, ["Security key", "1Password", "1Password"], Locale);
        learned.PriorityRules.Should().ContainSingle(rule =>
            !rule.BuiltIn
            && rule.DisplayName == "1Password"
            && rule.Action == AuthenticatorRuleAction.Ask);
        AuthenticatorPriorityPolicy.Learn(learned, ["1Password"], Locale).PriorityRules.Should().HaveCount(learned.PriorityRules.Count);
    }

    [Fact]
    public void EnsureBuiltIns_RepairsMissingAndEmptyNames() {
        List<AuthenticatorPriorityRule> repaired = AuthenticatorPriorityCatalog.EnsureBuiltIns([
            new AuthenticatorPriorityRule {
                Id = AuthenticatorPriorityCatalog.UsbId,
                Kind = AuthenticatorKind.External,
                DisplayName = "",
                Action = AuthenticatorRuleAction.Ask,
                BuiltIn = false
            }
        ]);
        repaired.Should().Contain(rule => rule.Id == AuthenticatorPriorityCatalog.UsbId && rule.Kind == AuthenticatorKind.Usb && rule.BuiltIn && rule.DisplayName == "USB security key");
        repaired.Should().Contain(rule => rule.Id == AuthenticatorPriorityCatalog.PairNewPhoneId);
        repaired.Should().Contain(rule => rule.Id == AuthenticatorPriorityCatalog.WindowsHelloId);
        AuthenticatorPriorityCatalog.EnsureBuiltIns(null).Should().HaveCount(3);
        AuthenticatorPriorityCatalog.Summary(repaired).Should().Contain("USB:");
    }

    [Fact]
    public void IgnoreOnlyVisible_ReturnsNone() {
        List<AuthenticatorPriorityRule> rules = AuthenticatorPriorityCatalog.ApplySkipAll(AuthenticatorPriorityCatalog.CreateDefaults());
        rules.First(rule => rule.Id == AuthenticatorPriorityCatalog.UsbId).Action = AuthenticatorRuleAction.Ignore;
        AuthenticatorPriorityPolicy.Decide(
            [new ChoiceMatch("phone", "iPhone, iPad, or Android device")],
            rules,
            Locale,
            false).Kind.Should().Be(AuthenticatorDecisionKind.None);
    }

    [Fact]
    public void LifetimeLabels_AreExhaustive() {
        foreach (PinCacheLifetime lifetime in Enum.GetValues<PinCacheLifetime>()) {
            PinCacheLifetimePolicy.TtlSeconds(lifetime).Should().BeGreaterThanOrEqualTo(0);
            PinCacheLifetimePolicy.Label(lifetime).Should().NotBeNullOrWhiteSpace();
        }
    }

}
