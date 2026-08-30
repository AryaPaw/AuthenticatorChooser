using System.Drawing;
using System.Windows.Forms;

namespace AuthenticatorChooser.Ui;

internal sealed class StatusPinBlock: TableLayoutPanel {

    private readonly AppState state;
    private readonly IPinCache pinCache;
    private readonly Action persist;
    private readonly List<Label> wrappingLabels = [];
    private RadioButton offMode = null!;
    private RadioButton lengthMode = null!;
    private RadioButton cacheMode = null!;
    private Panel lengthPanel = null!;
    private Panel cachePanel = null!;
    private TextBox pinSample = null!;
    private Label pinLiveCount = null!;
    private Label pinSavedSummary = null!;
    private ThemedButton pinToggle = null!;
    private ComboBox lifetimeBox = null!;
    private Label cacheStatus = null!;
    private ThemedButton cacheForget = null!;
    private readonly System.Windows.Forms.Timer countdown = new() { Interval = 1000 };
    private bool syncing;

    public StatusPinBlock(AppState state, IPinCache pinCache, Action persist) {
        this.state = state;
        this.pinCache = pinCache;
        this.persist = persist;
        Dock = DockStyle.Fill;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        ColumnCount = 1;
        RowCount = 0;
        GrowStyle = TableLayoutPanelGrowStyle.AddRows;
        BackColor = UiTheme.Card;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        Label intro = WrapLabel("Choose one PIN mode. Off leaves Windows Security alone, including the USB-key choice. The PIN is never written to disk.");
        Add(intro, 12);

        offMode = ModeRadio("Off", "pinModeOff");
        lengthMode = ModeRadio("Submit by length", "pinModeLength");
        cacheMode = ModeRadio("Remember PIN this session", "pinModeCache");
        Add(offMode, 6);
        Add(lengthMode, 6);
        Add(cacheMode, 12);

        lengthPanel = BuildLengthPanel();
        cachePanel = BuildCachePanel();
        Add(lengthPanel, 0);
        Add(cachePanel, 0);

        offMode.CheckedChanged += (_, _) => OnModeChecked(PinMode.Off, offMode);
        lengthMode.CheckedChanged += (_, _) => OnModeChecked(PinMode.Length, lengthMode);
        cacheMode.CheckedChanged += (_, _) => OnModeChecked(PinMode.Cache, cacheMode);
        countdown.Tick += (_, _) => RefreshCacheStatus();
        countdown.Start();
        BindFromState();
    }

    public void ApplyWrapWidth(int width) {
        foreach (Label label in wrappingLabels) {
            label.MaximumSize = new Size(width, 0);
        }
    }

    public void BindFromState() {
        syncing = true;
        offMode.Checked = state.PinMode == PinMode.Off;
        lengthMode.Checked = state.PinMode == PinMode.Length;
        cacheMode.Checked = state.PinMode == PinMode.Cache;
        pinCache.Lifetime = state.PinCacheLifetime;
        lifetimeBox.SelectedItem = PinCacheLifetimePolicy.Label(state.PinCacheLifetime);
        ApplyPinModeView(PinModePolicy.View(state.PinMode == PinMode.Length ? state.AutoSubmitPinLength : null, pinLiveCount.Text));
        ShowModePanels();
        RefreshCacheStatus();
        syncing = false;
    }

    public PinToggleDecision ApplyPinToggle() {
        PinToggleDecision decision = PinModePolicy.Press(state.AutoSubmitPinLength, pinSample.Enabled ? pinSample.Text : null);
        if (decision.Kind is PinToggleKind.TurnOn or PinToggleKind.TurnOff) {
            state.AutoSubmitPinLength = decision.LengthAfter;
            persist();
            ClearPinSample();
        } else if (decision.Kind == PinToggleKind.RejectedNeedLength) {
            ClearPinSample();
        }

        ApplyPinModeView(decision.View);
        return decision;
    }

    public void TurnOffPinAutosubmit() {
        if (!PinModePolicy.IsArmed(state.AutoSubmitPinLength)) {
            ApplyPinModeView(PinModePolicy.View(null));
            return;
        }

        ApplyPinToggle();
    }

    public void ClearPinSample() {
        pinSample.Clear();
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            countdown.Stop();
            countdown.Dispose();
        }

        base.Dispose(disposing);
    }

    private Panel BuildLengthPanel() {
        TableLayoutPanel stack = HiddenStack();
        pinSavedSummary = WrapLabel(PinPolicy.SavedLengthSummary(null), UiTheme.BodyBold, UiTheme.Ink);
        stack.Controls.Add(pinSavedSummary, 0, stack.RowCount++);
        stack.Controls.Add(FieldLabel("USB-key PIN", "pinSampleLabel"), 0, stack.RowCount++);
        pinSample = new TextBox {
            Width = 280,
            Height = 32,
            Font = UiTheme.Body,
            UseSystemPasswordChar = true,
            MaxLength = PinPolicy.MaxLength,
            AccessibleName = "pinSample",
            CausesValidation = false,
            Margin = new Padding(0, 0, 0, 6)
        };
        pinSample.KeyDown += (_, e) => {
            if (e.KeyCode == Keys.Enter) {
                e.SuppressKeyPress = true;
                ApplyPinToggle();
            }
        };
        stack.Controls.Add(pinSample, 0, stack.RowCount++);
        pinLiveCount = new Label {
            AutoSize = true,
            Text = PinPolicy.LiveCountLabel(0),
            Font = UiTheme.Caption,
            ForeColor = UiTheme.Muted,
            BackColor = UiTheme.Card,
            Margin = new Padding(0, 0, 0, 8)
        };
        pinSample.TextChanged += (_, _) => {
            if (!pinSample.Enabled) {
                return;
            }

            pinLiveCount.Text = PinPolicy.LiveCountLabel(pinSample.TextLength);
        };
        stack.Controls.Add(pinLiveCount, 0, stack.RowCount++);
        pinToggle = new ThemedButton("Turn on", true) { AccessibleName = "pinToggle", Margin = new Padding(0, 0, 0, 8) };
        pinToggle.Click += (_, _) => ApplyPinToggle();
        stack.Controls.Add(pinToggle, 0, stack.RowCount++);
        Label hint = WrapLabel("USB-key PIN only, not Windows Hello. Type it once so the length can be saved, then it is forgotten.");
        stack.Controls.Add(hint, 0, stack.RowCount++);
        return stack;
    }

    private Panel BuildCachePanel() {
        TableLayoutPanel stack = HiddenStack();
        Label confirmHint = WrapLabel("Type the USB-key PIN in the next Windows Security prompt and press Enter once. After that, later prompts in this session are filled for you. After a restart, type it again: OK is pressed when the length matches what you typed. The PIN is never written to disk. Forgotten at lock, sleep, pause, reset, or Exit.");
        stack.Controls.Add(confirmHint, 0, stack.RowCount++);
        stack.Controls.Add(FieldLabel("Keep cached PIN", "pinCacheLifetimeLabel"), 0, stack.RowCount++);
        lifetimeBox = new ComboBox {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = UiTheme.Body,
            Width = 280,
            AccessibleName = "pinCacheLifetime",
            Margin = new Padding(0, 0, 0, 10)
        };
        foreach (PinCacheLifetime lifetime in PinCacheLifetimePolicy.All) {
            lifetimeBox.Items.Add(PinCacheLifetimePolicy.Label(lifetime));
        }

        lifetimeBox.SelectedIndexChanged += (_, _) => {
            if (syncing || lifetimeBox.SelectedIndex < 0) {
                return;
            }

            state.PinCacheLifetime = PinCacheLifetimePolicy.All[lifetimeBox.SelectedIndex];
            pinCache.Lifetime = state.PinCacheLifetime;
            persist();
            RefreshCacheStatus();
        };
        stack.Controls.Add(lifetimeBox, 0, stack.RowCount++);
        cacheForget = new ThemedButton("Forget now", false) { AccessibleName = "pinCacheForget", Margin = new Padding(0, 0, 0, 8) };
        cacheForget.Click += (_, _) => {
            pinCache.Clear();
            RefreshCacheStatus();
        };
        stack.Controls.Add(cacheForget, 0, stack.RowCount++);
        cacheStatus = WrapLabel(PinCacheUxPolicy.WaitingStatus(null), UiTheme.BodyBold, UiTheme.Ink);
        cacheStatus.AccessibleName = "pinCacheStatus";
        stack.Controls.Add(cacheStatus, 0, stack.RowCount++);
        return stack;
    }

    private void OnModeChecked(PinMode mode, RadioButton box) {
        if (syncing || !box.Checked) {
            return;
        }

        pinCache.Clear();
        state.PinMode = mode;
        persist();
        BindFromState();
    }

    private void ShowModePanels() {
        lengthPanel.Visible = state.PinMode == PinMode.Length;
        cachePanel.Visible = state.PinMode == PinMode.Cache;
    }

    private void RefreshCacheStatus() {
        if (state.PinMode != PinMode.Cache) {
            cacheStatus.Text = PinCacheUxPolicy.WaitingStatus(state.LearnedPinLength);
            return;
        }

        cacheStatus.Text = CacheStatusText();
    }

    private string CacheStatusText() {
        if (!pinCache.HasCached) {
            return PinCacheUxPolicy.WaitingStatus(state.LearnedPinLength);
        }

        int? remaining = pinCache.RemainingSeconds;
        if (remaining is null) {
            return "PIN not cached";
        }

        if (state.PinCacheLifetime == PinCacheLifetime.UntilLockOrExit || remaining == int.MaxValue) {
            return "PIN cached until lock or exit";
        }

        return $"PIN cached - {remaining}s remaining";
    }

    private void ApplyPinModeView(PinModeView view) {
        pinToggle.Text = view.ButtonText;
        pinSavedSummary.Text = view.Summary;
        pinLiveCount.Text = view.Hint;
        pinSample.Enabled = view.FieldEnabled && state.PinMode == PinMode.Length;
        if (!pinSample.Enabled) {
            pinSample.Clear();
        }
    }

    private RadioButton ModeRadio(string text, string name) =>
        new() {
            AutoSize = true,
            Text = text,
            Font = UiTheme.Body,
            ForeColor = UiTheme.Ink,
            BackColor = UiTheme.Card,
            AccessibleName = name,
            Margin = Padding.Empty
        };

    private static Label FieldLabel(string text, string accessibleName) =>
        new() {
            AutoSize = true,
            Text = text,
            Font = UiTheme.BodyBold,
            ForeColor = UiTheme.Ink,
            BackColor = UiTheme.Card,
            Margin = new Padding(0, 2, 0, 4),
            UseMnemonic = false,
            AccessibleName = accessibleName
        };

    private Label WrapLabel(string text, Font? font = null, Color? color = null) {
        Label label = new() {
            AutoSize = true,
            Text = text,
            Font = font ?? UiTheme.Caption,
            ForeColor = color ?? UiTheme.Muted,
            BackColor = UiTheme.Card,
            MaximumSize = new Size(600, 0),
            UseMnemonic = false,
            Margin = new Padding(0, 0, 0, 8)
        };
        wrappingLabels.Add(label);
        return label;
    }

    private static TableLayoutPanel HiddenStack() {
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

    private void Add(Control control, int bottom) {
        control.Margin = new Padding(0, 0, 0, bottom);
        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(control, 0, RowCount);
        RowCount++;
    }

}
