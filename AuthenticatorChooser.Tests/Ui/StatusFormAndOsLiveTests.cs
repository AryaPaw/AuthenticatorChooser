using AuthenticatorChooser.Windows11;
using FluentAssertions;
using NSubstitute;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AuthenticatorChooser.Tests;

public sealed class StatusFormAndOsLiveTests {

    [Fact]
    public void StatusForm_CloseHidesInsteadOfExiting() {
        Exception? failure = null;
        Thread thread = new(() => {
            try {
                AppState state = new();
                IAutostartService autostart = Substitute.For<IAutostartService>();
                string root = Path.Combine(Path.GetTempPath(), "AuthenticatorChooserForm", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                string settings = Path.Combine(root, "settings.json");
                using TrayIcon tray = new(state);
                using StatusForm form = new(state, autostart, Path.Combine(root, "app.exe"), settings, root, tray, () => { });
                form.Reveal();
                form.ClientSize.Width.Should().BeGreaterThanOrEqualTo(640);
                form.Text.Should().Contain("AuthenticatorChooser");
                form.HideToTrayIfUserClosing(CloseReason.UserClosing).Should().BeTrue();
                form.Visible.Should().BeFalse();
                state.TrayHintShown.Should().BeTrue();
                Directory.Delete(root, true);
            } catch (Exception e) {
                failure = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure.Should().BeNull();
    }

    [Fact]
    public void Capture_StatusWindowPng_WhenRequested() {
        if (Environment.GetEnvironmentVariable("CAPTURE_STATUS_FORM") != "1") {
            return;
        }

        Exception? failure = null;
        Thread thread = new(() => {
            try {
                try {
                    Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                    Application.EnableVisualStyles();
                } catch (InvalidOperationException) {
                }
                AppState state = new();
                IAutostartService autostart = Substitute.For<IAutostartService>();
                autostart.IsRegistered().Returns(true);
                string root = Path.Combine(Path.GetTempPath(), "AuthenticatorChooserCapture", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                string settings = Path.Combine(root, "settings.json");
                using TrayIcon tray = new(state);
                using StatusForm form = new(state, autostart, Path.Combine(root, "app.exe"), settings, root, tray, () => { });
                form.TopMost = true;
                form.Reveal();
                form.Refresh();
                Application.DoEvents();
                Thread.Sleep(400);
                string dest = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".github", "images", "status-window.png"));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                using Bitmap bitmap = new(form.Width, form.Height);
                using (Graphics graphics = Graphics.FromImage(bitmap)) {
                    IntPtr hdc = graphics.GetHdc();
                    try {
                        PrintWindow(form.Handle, hdc, 2).Should().BeTrue();
                    } finally {
                        graphics.ReleaseHdc(hdc);
                    }
                }

                bitmap.Save(dest);
                File.Exists(dest).Should().BeTrue();
                new FileInfo(dest).Length.Should().BeGreaterThan(1000);
                Directory.Delete(root, true);
            } catch (Exception e) {
                failure = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure.Should().BeNull();
    }

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [Fact]
    public void LiveOsVersion_DoesNotThrow() {
        OsVersion version = OsVersion.getCurrent();
        version.name.Should().NotBeNullOrWhiteSpace();
        version.architecture.Should().NotBeNull();
    }

    [Fact]
    public void LoggingInitialize_AppliesConfiguration() {
        Logging.initialize(true, "ac-init.log");
        NLog.LogManager.Configuration.Should().NotBeNull();
        NLog.LogManager.Configuration!.AllTargets.Should().NotBeEmpty();
        NLog.LogManager.Shutdown();
    }

}
