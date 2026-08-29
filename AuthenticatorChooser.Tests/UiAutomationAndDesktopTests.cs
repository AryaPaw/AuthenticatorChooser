using AuthenticatorChooser.WindowOpening;
using AuthenticatorChooser.Windows11;
using FluentAssertions;
using ManagedWinapi.Windows;
using NSubstitute;
using System.Windows.Automation;
using System.Windows.Forms;

namespace AuthenticatorChooser.Tests;

public sealed class UiAutomationAndDesktopTests: IDisposable {

    public UiAutomationAndDesktopTests() {
        Win11Strategy.FindChoicesOverride = (_, _) => Task.FromResult<IReadOnlyCollection<AutomationElement>?>(Array.Empty<AutomationElement>());
        Win11Strategy.FindPinOverride = (_, _) => Task.FromResult<AutomationElement?>(null);
    }

    public void Dispose() {
        Win11Strategy.FindChoicesOverride = null;
        Win11Strategy.FindPinOverride = null;
    }

    [Fact]
    public void Chooser_IgnoresNonFidoWindow() {
        RunSta(() => {
            WindowsSecurityKeyChooser chooser = new(new ChooserOptions(new AppState()));
            SystemWindow window = new(IntPtr.Zero);
            chooser.isFidoPromptWindow(window).Should().BeFalse();
            chooser.chooseUsbSecurityKey(window);
        });
    }

    [Fact]
    public void PinAutosubmit_ReadsLengthAndMissesOkOnPlainForm() {
        RunSta(() => {
            using Form form = new() { Width = 160, Height = 90, Text = "pin-host" };
            using TextBox box = new() { UseSystemPasswordChar = true, Text = "1234" };
            form.Controls.Add(box);
            form.Show();
            AutomationElement el = AutomationElement.FromHandle(form.Handle);
            PinAutosubmit.TryInvokeOk(el).Should().BeFalse();
            PinAutosubmit.TryReadLength(AutomationElement.FromHandle(box.Handle)).Should().BeGreaterThanOrEqualTo(0);
            form.Close();
        });
    }

    [Fact]
    public void Strategies_HandleMissingChoices() {
        RunSta(() => {
            using Form form = new() { Text = "test", Width = 200, Height = 100 };
            form.Show();
            AutomationElement el = AutomationElement.FromHandle(form.Handle);
            ChooserOptions options = new(new AppState());
            new Win1125H2Strategy(options).handleWindow(I18N.getStrings(I18N.Key.CHOOSE_A_PASSKEY).First(), el, el, false).GetAwaiter().GetResult();

            AppState aggressive = new();
            aggressive.SkipAllNonSecurityKeyOptions = true;
            new Win1123H2Strategy(new ChooserOptions(aggressive)).handleWindow(I18N.getStrings(I18N.Key.SIGN_IN_WITH_YOUR_PASSKEY).First(), el, el, false).GetAwaiter().GetResult();
            form.Close();
        });
    }

    [Fact]
    public void ShellHook_ConstructsOnSta() {
        RunSta(() => {
            using ShellHookImpl hook = new();
            hook.Should().NotBeNull();
        });
    }

    [Fact]
    public void StatusForm_TogglesOptions() {
        RunSta(() => {
            AppState state = new();
            IAutostartService autostart = Substitute.For<IAutostartService>();
            autostart.Register(Arg.Any<string>(), Arg.Any<string?>()).Returns(true);
            autostart.Unregister().Returns(true);
            string root = Path.Combine(Path.GetTempPath(), "ac-form-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string settings = Path.Combine(root, "settings.json");
            using TrayIcon tray = new(state);
            using StatusForm form = new(state, autostart, Path.Combine(root, "app.exe"), settings, root, tray, () => { });
            form.Reveal();
            foreach (CheckBox box in Flatten(form).OfType<CheckBox>()) {
                box.Checked = !box.Checked;
            }

            TextBox pin = Flatten(form).OfType<TextBox>().Single(t => t.AccessibleName == "pinSample");
            Button toggle = Flatten(form).OfType<Button>().Single(b => b.AccessibleName == "pinToggle");
            toggle.Text.Should().Be("Turn on");
            pin.Text = "123456";
            form.ApplyPinToggle().Kind.Should().Be(PinToggleKind.TurnOn);
            pin.Text.Should().BeEmpty();
            pin.Enabled.Should().BeFalse();
            toggle.Text.Should().Be("Turn off");
            state.AutoSubmitPinLength.Should().Be(6);
            File.ReadAllText(settings).Should().NotContain("123456");
            form.TurnOffPinAutosubmit();
            toggle.Text.Should().Be("Turn on");
            pin.Enabled.Should().BeTrue();
            pin.Text = "12";
            form.ApplyPinToggle().Kind.Should().Be(PinToggleKind.RejectedNeedLength);
            state.AutoSubmitPinLength.Should().BeNull();
            SettingsStore.Load(settings).AutoSubmitPinLength.Should().Be(0);
            Flatten(form).OfType<Label>().Select(l => l.Text).Should().Contain(t => t.Contains("Ben Hutchison") && t.Contains("AryaPaw"));
            form.ClientSize.Height.Should().BeGreaterThanOrEqualTo(600);
            Flatten(form).OfType<Button>().First(b => b.AccessibleName == "pauseToggle").PerformClick();
            state.Enabled.Should().BeFalse();
            form.HideToTrayIfUserClosing(CloseReason.WindowsShutDown).Should().BeFalse();
            tray.AttachWindowActions(() => { }, () => { });
            tray.ShowRunningInTrayHint();
            state.FileLogEnabled = true;
            state.LogFilename = "x.log";
            Directory.Delete(root, true);
        });
    }

    [Fact]
    public void AppSession_RunUi_ReturnsAfterLoop() {
        Exception? error = null;
        Thread thread = new(() => {
            try {
                string root = Path.Combine(Path.GetTempPath(), "ac-runui-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                string settings = Path.Combine(root, "settings.json");
                ISingleInstanceService single = Substitute.For<ISingleInstanceService>();
                single.TryAcquire().Returns(true);
                IAutostartService autostart = Substitute.For<IAutostartService>();
                autostart.Register(Arg.Any<string>(), Arg.Any<string?>()).Returns(true);
                using AppSession session = new(
                    Substitute.For<IUserNotifier>(),
                    autostart,
                    single,
                    new ImmediateReturnLoop(),
                    () => settings,
                    () => root,
                    () => Path.Combine(root, "AuthenticatorChooser.exe"),
                    (_, _) => { });
                session.Run(Startup.ToLaunchRequest(false, false, false, null, (false, null))).Should().Be(0);
                Directory.Delete(root, true);
            } catch (Exception e) {
                error = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(20))) {
            throw new TimeoutException("AppSession.RunUi timed out");
        }

        if (error is not null) {
            throw error;
        }
    }

    [Fact]
    public void AutostartService_RegisterUnregister_DoNotThrow() {
        ScheduledTaskAutostartService service = new("missing\\user", "coverageuser" + Guid.NewGuid().ToString("N")[..8]);
        bool registered = service.Register(Path.Combine(Path.GetTempPath(), "AuthenticatorChooser.exe"), null);
        if (registered) {
            service.IsRegistered();
            service.Unregister();
        } else {
            service.Unregister();
        }
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

    private sealed class ImmediateReturnLoop: IUiLoop {

        public void Run(StatusForm form) {
            form.Show();
            form.Reveal();
        }

    }

}
