using System.Drawing;
using System.Windows.Forms;

namespace AuthenticatorChooser.Ui;

internal sealed class AuthenticatorPriorityForm: Form {

    private readonly List<AuthenticatorPriorityRule> rules;
    private readonly ListBox list = new();
    private readonly ComboBox actionBox = new();
    private readonly TextBox nameBox = new();
    private readonly Label status = new();
    private bool applying;

    public AuthenticatorPriorityForm(IEnumerable<AuthenticatorPriorityRule> current) {
        rules = AuthenticatorPriorityCatalog.EnsureBuiltIns(current);
        Text = "Manage authenticator priorities";
        Font = UiTheme.Body;
        ForeColor = UiTheme.Ink;
        BackColor = UiTheme.Surface;
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(600, 540);
        ClientSize = new Size(640, 580);
        Padding = new Padding(UiTheme.PagePad);
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);

        TableLayoutPanel root = new() {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = UiTheme.Surface
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Label hint = new() {
            AutoSize = true,
            MaximumSize = new Size(580, 0),
            Text = "The list is checked from top to bottom. Select auto-clicks that option. Ask leaves the Windows prompt alone. Ignore skips it. Unknown names stay on Ask until you add them. Built-in rows cannot be renamed or removed.",
            Font = UiTheme.Caption,
            ForeColor = UiTheme.Muted,
            Margin = new Padding(0, 0, 0, 12)
        };

        CardPanel listCard = new() { Dock = DockStyle.Fill, Padding = new Padding(8) };
        list.Dock = DockStyle.Fill;
        list.BorderStyle = BorderStyle.None;
        list.IntegralHeight = false;
        list.AccessibleName = "priorityList";
        list.SelectedIndexChanged += (_, _) => BindSelected();
        listCard.Controls.Add(list);

        TableLayoutPanel editor = new() {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 4,
            Margin = new Padding(0, 12, 0, 12)
        };
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        actionBox.DropDownStyle = ComboBoxStyle.DropDownList;
        actionBox.AccessibleName = "priorityAction";
        actionBox.AccessibleDescription = "What to do when the selected row appears in Windows Security";
        actionBox.FlatStyle = FlatStyle.System;
        actionBox.Width = 180;
        actionBox.Items.AddRange(["Select", "Ask", "Ignore"]);
        actionBox.SelectedIndexChanged += (_, _) => ApplyAction();

        nameBox.Width = 280;
        nameBox.Height = UiTheme.ButtonHeight;
        nameBox.AccessibleName = "priorityName";
        nameBox.BorderStyle = BorderStyle.FixedSingle;
        nameBox.BackColor = UiTheme.Card;
        nameBox.ForeColor = UiTheme.Ink;
        nameBox.PlaceholderText = "Exact name from the Windows prompt";

        status.AutoSize = true;
        status.MaximumSize = new Size(580, 0);
        status.AccessibleName = "priorityStatus";
        status.Font = UiTheme.Caption;
        status.ForeColor = UiTheme.Muted;
        status.Margin = new Padding(0, 0, 0, 8);
        status.Text = "Add a custom name independently of the selected row. The dropdown only changes that row.";

        FlowLayoutPanel tools = new() {
            AutoSize = true,
            WrapContents = true,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        ThemedButton add = new("Add name", false) { AccessibleName = "priorityAdd" };
        ThemedButton remove = new("Remove", false) { AccessibleName = "priorityRemove" };
        ThemedButton up = new("Up", false) { AccessibleName = "priorityUp" };
        ThemedButton down = new("Down", false) { AccessibleName = "priorityDown" };
        ThemedButton restore = new("Restore defaults", false) { AccessibleName = "priorityRestore" };
        add.Click += (_, _) => AddName();
        remove.Click += (_, _) => RemoveSelected();
        up.Click += (_, _) => MoveSelected(-1);
        down.Click += (_, _) => MoveSelected(1);
        restore.Click += (_, _) => {
            rules.Clear();
            rules.AddRange(AuthenticatorPriorityCatalog.CreateDefaults().Select(rule => rule.Clone()));
            SetStatus("Restored the built-in rows.", false);
            RefreshList(0);
        };
        tools.Controls.AddRange([add, remove, up, down, restore]);
        editor.Controls.Add(LabeledRow("When this row appears", actionBox), 0, 0);
        editor.Controls.Add(LabeledRow("Custom name to add", nameBox), 0, 1);
        editor.Controls.Add(status, 0, 2);
        editor.Controls.Add(tools, 0, 3);

        FlowLayoutPanel buttons = new() {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            WrapContents = false,
            Margin = Padding.Empty
        };
        ThemedButton ok = new("OK", true) { DialogResult = DialogResult.OK, AccessibleName = "priorityOk", Margin = Padding.Empty };
        ThemedButton cancel = new("Cancel", false) { DialogResult = DialogResult.Cancel, Margin = new Padding(0, 0, 8, 0) };
        AcceptButton = ok;
        CancelButton = cancel;
        buttons.Controls.AddRange([ok, cancel]);

        root.Controls.Add(hint, 0, 0);
        root.Controls.Add(listCard, 0, 1);
        root.Controls.Add(editor, 0, 2);
        root.Controls.Add(buttons, 0, 3);
        Controls.Add(root);
        RefreshList(0);
    }

    public IReadOnlyList<AuthenticatorPriorityRule> Result => AuthenticatorPriorityCatalog.Clone(rules);

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
        if (keyData == Keys.Enter && nameBox.Focused) {
            AddName();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private static TableLayoutPanel LabeledRow(string caption, Control field) {
        TableLayoutPanel row = new() {
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 8)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        Label label = new() {
            AutoSize = true,
            Text = caption,
            Font = UiTheme.BodyBold,
            ForeColor = UiTheme.Ink,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 8, 0)
        };
        field.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        field.Margin = new Padding(0, 4, 0, 0);
        row.Controls.Add(label, 0, 0);
        row.Controls.Add(field, 1, 0);
        return row;
    }

    private void RefreshList(int selected) {
        list.Items.Clear();
        foreach (AuthenticatorPriorityRule rule in rules) {
            string kind = rule.BuiltIn ? "built-in" : "custom";
            list.Items.Add($"{rule.DisplayName} — {rule.Action} ({kind})");
        }

        if (list.Items.Count == 0) {
            return;
        }

        list.SelectedIndex = Math.Clamp(selected, 0, list.Items.Count - 1);
        BindSelected();
    }

    private void BindSelected() {
        applying = true;
        if (list.SelectedIndex is < 0 || list.SelectedIndex >= rules.Count) {
            applying = false;
            return;
        }

        AuthenticatorPriorityRule rule = rules[list.SelectedIndex];
        actionBox.SelectedItem = rule.Action.ToString();
        applying = false;
    }

    private void ApplyAction() {
        if (applying || list.SelectedIndex < 0 || actionBox.SelectedItem is not string label) {
            return;
        }

        if (!Enum.TryParse(label, out AuthenticatorRuleAction action)) {
            return;
        }

        rules[list.SelectedIndex].Action = action;
        RefreshList(list.SelectedIndex);
    }

    private void AddName() {
        PriorityNameAddStatus result = AuthenticatorPriorityCatalog.TryAddCustom(rules, nameBox.Text, out AuthenticatorPriorityRule? added);
        switch (result) {
            case PriorityNameAddStatus.Empty:
                SetStatus("Type the exact name shown in Windows Security, then click Add name.", true);
                return;
            case PriorityNameAddStatus.Duplicate:
                SetStatus("That name is already in the list.", true);
                return;
            case PriorityNameAddStatus.Added:
                nameBox.Clear();
                SetStatus($"Added {added!.DisplayName} as Ask.", false);
                RefreshList(rules.Count - 1);
                return;
            default:
                throw new InvalidOperationException($"Unhandled priority add status {result}");
        }
    }

    private void RemoveSelected() {
        if (list.SelectedIndex < 0) {
            return;
        }

        if (rules[list.SelectedIndex].BuiltIn) {
            SetStatus("Built-in rows cannot be removed.", true);
            return;
        }

        int index = list.SelectedIndex;
        rules.RemoveAt(index);
        SetStatus("Removed the custom name.", false);
        RefreshList(Math.Max(0, index - 1));
    }

    private void MoveSelected(int delta) {
        int index = list.SelectedIndex;
        int next = index + delta;
        if (index < 0 || next < 0 || next >= rules.Count) {
            return;
        }

        (rules[index], rules[next]) = (rules[next], rules[index]);
        RefreshList(next);
    }

    private void SetStatus(string text, bool error) {
        status.Text = text;
        status.ForeColor = error ? UiTheme.Warning : UiTheme.Muted;
    }

}
