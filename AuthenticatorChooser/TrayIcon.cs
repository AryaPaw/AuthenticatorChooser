using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace AuthenticatorChooser;

public sealed class TrayIcon: IDisposable {

    private readonly AppState state;
    private readonly NotifyIcon notifyIcon;
    private readonly ToolStripMenuItem pauseMenuItem;

    private Action showWindow = () => { };
    private Action exit = () => { };

    public TrayIcon(AppState state, Action? showWindow = null, Action? exit = null) {
        this.state = state;
        this.showWindow = showWindow ?? this.showWindow;
        this.exit = exit ?? this.exit;
        pauseMenuItem = new ToolStripMenuItem(StatusPresenter.PauseActionLabel(state.Enabled));
        pauseMenuItem.Click += (_, _) => StatusPresenter.ToggleEnabled(state);

        ToolStripMenuItem openMenuItem = new("Open");
        openMenuItem.Click += (_, _) => this.showWindow();

        ToolStripMenuItem exitMenuItem = new("Exit");
        exitMenuItem.Click += (_, _) => this.exit();

        ContextMenuStrip contextMenu = new();
        contextMenu.Items.Add(openMenuItem);
        contextMenu.Items.Add(pauseMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitMenuItem);

        notifyIcon = new NotifyIcon {
            Text = TooltipText(state),
            Icon = AppIcons.CreateKeyIcon(),
            ContextMenuStrip = contextMenu,
            Visible = AllowsDesktopNotifications(Process.GetCurrentProcess().ProcessName)
        };
        notifyIcon.DoubleClick += (_, _) => this.showWindow();
        state.Changed += OnStateChanged;
    }

    public void AttachWindowActions(Action show, Action quit) {
        showWindow = show;
        exit = quit;
    }

    public void ShowRunningInTrayHint() {
        if (!AllowsDesktopNotifications(Process.GetCurrentProcess().ProcessName)) {
            return;
        }

        notifyIcon.BalloonTipTitle = nameof(AuthenticatorChooser);
        notifyIcon.BalloonTipText = "Still running in the notification area. Double-click the icon to open the status window.";
        notifyIcon.ShowBalloonTip(4000);
    }

    internal static bool AllowsDesktopNotifications(string processName) {
        if (string.IsNullOrWhiteSpace(processName)) {
            return true;
        }

        return !processName.StartsWith("testhost", StringComparison.OrdinalIgnoreCase)
            && !processName.StartsWith("vstest", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() {
        state.Changed -= OnStateChanged;
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnStateChanged(object? sender, EventArgs e) {
        if (notifyIcon.ContextMenuStrip?.InvokeRequired == true) {
            notifyIcon.ContextMenuStrip.BeginInvoke(Refresh);
            return;
        }

        Refresh();
    }

    private void Refresh() {
        pauseMenuItem.Text = StatusPresenter.PauseActionLabel(state.Enabled);
        notifyIcon.Text = TooltipText(state);
    }

    internal static string TooltipText(AppState current) =>
        $"{nameof(AuthenticatorChooser)} ({StatusPresenter.StatusLabel(current.Enabled)})";

}
