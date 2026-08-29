using AuthenticatorChooser.WindowOpening;
using FluentAssertions;
using ManagedWinapi.Windows;

namespace AuthenticatorChooser.Tests;

public sealed class WindowOpeningListenerTests {

    [Fact]
    public void ForwardsOnlyWindowCreated() {
        FakeShellHook hook = new();
        using WindowOpeningListenerImpl listener = new(hook);
        SystemWindow? seen = null;
        listener.windowOpened += (_, window) => seen = window;

        hook.Raise(ShellEventArgs.ShellEvent.HSHELL_WINDOWDESTROYED, 1);
        seen.Should().BeNull();

        hook.Raise(ShellEventArgs.ShellEvent.HSHELL_WINDOWCREATED, 42);
        seen.Should().NotBeNull();
        seen!.HWnd.Should().Be(new IntPtr(42));
    }

    [Fact]
    public void ShellEventPolicy_RecognizesCreated() {
        ShellEventPolicy.IsWindowCreated(ShellEventArgs.ShellEvent.HSHELL_WINDOWCREATED).Should().BeTrue();
        ShellEventPolicy.IsWindowCreated(ShellEventArgs.ShellEvent.HSHELL_REDRAW).Should().BeFalse();
    }

    private sealed class FakeShellHook: ShellHook {

        public event EventHandler<ShellEventArgs>? shellEvent;

        public void Raise(ShellEventArgs.ShellEvent kind, int hwnd) =>
            shellEvent?.Invoke(this, new ShellEventArgs(kind, new IntPtr(hwnd)));

        public void Dispose() { }

    }

}
