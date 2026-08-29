using System.Drawing;
using System.Windows.Forms;

namespace AuthenticatorChooser.Ui;

public sealed class StatusForm: Form {

    private readonly AppState state;
    private readonly IAutostartService autostart;
    private readonly string executablePath;
    private readonly string settingsPath;
    private readonly string allowedRoot;
    private readonly TrayIcon trayIcon;
    private readonly Action exit;
    private readonly List<Label> wrappingLabels = [];
    private readonly Icon windowIcon;
    private TableLayoutPanel shell = null!;
    private StatusBadge statusBadge = null!;
    private Label eventValue = null!;
    private CheckBox skipAllBox = null!;
    private CheckBox autostartBox = null!;
    private CheckBox logBox = null!;
    private CheckBox autoUpdateBox = null!;
    private StatusPinBlock pinBlock = null!;
    private StatusFooter footer = null!;
    private ThemedButton pauseButton = null!;
    private bool forceClose;
    private bool syncing;
    private bool allowShow;

    public StatusForm(AppState state, IAutostartService autostart, string executablePath, string settingsPath, string allowedRoot, TrayIcon trayIcon, Action exit) {
        this.state = state;
        this.autostart = autostart;
        this.executablePath = executablePath;
        this.settingsPath = settingsPath;
        this.allowedRoot = allowedRoot;
        this.trayIcon = trayIcon;
        this.exit = exit;
        windowIcon = AppIcons.CreateKeyIcon();

        Text = nameof(AuthenticatorChooser);
        Icon = windowIcon;
        Font = UiTheme.Body;
        ForeColor = UiTheme.Ink;
        BackColor = UiTheme.Surface;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(640, 700);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ShowInTaskbar = false;
        Padding = Padding.Empty;

        shell = new TableLayoutPanel {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = UiTheme.Surface,
            Padding = Padding.Empty
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        shell.Controls.Add(BuildHeader(), 0, 0);
        shell.Controls.Add(BuildActions(), 0, 1);
        shell.Controls.Add(BuildBody(), 0, 2);
        footer = new StatusFooter();
        shell.Controls.Add(footer, 0, 3);
        Controls.Add(shell);

        Load += (_, _) => FitToContent();
        Resize += (_, _) => ApplyWrapWidths();
        state.Changed += OnStateChanged;
        BindFromState();
        FormClosing += OnFormClosing;
        ClientSize = new Size(700, 720);
    }

    public void Reveal() {
        allowShow = true;
        ShowInTaskbar = true;
        Icon = windowIcon;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        FitToContent();
    }

    protected override void SetVisibleCore(bool value) {
        base.SetVisibleCore(allowShow && value);
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            state.Changed -= OnStateChanged;
        }

        base.Dispose(disposing);
    }

    private Panel BuildHeader() {
        TableLayoutPanel header = new() {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Brand950,
            Padding = new Padding(UiTheme.PagePad, 18, UiTheme.PagePad, 18),
            ColumnCount = 2,
            RowCount = 3
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        PictureBox icon = new() {
            Size = new Size(48, 48),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = windowIcon.ToBitmap(),
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 12, 0)
        };
        header.SetRowSpan(icon, 3);
        header.Controls.Add(icon, 0, 0);

        TableLayoutPanel titleRow = new() {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 4)
        };
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        Label title = new() {
            AutoSize = true,
            Text = nameof(AuthenticatorChooser),
            Font = UiTheme.Title,
            ForeColor = UiTheme.OnBrand,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        titleRow.Controls.Add(title, 0, 0);
        titleRow.Controls.Add(VersionLink(), 1, 0);
        header.Controls.Add(titleRow, 1, 0);

        Label subtitle = Wrap(AppCredits.ProductSubtitle, UiTheme.Body, UiTheme.OnHeaderMuted, Color.Transparent);
        subtitle.Margin = new Padding(0, 0, 0, 10);
        header.Controls.Add(subtitle, 1, 1);

        FlowLayoutPanel statusRow = new() {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        statusBadge = new StatusBadge();
        eventValue = Wrap("", UiTheme.Caption, UiTheme.OnHeaderMuted, Color.Transparent);
        eventValue.Margin = new Padding(0, 6, 0, 0);
        statusRow.Controls.Add(statusBadge);
        statusRow.Controls.Add(eventValue);
        header.Controls.Add(statusRow, 1, 2);
        return header;
    }

    private Panel BuildActions() {
        FlowLayoutPanel actions = new() {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(UiTheme.PagePad, 16, UiTheme.PagePad, 8),
            BackColor = UiTheme.Surface
        };
        pauseButton = new ThemedButton("Pause", true) { AccessibleName = "pauseToggle" };
        ThemedButton quit = new("Exit", false);
        pauseButton.Click += (_, _) => StatusPresenter.ToggleEnabled(state);
        quit.Click += (_, _) => {
            forceClose = true;
            exit();
        };
        actions.Controls.AddRange([pauseButton, quit]);
        return actions;
    }

    private Panel BuildBody() {
        TableLayoutPanel host = new() {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(UiTheme.PagePad, 4, UiTheme.PagePad, 8),
            BackColor = UiTheme.Surface
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        CardPanel fido = new() { Dock = DockStyle.Fill, AutoSize = true };
        TableLayoutPanel fidoStack = Stack();
        Add(fidoStack, Heading("FIDO prompts"), 12);
        skipAllBox = CreateCheck("Always choose the USB security key");
        skipAllBox.AccessibleName = "skipAllOptions";
        skipAllBox.CheckedChanged += (_, _) => {
            if (syncing) {
                return;
            }

            state.SkipAllNonSecurityKeyOptions = skipAllBox.Checked;
            Persist();
        };
        Add(fidoStack, skipAllBox, 6);
        Add(fidoStack, Wrap("When this is off, the Security Key is chosen only if the other option is pairing a new phone. When it is on, Windows Hello and a paired phone are skipped too.", UiTheme.Caption, UiTheme.Muted, UiTheme.Card), 16);
        Add(fidoStack, Heading("Autosubmit security-key PIN"), 8);
        pinBlock = new StatusPinBlock(state, Persist);
        Add(fidoStack, pinBlock, 0);
        fido.Controls.Add(fidoStack);

        CardPanel app = new() { Dock = DockStyle.Fill, AutoSize = true };
        TableLayoutPanel appStack = Stack();
        Add(appStack, Heading("This computer"), 12);
        autostartBox = CreateCheck("Start in the background when I sign in to Windows");
        autostartBox.AccessibleName = "autostartOnLogon";
        autostartBox.CheckedChanged += (_, _) => {
            if (syncing) {
                return;
            }

            bool want = autostartBox.Checked;
            bool ok = want ? autostart.Register(executablePath, null) : autostart.Unregister();
            if (!ok) {
                MessageBox.Show(this, "Could not update the logon scheduled task.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                syncing = true;
                autostartBox.Checked = state.AutostartOnLogon;
                syncing = false;
                return;
            }

            state.AutostartOnLogon = want;
            Persist();
        };
        Add(appStack, autostartBox, 10);
        logBox = CreateCheck("Write a debug log under AppData");
        logBox.AccessibleName = "writeDebugLog";
        logBox.CheckedChanged += (_, _) => {
            if (syncing) {
                return;
            }

            state.FileLogEnabled = logBox.Checked;
            Logging.initialize(state.FileLogEnabled, state.LogFilename);
            Persist();
        };
        Add(appStack, logBox, 10);
        autoUpdateBox = CreateCheck("Install updates silently from GitHub");
        autoUpdateBox.AccessibleName = "autoUpdateEnabled";
        autoUpdateBox.CheckedChanged += (_, _) => {
            if (syncing) {
                return;
            }

            state.AutoUpdateEnabled = autoUpdateBox.Checked;
            Persist();
        };
        Add(appStack, autoUpdateBox, 6);
        Add(appStack, Wrap("When a newer GitHub Release exists, the installer is downloaded and applied in the background. No notifications.", UiTheme.Caption, UiTheme.Muted, UiTheme.Card), 12);
        FlowLayoutPanel appButtons = new() {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = UiTheme.Card,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        ThemedButton openLog = new("Open log", false);
        openLog.Click += (_, _) => {
            string logPath = Logging.ResolveLogPath(state.LogFilename, allowedRoot);
            if (File.Exists(logPath)) {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(logPath) { UseShellExecute = true });
            } else {
                MessageBox.Show(this, $"Log file not found yet:\n{logPath}", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        ThemedButton reset = new("Reset settings", false);
        reset.AccessibleName = "resetSettings";
        reset.MinimumSize = new Size(168, 40);
        reset.MaximumSize = new Size(168, 40);
        reset.Size = new Size(168, 40);
        reset.Click += (_, _) => ResetSettings();
        appButtons.Controls.AddRange([openLog, reset]);
        Add(appStack, appButtons, 0);
        app.Controls.Add(appStack);

        host.Controls.Add(fido, 0, 0);
        host.Controls.Add(app, 0, 1);
        return host;
    }

    private static TableLayoutPanel Stack() {
        TableLayoutPanel stack = new() {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 0,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            BackColor = UiTheme.Card
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        return stack;
    }

    private static void Add(TableLayoutPanel stack, Control control, int bottom) {
        control.Margin = new Padding(0, 0, 0, bottom);
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.Controls.Add(control, 0, stack.RowCount);
        stack.RowCount++;
    }

    private static Label Heading(string text) =>
        new() {
            AutoSize = true,
            Text = text,
            Font = UiTheme.Section,
            ForeColor = UiTheme.Ink,
            BackColor = UiTheme.Card,
            Margin = Padding.Empty
        };

    private Label Wrap(string text, Font font, Color color, Color back) {
        Label label = new() {
            AutoSize = true,
            Text = text,
            Font = font,
            ForeColor = color,
            BackColor = back,
            MaximumSize = new Size(600, 0),
            UseMnemonic = false
        };
        wrappingLabels.Add(label);
        return label;
    }

    private static CheckBox CreateCheck(string text) =>
        new() {
            AutoSize = true,
            Text = text,
            Font = UiTheme.Body,
            ForeColor = UiTheme.Ink,
            BackColor = UiTheme.Card,
            UseVisualStyleBackColor = true,
            Margin = Padding.Empty
        };

    private void ApplyWrapWidths() {
        int width = Math.Max(280, ClientSize.Width - (UiTheme.PagePad * 2) - 56);
        foreach (Label label in wrappingLabels) {
            label.MaximumSize = new Size(width, 0);
        }

        pinBlock.ApplyWrapWidth(width);
        footer.ApplyWrapWidth(width);
    }

    private void FitToContent() {
        ApplyWrapWidths();
        PerformLayout();
        Size preferred = shell.GetPreferredSize(new Size(ClientSize.Width, 0));
        int width = Math.Max(MinimumSize.Width, 700);
        int height = Math.Max(MinimumSize.Height, preferred.Height + 36);
        ClientSize = new Size(width, height);
    }

    private void OnStateChanged(object? sender, EventArgs e) {
        if (IsHandleCreated && InvokeRequired) {
            BeginInvoke(BindFromState);
            return;
        }

        BindFromState();
    }

    private void BindFromState() {
        syncing = true;
        statusBadge.ShowRunning(state.Enabled);
        eventValue.Text = StatusPresenter.EventLabel(state.LastEvent, state.LastEventDetail);
        pauseButton.Text = StatusPresenter.PauseActionLabel(state.Enabled);
        skipAllBox.Checked = state.SkipAllNonSecurityKeyOptions;
        autostartBox.Checked = state.AutostartOnLogon;
        logBox.Checked = state.FileLogEnabled;
        autoUpdateBox.Checked = state.AutoUpdateEnabled;
        pinBlock.BindFromState();
        syncing = false;
    }

    internal PinToggleDecision ApplyPinToggle() => pinBlock.ApplyPinToggle();

    internal void TurnOffPinAutosubmit() => pinBlock.TurnOffPinAutosubmit();

    private void Persist() {
        SettingsStore.EnsurePathAllowed(settingsPath, allowedRoot);
        SettingsStore.Save(settingsPath, state.ToSettings());
    }

    internal bool HideToTrayIfUserClosing(CloseReason reason) {
        if (forceClose || reason != CloseReason.UserClosing) {
            return false;
        }

        Hide();
        ShowInTaskbar = false;
        pinBlock.ClearPinSample();
        if (!state.TrayHintShown) {
            trayIcon.ShowRunningInTrayHint();
            state.TrayHintShown = true;
            Persist();
        }

        return true;
    }

    private static LinkLabel VersionLink() {
        LinkLabel version = new() {
            AutoSize = true,
            Text = AppCredits.VersionLine,
            Font = UiTheme.Caption,
            LinkColor = UiTheme.OnHeaderMuted,
            ActiveLinkColor = UiTheme.OnBrand,
            VisitedLinkColor = UiTheme.OnHeaderMuted,
            LinkBehavior = LinkBehavior.HoverUnderline,
            BackColor = Color.Transparent,
            Margin = new Padding(12, 8, 0, 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AccessibleName = "versionReleases"
        };
        version.LinkClicked += (_, _) => SafeWeb.OpenHttps(AppCredits.ReleasesUrl);
        return version;
    }

    private void ResetSettings() {
        DialogResult answer = MessageBox.Show(
            this,
            "Reset all options to their defaults? Autostart, PIN autosubmit, logging, and silent updates will return to factory values.",
            Text,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) {
            return;
        }

        if (!SettingsReset.TryApply(state, autostart, executablePath, settingsPath, allowedRoot)) {
            MessageBox.Show(this, "Settings were reset, but the logon scheduled task could not be updated.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e) {
        if (HideToTrayIfUserClosing(e.CloseReason)) {
            e.Cancel = true;
        }
    }

}
