using ManagedWinapi.Windows;

namespace AuthenticatorChooser.WindowOpening;

public interface WindowOpeningListener: IDisposable {

    event EventHandler<SystemWindow>? windowOpened;

}

public class WindowOpeningListenerImpl: WindowOpeningListener {

    private readonly ShellHook shellHook;

    public event EventHandler<SystemWindow>? windowOpened;

    public WindowOpeningListenerImpl(): this(new ShellHookImpl()) { }

    public WindowOpeningListenerImpl(ShellHook shellHook) {
        this.shellHook = shellHook;
        shellHook.shellEvent += onWindowOpened;
    }

    private void onWindowOpened(object? sender, ShellEventArgs args) {
        if (ShellEventPolicy.IsWindowCreated(args.shellEvent)) {
            windowOpened?.Invoke(this, new SystemWindow(args.windowHandle));
        }
    }

    public void Dispose() {
        shellHook.shellEvent -= onWindowOpened;
        shellHook.Dispose();
        GC.SuppressFinalize(this);
    }

}