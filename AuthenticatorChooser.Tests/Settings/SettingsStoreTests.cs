using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class SettingsStoreTests: IDisposable {

    private readonly string root;

    public SettingsStoreTests() {
        root = Path.Combine(Path.GetTempPath(), "AuthenticatorChooserTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    [Fact]
    public void Load_ReturnsDefaultsWhenMissing() {
        AppSettings loaded = SettingsStore.Load(Path.Combine(root, "missing.json"));
        loaded.Enabled.Should().BeTrue();
        loaded.AutostartOnLogon.Should().BeTrue();
        loaded.AutoUpdateEnabled.Should().BeTrue();
        loaded.SchemaVersion.Should().Be(AppSettings.CurrentSchema);
    }

    [Fact]
    public void Migrate_EnablesAutostartOnOldSchema() {
        AppSettings migrated = SettingsStore.Migrate(new AppSettings {
            SchemaVersion = 0,
            AutostartOnLogon = false,
            Enabled = true
        });
        migrated.SchemaVersion.Should().Be(AppSettings.CurrentSchema);
        migrated.AutostartOnLogon.Should().BeTrue();
        migrated.Enabled.Should().BeTrue();
    }

    [Fact]
    public void SaveAndLoad_RoundTrips() {
        string path = Path.Combine(root, "settings.json");
        DateTime checkedAt = new(2026, 8, 29, 17, 0, 0, DateTimeKind.Utc);
        SettingsStore.Save(path, new AppSettings {
            Enabled = false,
            AutoSubmitPinLength = 6,
            SkipAllNonSecurityKeyOptions = true,
            AutoUpdateEnabled = false,
            LastUpdateCheckUtc = checkedAt
        });
        AppSettings loaded = SettingsStore.Load(path);
        loaded.Enabled.Should().BeFalse();
        loaded.AutoSubmitPinLength.Should().Be(6);
        loaded.SkipAllNonSecurityKeyOptions.Should().BeTrue();
        loaded.AutoUpdateEnabled.Should().BeFalse();
        loaded.LastUpdateCheckUtc.Should().Be(checkedAt);
        loaded.PinMode.Should().Be(PinMode.Off);
        loaded.PinCacheLifetime.Should().Be(PinCacheLifetime.TwoMinutes);
        loaded.PriorityRules.Should().NotBeEmpty();
        File.ReadAllText(path).Should().NotContain("\"Pin\"");
        File.ReadAllText(path).Should().NotContain("123456");
    }

    [Fact]
    public void EnsurePathAllowed_RejectsEscape() {
        Action act = () => SettingsStore.EnsurePathAllowed(@"C:\Windows\settings.json", root);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MergeCli_OverridesStoredValues() {
        AppSettings merged = SettingsStore.MergeCli(
            new AppSettings { AutoSubmitPinLength = 0 },
            new CliOverrides(true, 8, true, "log.txt", true));
        merged.SkipAllNonSecurityKeyOptions.Should().BeTrue();
        merged.AutoSubmitPinLength.Should().Be(8);
        merged.FileLogEnabled.Should().BeTrue();
        merged.LogFilename.Should().Be("log.txt");
        merged.AutostartOnLogon.Should().BeTrue();
    }

    [Fact]
    public void MergeCli_DropsShortPin() {
        SettingsStore.MergeCli(new AppSettings(), new CliOverrides(false, 2, false, null, false)).AutoSubmitPinLength.Should().Be(0);
    }

    [Fact]
    public void Migrate_Schema2LengthAndSkipAll_BecomesExclusivePinModeAndUsbSelect() {
        AppSettings migrated = SettingsStore.Migrate(new AppSettings {
            SchemaVersion = 2,
            AutoSubmitPinLength = 6,
            SkipAllNonSecurityKeyOptions = true,
            AutostartOnLogon = false
        });
        migrated.SchemaVersion.Should().Be(3);
        migrated.PinMode.Should().Be(PinMode.Length);
        migrated.AutoSubmitPinLength.Should().Be(6);
        migrated.AutostartOnLogon.Should().BeFalse();
        AuthenticatorPriorityCatalog.ActionFor(migrated.PriorityRules, AuthenticatorPriorityCatalog.UsbId)
            .Should().Be(AuthenticatorRuleAction.Select);
        AuthenticatorPriorityCatalog.ActionFor(migrated.PriorityRules, AuthenticatorPriorityCatalog.WindowsHelloId)
            .Should().Be(AuthenticatorRuleAction.Ignore);
        AuthenticatorPriorityCatalog.ActionFor(migrated.PriorityRules, AuthenticatorPriorityCatalog.PairNewPhoneId)
            .Should().Be(AuthenticatorRuleAction.Ignore);
    }

    [Fact]
    public void MergeCli_SkipAllRewritesRulesAndPinLengthArmsLengthMode() {
        AppSettings merged = SettingsStore.MergeCli(
            new AppSettings { PinMode = PinMode.Off },
            new CliOverrides(true, 8, false, null, false));
        merged.SkipAllNonSecurityKeyOptions.Should().BeTrue();
        merged.PinMode.Should().Be(PinMode.Length);
        AuthenticatorPriorityCatalog.ActionFor(merged.PriorityRules, AuthenticatorPriorityCatalog.UsbId)
            .Should().Be(AuthenticatorRuleAction.Select);
        merged.PriorityRules.Where(rule => rule.Id != AuthenticatorPriorityCatalog.UsbId)
            .Should().OnlyContain(rule => rule.Action == AuthenticatorRuleAction.Ignore);
    }

    [Fact]
    public void Save_ReplacesAtomically() {
        string path = Path.Combine(root, "atomic.json");
        SettingsStore.Save(path, new AppSettings { Enabled = false, PinMode = PinMode.Cache });
        SettingsStore.Save(path, new AppSettings { Enabled = true, PinMode = PinMode.Length, AutoSubmitPinLength = 6 });
        File.Exists(path + ".tmp").Should().BeFalse();
        AppSettings loaded = SettingsStore.Load(path);
        loaded.Enabled.Should().BeTrue();
        loaded.PinMode.Should().Be(PinMode.Length);
        loaded.AutoSubmitPinLength.Should().Be(6);
    }

    public void Dispose() {
        if (Directory.Exists(root)) {
            Directory.Delete(root, true);
        }
    }

}
