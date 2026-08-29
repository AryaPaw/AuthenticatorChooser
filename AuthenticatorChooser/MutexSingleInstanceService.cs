using System.Security.Principal;

namespace AuthenticatorChooser;

public interface ISingleInstanceService: IDisposable {

    bool TryAcquire();

    void SignalShowWindow();

    void WatchShowWindow(Action onShowWindow);

}

public sealed class MutexSingleInstanceService: ISingleInstanceService {

    private readonly Mutex mutex;
    private readonly EventWaitHandle showWindow;
    private readonly CancellationTokenSource watching = new();
    private bool ownsMutex;
    private Thread? watcher;

    public MutexSingleInstanceService(string userSid) {
        mutex = new Mutex(true, $@"Local\{nameof(AuthenticatorChooser)}_{userSid}", out ownsMutex);
        showWindow = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{nameof(AuthenticatorChooser)}_show_{userSid}");
    }

    public static MutexSingleInstanceService ForCurrentUser() {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new MutexSingleInstanceService(identity.User?.Value ?? "unknown");
    }

    public bool TryAcquire() => ownsMutex;

    public void SignalShowWindow() => showWindow.Set();

    public void WatchShowWindow(Action onShowWindow) {
        watcher = new Thread(() => {
            while (!watching.IsCancellationRequested) {
                if (showWindow.WaitOne(TimeSpan.FromMilliseconds(500))) {
                    if (!watching.IsCancellationRequested) {
                        onShowWindow();
                    }
                }
            }
        }) {
            IsBackground = true,
            Name = "AuthenticatorChooser-show-window"
        };
        watcher.Start();
    }

    public void Dispose() {
        watching.Cancel();
        showWindow.Set();
        watcher?.Join(TimeSpan.FromSeconds(2));
        if (ownsMutex) {
            mutex.ReleaseMutex();
        }

        mutex.Dispose();
        showWindow.Dispose();
        watching.Dispose();
    }

}
