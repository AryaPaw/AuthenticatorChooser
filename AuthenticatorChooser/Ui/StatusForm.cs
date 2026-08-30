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
    private readonly IPinCache pinCache;
    private readonly bool ownsPinCache;
    private readonly List<Label> wrappingLabels = [];
    private readonly Icon windowIcon;
    private TableLayoutPanel shell = null!;
    private StatusBadge statusBadge = null!;
    private Label eventValue = null!;
    private CheckBox autostartBox = null!;
    private CheckBox logBox = null!;
    private CheckBox autoUpdateBox = null!;
    private StatusPinBlock pinBlock = null!;
    private Label prioritySummary = null!;
    private StatusFooter footer = null!;
    private ThemedButton pauseButton = null!;
    private ThemedButton fidoTab = null!;
    private ThemedButton computerTab = null!;
    private Panel fidoPage = null!;
    private Panel computerPage = null!;
    private bool forceClose;
    private bool syncing;
    private bool allowShow;

    public StatusForm(AppState state, IAutostartService autostart, string executablePath, string settingsPath, string allowedRoot, TrayIcon trayIcon, Action exit, IPinCache? pinCache = null) {
        this.state = state;
        this.autostart = autostart;
        this.executablePath = executablePath;
        this.settingsPath = settingsPath;
        this.allowedRoot = allowedRoot;
        this.trayIcon = trayIcon;
        this.exit = exit;
        ownsPinCache = pinCache is null;
        this.pinCache = pinCache ?? new PinCache();
        this.pinCache.Lifetime = state.PinCacheLifetime;
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
        MinimumSize = new Size(740, 880);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ShowInTaskbar = false;
        Padding = Padding.Empty;

        shell = new TableLayoutPanel {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = UiTheme.Surface,
            Padding = Padding.Empty
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        shell.Controls.Add(BuildHeader(), 0, 0);
        shell.Controls.Add(BuildBody(), 0, 1);
        footer = new StatusFooter();
        shell.Controls.Add(footer, 0, 2);
        Controls.Add(shell);

        Load += (_, _) => ApplyWrapWidths();
        Resize += (_, _) => ApplyWrapWidths();
        state.Changed += OnStateChanged;
        BindFromState();
        FormClosing += OnFormClosing;
        ClientSize = PreferredClientSize();
    }

    private static Size PreferredClientSize() {
        Rectangle work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        int width = Math.Min(800, Math.Max(720, work.Width - 72));
        int height = Math.Min(1040, Math.Max(860, work.Height - 72));
        return new Size(width, height);
    }

    public void Reveal() {
        allowShow = true;
        ShowInTaskbar = true;
        Icon = windowIcon;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        ApplyWrapWidths();
    }

    protected override void SetVisibleCore(bool value) {
        base.SetVisibleCore(allowShow && value);
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            state.Changed -= OnStateChanged;
            if (ownsPinCache) {
                pinCache.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private Panel BuildHeader() {
        TableLayoutPanel header = new() {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Brand950,
            Padding = new Padding(UiTheme.PagePad, 20, UiTheme.PagePad, 20),
            ColumnCount = 2,
            RowCount = 3
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        PictureBox icon = new() {
            Size = new Size(40, 40),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = windowIcon.ToBitmap(),
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 16, 0),
            Anchor = AnchorStyles.Left | AnchorStyles.Top
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
        subtitle.Margin = new Padding(0, 0, 0, 14);
        header.Controls.Add(subtitle, 1, 1);

        TableLayoutPanel statusRow = new() {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        FlowLayoutPanel statusLeft = new() {
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
        eventValue.Margin = new Padding(0, 6, 8, 0);
        statusLeft.Controls.Add(statusBadge);
        statusLeft.Controls.Add(eventValue);

        FlowLayoutPanel headerActions = new() {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Anchor = AnchorStyles.Right | AnchorStyles.Top
        };
        pauseButton = new ThemedButton("Pause", false) { AccessibleName = "pauseToggle", Margin = new Padding(0, 0, 8, 0) };
        ThemedButton quit = new("Exit", false) { Margin = Padding.Empty };
        pauseButton.Click += (_, _) => {
            StatusPresenter.ToggleEnabled(state);
            if (!state.Enabled) {
                pinCache.Clear();
            }
        };
        quit.Click += (_, _) => {
            forceClose = true;
            exit();
        };
        headerActions.Controls.AddRange([pauseButton, quit]);
        statusRow.Controls.Add(statusLeft, 0, 0);
        statusRow.Controls.Add(headerActions, 1, 0);
        header.Controls.Add(statusRow, 1, 2);
        return header;
    }

    private Control BuildBody() {
        TableLayoutPanel body = new() {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = UiTheme.Surface,
            Padding = Padding.Empty,
            AccessibleName = "statusTabs"
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        fidoTab = new ThemedButton("FIDO", true) { AccessibleName = "fidoTab" };
        computerTab = new ThemedButton("This computer", false) { AccessibleName = "computerTab" };
        fidoTab.Click += (_, _) => ShowTab(true);
        computerTab.Click += (_, _) => ShowTab(false);
        SegmentTrack tabs = new(fidoTab, computerTab) {
            Dock = DockStyle.Fill,
            Margin = new Padding(UiTheme.PagePad, 16, UiTheme.PagePad, 12)
        };

        Panel host = new() {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Surface,
            Padding = new Padding(UiTheme.PagePad, 0, UiTheme.PagePad, 8)
        };
        fidoPage = BuildFidoPage();
        computerPage = BuildComputerPage();
        computerPage.Visible = false;
        host.Controls.Add(computerPage);
        host.Controls.Add(fidoPage);

        body.Controls.Add(tabs, 0, 0);
        body.Controls.Add(host, 0, 1);
        return body;
    }

    private void ShowTab(bool fido) {
        fidoTab.Primary = fido;
        computerTab.Primary = !fido;
        fidoPage.Visible = fido;
        computerPage.Visible = !fido;
        if (fido) {
            fidoPage.BringToFront();
        } else {
            computerPage.BringToFront();
        }
    }

    private Panel BuildFidoPage() {
        Panel scroll = ScrollPage("fidoScroll");
        CardPanel fido = new() { Dock = DockStyle.Top, AutoSize = true };
        TableLayoutPanel fidoStack = Stack();
        Add(fidoStack, Heading("Authenticator priority"), 8);
        prioritySummary = Wrap(AuthenticatorPriorityCatalog.Summary(state.PriorityRules), UiTheme.BodyBold, UiTheme.Ink, UiTheme.Card);
        prioritySummary.AccessibleName = "prioritySummary";
        Add(fidoStack, prioritySummary, 6);
        Add(fidoStack, Wrap("Unknown authenticators stay on Ask and stop automatic clicks. Learned names are added only after they appear in a real FIDO prompt.", UiTheme.Caption, UiTheme.Muted, UiTheme.Card), 12);
        ThemedButton manage = new("Manage priorities", false) { AccessibleName = "managePriorities" };
        manage.Click += (_, _) => EditPriorities();
        Add(fidoStack, manage, 16);
        Panel rule = new() {
            Size = new Size(100, 1),
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 16),
            BackColor = UiTheme.Border
        };
        Add(fidoStack, rule, 0);
        Add(fidoStack, Heading("Security-key PIN"), 8);
        pinBlock = new StatusPinBlock(state, pinCache, Persist);
        Add(fidoStack, pinBlock, 0);
        fido.Controls.Add(fidoStack);
        scroll.Controls.Add(fido);
        return scroll;
    }

    private Panel BuildComputerPage() {
        Panel scroll = ScrollPage("computerScroll");
        CardPanel app = new() { Dock = DockStyle.Top, AutoSize = true };
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
        Add(appStack, Wrap("When a newer GitHub Release exists, the installer is downloaded and applied in the background. No notifications.", UiTheme.Caption, UiTheme.Muted, UiTheme.Card), 16);
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
        ThemedButton reset = new("Reset settings", false) { AccessibleName = "resetSettings" };
        reset.Click += (_, _) => ResetSettings();
        appButtons.Controls.AddRange([openLog, reset]);
        Add(appStack, appButtons, 0);
        app.Controls.Add(appStack);
        scroll.Controls.Add(app);
        return scroll;
    }

    private static Panel ScrollPage(string accessibleName) {
        Panel scroll = new() {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = UiTheme.Surface,
            Padding = Padding.Empty,
            AccessibleName = accessibleName
        };
        scroll.Layout += (_, _) => {
            scroll.HorizontalScroll.Maximum = 0;
            scroll.HorizontalScroll.Enabled = false;
            scroll.HorizontalScroll.Visible = false;
        };
        return scroll;
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
        int width = Math.Max(280, ClientSize.Width - (UiTheme.PagePad * 2) - 80);
        foreach (Label label in wrappingLabels) {
            label.MaximumSize = new Size(width, 0);
        }

        pinBlock.ApplyWrapWidth(width);
        footer.ApplyWrapWidth(width);
    }

    internal int FooterTop => footer.Top;

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
        autostartBox.Checked = state.AutostartOnLogon;
        logBox.Checked = state.FileLogEnabled;
        autoUpdateBox.Checked = state.AutoUpdateEnabled;
        prioritySummary.Text = AuthenticatorPriorityCatalog.Summary(state.PriorityRules);
        pinBlock.BindFromState();
        if (!state.Enabled) {
            pinCache.Clear();
        }
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
            Margin = new Padding(0, 2, 0, 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AccessibleName = "versionReleases"
        };
        version.LinkClicked += (_, _) => SafeWeb.OpenHttps(AppCredits.ReleasesUrl);
        return version;
    }

    private void ResetSettings() {
        DialogResult answer = MessageBox.Show(
            this,
            "Reset all options to their defaults? Autostart, PIN mode, priorities, logging, and silent updates will return to factory values.",
            Text,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) {
            return;
        }

        pinCache.Clear();
        if (!SettingsReset.TryApply(state, autostart, executablePath, settingsPath, allowedRoot)) {
            MessageBox.Show(this, "Settings were reset, but the logon scheduled task could not be updated.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void EditPriorities() {
        using AuthenticatorPriorityForm dialog = new(state.PriorityRules);
        if (dialog.ShowDialog(this) != DialogResult.OK) {
            return;
        }

        state.PriorityRules = dialog.Result;
        state.SkipAllNonSecurityKeyOptions = false;
        prioritySummary.Text = AuthenticatorPriorityCatalog.Summary(state.PriorityRules);
        Persist();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e) {
        if (HideToTrayIfUserClosing(e.CloseReason)) {
            e.Cancel = true;
        }
    }

}
