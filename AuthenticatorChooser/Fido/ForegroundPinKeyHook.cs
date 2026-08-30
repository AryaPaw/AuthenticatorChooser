using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using NLog;

namespace AuthenticatorChooser.Fido;

internal interface IPinKeyHook: IDisposable {
    void Start(IntPtr windowHandle, int processId, PinLearnSession session);
    void Stop();
}

internal sealed class NullPinKeyHook: IPinKeyHook {
    public void Start(IntPtr windowHandle, int processId, PinLearnSession session) { }
    public void Stop() { }
    public void Dispose() { }
}

[ExcludeFromCodeCoverage]
internal sealed class ForegroundPinKeyHook: IPinKeyHook {

    private static readonly Logger Logger = LogManager.GetLogger(typeof(ForegroundPinKeyHook).FullName!);

    private const int WhKeyboardLl = 13;
    private const int HcAction = 0;
    private const int WmKeydown = 0x0100;
    private const int VkBack = 0x08;
    private const int VkReturn = 0x0D;
    private const uint WmQuit = 0x0012;

    private readonly object gate = new();
    private readonly ManualResetEventSlim started = new(false);
    private readonly Thread pumpThread;
    private volatile uint nativeThreadId;
    private IntPtr hook = IntPtr.Zero;
    private HookProc? proc;
    private IntPtr targetWindow;
    private int targetPid;
    private PinLearnSession? session;
    private bool disposed;

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    public ForegroundPinKeyHook() {
        pumpThread = new Thread(Pump) {
            Name = "pin-key-hook",
            IsBackground = true
        };
        pumpThread.SetApartmentState(ApartmentState.STA);
        pumpThread.Start();
        if (!started.Wait(TimeSpan.FromSeconds(5))) {
            Logger.Warn("PIN key hook thread did not start in time");
        }
    }

    public void Start(IntPtr windowHandle, int processId, PinLearnSession next) {
        lock (gate) {
            ObjectDisposedException.ThrowIf(disposed, this);
            targetWindow = windowHandle;
            targetPid = processId;
            session = next;
        }
    }

    public void Stop() {
        lock (gate) {
            targetWindow = IntPtr.Zero;
            targetPid = 0;
            session = null;
        }
    }

    public void Dispose() {
        lock (gate) {
            if (disposed) {
                return;
            }

            disposed = true;
            targetWindow = IntPtr.Zero;
            targetPid = 0;
            session = null;
        }

        if (pumpThread.IsAlive && nativeThreadId != 0) {
            PostThreadMessage(nativeThreadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
            if (!pumpThread.Join(TimeSpan.FromSeconds(2))) {
                Logger.Warn("PIN key hook thread did not exit");
            }
        }

        started.Dispose();
    }

    private void Pump() {
        nativeThreadId = GetCurrentThreadId();
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
        proc = OnHook;
        hook = SetWindowsHookEx(WhKeyboardLl, proc, IntPtr.Zero, 0);
        if (hook == IntPtr.Zero) {
            Logger.Warn("Keyboard hook for PIN capture was not installed (win32={code})", Marshal.GetLastWin32Error());
        } else {
            Logger.Info("PIN key hook installed on a dedicated message-loop thread");
        }

        started.Set();
        try {
            while (GetMessage(out Msg msg, IntPtr.Zero, 0, 0) > 0) {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        } finally {
            if (hook != IntPtr.Zero) {
                UnhookWindowsHookEx(hook);
                hook = IntPtr.Zero;
                proc = null;
            }
        }
    }

    private IntPtr OnHook(int code, IntPtr wParam, IntPtr lParam) {
        if (code == HcAction && wParam == WmKeydown) {
            KbdLlHook kbd = Marshal.PtrToStructure<KbdLlHook>(lParam);
            PinLearnSession? current;
            IntPtr hwnd;
            int pid;
            lock (gate) {
                current = session;
                hwnd = targetWindow;
                pid = targetPid;
            }

            IntPtr foreground = GetForegroundWindow();
            NativeSecurity.GetWindowThreadProcessId(foreground, out uint foregroundPid);
            IntPtr foregroundRoot = NativeSecurity.GetAncestorRoot(foreground);
            IntPtr dialogRoot = NativeSecurity.GetAncestorRoot(hwnd);
            if (current is not null && PinLearnPolicy.IsCaptureForeground(foreground, hwnd, pid, (int) foregroundPid, foregroundRoot, dialogRoot)) {
                Apply(current, kbd);
            }
        }

        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    private static void Apply(PinLearnSession current, KbdLlHook kbd) {
        if (kbd.vkCode == VkBack) {
            current.OnBackspace();
            return;
        }

        if (kbd.vkCode == VkReturn) {
            current.OnEnter();
            return;
        }

        if (DigitFromVk(kbd.vkCode) is char digit) {
            current.OnCharacter(digit);
            return;
        }

        char? mapped = MapChar(kbd);
        if (mapped is char value) {
            current.OnCharacter(value);
        }
    }

    private static char? DigitFromVk(uint vkCode) {
        if (vkCode is >= 0x30 and <= 0x39) {
            return (char) vkCode;
        }

        if (vkCode is >= 0x60 and <= 0x69) {
            return (char) ('0' + (vkCode - 0x60));
        }

        return null;
    }

    private static char? MapChar(KbdLlHook kbd) {
        byte[] state = new byte[256];
        if (!GetKeyboardState(state)) {
            return null;
        }

        StringBuilder buffer = new(8);
        int written = ToUnicode(kbd.vkCode, kbd.scanCode, state, buffer, buffer.Capacity, 0);
        if (written != 1) {
            return null;
        }

        return buffer[0];
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState(byte[] lpKeyState);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState, StringBuilder pwszBuff, int cchBuff, uint wFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref Msg lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref Msg lpMsg);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHook {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

}
