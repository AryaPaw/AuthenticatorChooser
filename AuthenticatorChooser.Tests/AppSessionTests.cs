using FluentAssertions;
using NSubstitute;

namespace AuthenticatorChooser.Tests;

public sealed class AppSessionTests: IDisposable {

    private readonly string root;
    private readonly string settingsPath;

    public AppSessionTests() {
        root = Path.Combine(Path.GetTempPath(), "AuthenticatorChooserSession", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        settingsPath = Path.Combine(root, "settings.json");
    }

    [Fact]
    public void Prepare_Help_DoesNotRequireMutex() {
        IAutostartService autostart = Substitute.For<IAutostartService>();
        ISingleInstanceService single = Substitute.For<ISingleInstanceService>();
        using AppSession session = Create(autostart, single);
        LaunchPreparation result = session.Prepare(Startup.ToLaunchRequest(true, false, false, null, (false, null)));
        result.ExitCode.Should().Be(0);
        result.Message.Should().Contain("--help");
        result.State.Should().BeNull();
        single.DidNotReceive().TryAcquire();
    }

    [Fact]
    public void Prepare_AutostartFailure_ReturnsOne() {
        IAutostartService autostart = Substitute.For<IAutostartService>();
        autostart.Register(Arg.Any<string>(), Arg.Any<string?>()).Returns(false);
        ISingleInstanceService single = Substitute.For<ISingleInstanceService>();
        single.TryAcquire().Returns(true);
        using AppSession session = Create(autostart, single);
        LaunchPreparation result = session.Prepare(Startup.ToLaunchRequest(false, true, false, null, (false, null)));
        result.ExitCode.Should().Be(1);
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Prepare_SecondInstance_SignalsShowWindow() {
        IAutostartService autostart = Substitute.For<IAutostartService>();
        ISingleInstanceService single = Substitute.For<ISingleInstanceService>();
        single.TryAcquire().Returns(false);
        using AppSession session = Create(autostart, single);
        LaunchPreparation result = session.Prepare(Startup.ToLaunchRequest(false, false, true, 6, (true, "x.log")));
        result.ExitCode.Should().Be(2);
        single.Received(1).SignalShowWindow();
        autostart.DidNotReceive().Register(Arg.Any<string>(), Arg.Any<string?>());
        SettingsStore.Load(settingsPath).SkipAllNonSecurityKeyOptions.Should().BeTrue();
        SettingsStore.Load(settingsPath).AutoSubmitPinLength.Should().Be(6);
    }

    [Fact]
    public void Prepare_Success_SavesSettings() {
        IAutostartService autostart = Substitute.For<IAutostartService>();
        autostart.Register(Arg.Any<string>(), Arg.Any<string?>()).Returns(true);
        ISingleInstanceService single = Substitute.For<ISingleInstanceService>();
        single.TryAcquire().Returns(true);
        using AppSession session = Create(autostart, single);
        LaunchPreparation result = session.Prepare(Startup.ToLaunchRequest(false, true, false, null, (false, null)));
        result.ExitCode.Should().Be(0);
        result.State.Should().NotBeNull();
        result.Message.Should().Contain("start automatically");
        SettingsStore.Load(settingsPath).AutostartOnLogon.Should().BeTrue();
    }

    [Fact]
    public void Run_Help_NotifiesAndReturnsZero() {
        IAutostartService autostart = Substitute.For<IAutostartService>();
        ISingleInstanceService single = Substitute.For<ISingleInstanceService>();
        IUserNotifier notifier = Substitute.For<IUserNotifier>();
        using AppSession session = Create(autostart, single, notifier);
        session.Run(Startup.ToLaunchRequest(true, false, false, null, (false, null))).Should().Be(0);
        notifier.Received(1).Info(Arg.Any<string>(), Arg.Is<string>(text => text.Contains("--autostart-on-logon")));
    }

    [Fact]
    public void Run_AutostartError_NotifiesError() {
        IAutostartService autostart = Substitute.For<IAutostartService>();
        autostart.Register(Arg.Any<string>(), Arg.Any<string?>()).Returns(false);
        ISingleInstanceService single = Substitute.For<ISingleInstanceService>();
        single.TryAcquire().Returns(true);
        IUserNotifier notifier = Substitute.For<IUserNotifier>();
        using AppSession session = Create(autostart, single, notifier);
        session.Run(Startup.ToLaunchRequest(false, true, false, null, (false, null))).Should().Be(1);
        notifier.Received(1).Error(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void Prepare_DefaultAutostart_RegistersWithoutCliFlag() {
        IAutostartService autostart = Substitute.For<IAutostartService>();
        autostart.Register(Arg.Any<string>(), Arg.Any<string?>()).Returns(true);
        ISingleInstanceService single = Substitute.For<ISingleInstanceService>();
        single.TryAcquire().Returns(true);
        using AppSession session = Create(autostart, single);
        LaunchPreparation result = session.Prepare(Startup.ToLaunchRequest(false, false, false, null, (false, null)));
        result.ExitCode.Should().Be(0);
        result.Message.Should().BeNull();
        autostart.Received(1).Register(Arg.Any<string>(), Arg.Any<string?>());
        SettingsStore.Load(settingsPath).AutostartOnLogon.Should().BeTrue();
    }

    public void Dispose() {
        if (Directory.Exists(root)) {
            Directory.Delete(root, true);
        }
    }

    private AppSession Create(IAutostartService autostart, ISingleInstanceService single, IUserNotifier? notifier = null) =>
        new(
            notifier ?? Substitute.For<IUserNotifier>(),
            autostart,
            single,
            Substitute.For<IUiLoop>(),
            () => settingsPath,
            () => root,
            () => Path.Combine(root, "AuthenticatorChooser.exe"),
            (_, _) => { });

}
