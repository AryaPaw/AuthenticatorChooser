using System.Drawing;
using System.Windows.Forms;

namespace AuthenticatorChooser.Ui;

internal sealed class AuthenticatorPriorityForm: Form {

    private readonly List<AuthenticatorPriorityRule> rules;
    private readonly ListBox list = new();
    private readonly ComboBox actionBox = new();
    private readonly TextBox nameBox = new();
    private bool applying;

    public AuthenticatorPriorityForm(IEnumerable<AuthenticatorPriorityRule> current) {
        rules = AuthenticatorPriorityCatalog.EnsureBuiltIns(current);
        Text = "Manage authenticator priorities";
        Font = UiTheme.Body;
        ForeColor = UiTheme.Ink;
        BackColor = UiTheme.Surface;
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(560, 480);
        ClientSize = new Size(600, 520);
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
            MaximumSize = new Size(540, 0),
            Text = "First matching non-Ignore rule wins. Unknown names stop automatic selection (Ask). Built-in rows cannot be renamed or removed.",
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
            RowCount = 2,
            Margin = new Padding(0, 12, 0, 12)
        };
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        FlowLayoutPanel fields = new() {
            AutoSize = true,
            WrapContents = true,
            Padding = Padding.Empty,
            Margin = new Padding(0, 0, 0, 8)
        };
        actionBox.DropDownStyle = ComboBoxStyle.DropDownList;
        actionBox.AccessibleName = "priorityAction";
        actionBox.FlatStyle = FlatStyle.System;
        actionBox.Width = 120;
        actionBox.Margin = new Padding(0, 0, 8, 0);
        actionBox.Items.AddRange(["Select", "Ask", "Ignore"]);
        actionBox.SelectedIndexChanged += (_, _) => ApplyAction();
        nameBox.Width = 200;
        nameBox.Height = UiTheme.ButtonHeight;
        nameBox.AccessibleName = "priorityName";
        nameBox.Margin = new Padding(0, 0, 8, 0);
        fields.Controls.AddRange([actionBox, nameBox]);

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
            RefreshList(0);
        };
        tools.Controls.AddRange([add, remove, up, down, restore]);
        editor.Controls.Add(fields, 0, 0);
        editor.Controls.Add(tools, 0, 1);

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

    private void RefreshList(int selected) {
        list.Items.Clear();
        foreach (AuthenticatorPriorityRule rule in rules) {
            string kind = rule.BuiltIn ? "built-in" : "learned";
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
        nameBox.Enabled = !rule.BuiltIn;
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
        string name = nameBox.Text.Trim();
        if (name.Length == 0) {
            return;
        }

        if (rules.Any(rule => string.Equals(rule.DisplayName, name, StringComparison.OrdinalIgnoreCase))) {
            return;
        }

        rules.Add(new AuthenticatorPriorityRule {
            Id = "custom:" + Guid.NewGuid().ToString("N"),
            Kind = AuthenticatorKind.External,
            DisplayName = name,
            Action = AuthenticatorRuleAction.Ask,
            BuiltIn = false
        });
        nameBox.Clear();
        RefreshList(rules.Count - 1);
    }

    private void RemoveSelected() {
        if (list.SelectedIndex < 0 || rules[list.SelectedIndex].BuiltIn) {
            return;
        }

        int index = list.SelectedIndex;
        rules.RemoveAt(index);
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

}
