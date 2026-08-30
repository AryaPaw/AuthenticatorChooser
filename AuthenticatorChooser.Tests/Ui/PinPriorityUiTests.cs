using FluentAssertions;
using NSubstitute;
using System.Windows.Forms;

namespace AuthenticatorChooser.Tests;

public sealed class PinPriorityUiTests {

    [Fact]
    public void StatusForm_CacheModeStoresWithoutPersistingPin() {
        RunSta(() => {
            StubFido2DeviceCounter devices = new() { Count = 1 };
            using PinCache cache = new(devices, new StubDebugger(), new StubClock());
            AppState state = new();
            IAutostartService autostart = Substitute.For<IAutostartService>();
            autostart.Register(Arg.Any<string>(), Arg.Any<string?>()).Returns(true);
            string root = Path.Combine(Path.GetTempPath(), "ac-pinui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string settings = Path.Combine(root, "settings.json");
            using TrayIcon tray = new(state);
            using StatusForm form = new(state, autostart, Path.Combine(root, "app.exe"), settings, root, tray, () => { }, cache);
            form.Reveal();
            Flatten(form).OfType<RadioButton>().Single(r => r.AccessibleName == "pinModeCache").Checked = true;
            state.PinMode.Should().Be(PinMode.Cache);
            Flatten(form).OfType<TextBox>().Should().NotContain(t => t.AccessibleName == "pinCacheValue");
            Flatten(form).OfType<Button>().Should().NotContain(b => b.AccessibleName == "pinCacheStore");
            Flatten(form).OfType<NumericUpDown>().Should().BeEmpty();
            Flatten(form).OfType<CheckBox>().Should().NotContain(c => c.AccessibleName == "pinCacheAutoSubmit");
            Flatten(form).OfType<Label>().Select(l => l.Text).Should().Contain(t => t.Contains("USB-key choice") && t.Contains("never written"));
            Flatten(form).OfType<Label>().Select(l => l.Text).Should().Contain(t => t.Contains("Enter once") && t.Contains("restart"));
            Flatten(form).OfType<Label>().Single(l => l.AccessibleName == "pinCacheStatus").Text.Should().Contain("press Enter");
            state.AutoSubmitPinLength = 6;
            state.Report(ChooserEventKind.Waiting, "ignore");
            Flatten(form).OfType<Label>().Single(l => l.AccessibleName == "pinCacheStatus").Text.Should().Contain("press Enter");
            state.LearnedPinLength = 10;
            Flatten(form).OfType<Label>().Single(l => l.AccessibleName == "pinCacheStatus").Text.Should().Contain("10 characters");
            cache.TryStore("1357902468").Should().Be(PinCacheStoreResult.Stored);
            state.Report(ChooserEventKind.Waiting, "cached");
            Flatten(form).OfType<Label>().Single(l => l.AccessibleName == "pinCacheStatus").Text.Should().Contain("cached");
            File.ReadAllText(settings).Should().NotContain("1357902468");
            Flatten(form).OfType<Button>().Single(b => b.AccessibleName == "pinCacheForget").PerformClick();
            cache.HasCached.Should().BeFalse();
            Directory.Delete(root, true);
        });
    }

    [Fact]
    public void PriorityForm_EditsOrderAndRejectsBuiltinDelete() {
        RunSta(() => {
            using AuthenticatorPriorityForm form = new(AuthenticatorPriorityCatalog.CreateDefaults());
            form.Show();
            ListBox list = Flatten(form).OfType<ListBox>().Single(b => b.AccessibleName == "priorityList");
            list.Items.Count.Should().Be(3);
            TextBox name = Flatten(form).OfType<TextBox>().Single(t => t.AccessibleName == "priorityName");
            name.Text = "1Password";
            Flatten(form).OfType<Button>().Single(b => b.AccessibleName == "priorityAdd").PerformClick();
            list.Items.Count.Should().Be(4);
            list.SelectedIndex = 0;
            Flatten(form).OfType<Button>().Single(b => b.AccessibleName == "priorityRemove").PerformClick();
            form.Result.Should().Contain(rule => rule.Id == AuthenticatorPriorityCatalog.UsbId);
            list.SelectedIndex = list.Items.Count - 1;
            Flatten(form).OfType<Button>().Single(b => b.AccessibleName == "priorityRemove").PerformClick();
            form.Result.Should().NotContain(rule => rule.DisplayName == "1Password");
            Flatten(form).OfType<Button>().Single(b => b.AccessibleName == "priorityRestore").PerformClick();
            form.Result.Should().HaveCount(3);
            list.SelectedIndex = 0;
            ComboBox action = Flatten(form).OfType<ComboBox>().Single(c => c.AccessibleName == "priorityAction");
            action.SelectedItem = "Ignore";
            form.Result[0].Action.Should().Be(AuthenticatorRuleAction.Ignore);
            Flatten(form).OfType<Button>().Single(b => b.AccessibleName == "priorityDown").PerformClick();
            form.Result[1].Id.Should().Be(AuthenticatorPriorityCatalog.UsbId);
            Flatten(form).OfType<Button>().Single(b => b.AccessibleName == "priorityUp").PerformClick();
            form.Result[0].Id.Should().Be(AuthenticatorPriorityCatalog.UsbId);
            form.Close();
        });
    }

    private static IEnumerable<Control> Flatten(Control root) {
        foreach (Control child in root.Controls) {
            yield return child;
            foreach (Control nested in Flatten(child)) {
                yield return nested;
            }
        }
    }

    private static void RunSta(Action action) {
        Exception? error = null;
        Thread thread = new(() => {
            try {
                action();
            } catch (Exception e) {
                error = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) {
            throw error;
        }
    }

    private sealed class StubFido2DeviceCounter: IFido2DeviceCounter {
        public int? Count { get; set; } = 1;
        public int? CountCtapHid() => Count;
    }

    private sealed class StubClock: IMonotonicClock {
        public long TickCount64 { get; set; } = 1;
    }

    private sealed class StubDebugger: IDebuggerProbe {
        public bool IsAttached { get; set; }
    }

}
