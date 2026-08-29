using System.Drawing;
using System.Windows.Forms;

namespace AuthenticatorChooser.Ui;

internal sealed class StatusPinBlock: TableLayoutPanel {

    private readonly AppState state;
    private readonly Action persist;
    private readonly List<Label> wrappingLabels = [];
    private readonly TextBox pinSample;
    private readonly Label pinLiveCount;
    private readonly Label pinSavedSummary;
    private readonly ThemedButton pinToggle;

    public StatusPinBlock(AppState state, Action persist) {
        this.state = state;
        this.persist = persist;
        Dock = DockStyle.Fill;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        ColumnCount = 1;
        RowCount = 0;
        GrowStyle = TableLayoutPanelGrowStyle.AddRows;
        BackColor = UiTheme.Card;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        pinSavedSummary = new Label {
            AutoSize = true,
            Font = UiTheme.BodyBold,
            ForeColor = UiTheme.Ink,
            BackColor = UiTheme.Card,
            MaximumSize = new Size(600, 0)
        };
        wrappingLabels.Add(pinSavedSummary);
        Add(pinSavedSummary, 8);

        pinSample = new TextBox {
            Width = 280,
            Font = UiTheme.Body,
            UseSystemPasswordChar = true,
            MaxLength = PinPolicy.MaxLength,
            AccessibleName = "pinSample",
            CausesValidation = false,
            Margin = Padding.Empty
        };
        pinSample.KeyDown += (_, e) => {
            if (e.KeyCode == Keys.Enter) {
                e.SuppressKeyPress = true;
                ApplyPinToggle();
            }
        };
        Add(pinSample, 6);

        pinLiveCount = new Label {
            AutoSize = true,
            Text = PinPolicy.LiveCountLabel(0),
            Font = UiTheme.Caption,
            ForeColor = UiTheme.Muted,
            BackColor = UiTheme.Card
        };
        pinSample.TextChanged += (_, _) => {
            if (!pinSample.Enabled) {
                return;
            }

            pinLiveCount.Text = PinPolicy.LiveCountLabel(pinSample.TextLength);
        };
        Add(pinLiveCount, 8);

        pinToggle = new ThemedButton("Turn on", true);
        pinToggle.AccessibleName = "pinToggle";
        pinToggle.Click += (_, _) => ApplyPinToggle();
        Add(pinToggle, 8);

        Label hint = new() {
            AutoSize = true,
            Text = "USB-key PIN only, not Windows Hello. Turn on keeps the count and forgets the PIN. Turn off disables autosubmit.",
            Font = UiTheme.Caption,
            ForeColor = UiTheme.Muted,
            BackColor = UiTheme.Card,
            MaximumSize = new Size(600, 0),
            UseMnemonic = false
        };
        wrappingLabels.Add(hint);
        Add(hint, 0);
    }

    public void ApplyWrapWidth(int width) {
        foreach (Label label in wrappingLabels) {
            label.MaximumSize = new Size(width, 0);
        }
    }

    public void BindFromState() {
        pinSavedSummary.Text = PinPolicy.SavedLengthSummary(state.AutoSubmitPinLength);
        ApplyPinModeView(PinModePolicy.View(state.AutoSubmitPinLength, pinLiveCount.Text));
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
        pinSample.Text = string.Empty;
    }

    private void ApplyPinModeView(PinModeView view) {
        pinToggle.Text = view.ButtonText;
        pinSavedSummary.Text = view.Summary;
        pinLiveCount.Text = view.Hint;
        pinSample.Enabled = view.FieldEnabled;
        if (!view.FieldEnabled) {
            ClearPinSample();
        }
    }

    private void Add(Control control, int bottom) {
        control.Margin = new Padding(0, 0, 0, bottom);
        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(control, 0, RowCount);
        RowCount++;
    }

}
