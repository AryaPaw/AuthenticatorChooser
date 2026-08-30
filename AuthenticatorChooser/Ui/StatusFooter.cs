using System.Drawing;
using System.Windows.Forms;

namespace AuthenticatorChooser.Ui;

internal sealed class StatusFooter: TableLayoutPanel {

    private readonly List<Label> wrappingLabels = [];

    public StatusFooter() {
        Dock = DockStyle.Fill;
        AutoSize = true;
        AccessibleName = "statusFooter";
        ColumnCount = 1;
        RowCount = 3;
        BackColor = UiTheme.Brand50;
        Padding = new Padding(UiTheme.PagePad, 16, UiTheme.PagePad, 18);
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        RowStyles.Add(new RowStyle(SizeType.AutoSize));
        RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Label about = Wrap(AppCredits.CopyrightLine);
        Label hint = Wrap("Hold Shift on a FIDO prompt to skip one automatic click. Close this window to keep the tray icon.");
        about.Margin = new Padding(0, 0, 0, 8);
        hint.Margin = new Padding(0, 0, 0, 8);

        FlowLayoutPanel links = new() {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        LinkLabel original = Link("Original source");
        LinkLabel fork = Link("This fork");
        fork.Margin = new Padding(16, 0, 0, 0);
        original.LinkClicked += (_, _) => SafeWeb.OpenHttps(AppCredits.OriginalRepositoryUrl);
        fork.LinkClicked += (_, _) => SafeWeb.OpenHttps(AppCredits.ForkRepositoryUrl);
        links.Controls.AddRange([original, fork]);

        Controls.Add(about, 0, 0);
        Controls.Add(hint, 0, 1);
        Controls.Add(links, 0, 2);
    }

    public void ApplyWrapWidth(int width) {
        foreach (Label label in wrappingLabels) {
            label.MaximumSize = new Size(width, 0);
        }
    }

    private Label Wrap(string text) {
        Label label = new() {
            AutoSize = true,
            Text = text,
            Font = UiTheme.Caption,
            ForeColor = UiTheme.Muted,
            BackColor = Color.Transparent,
            MaximumSize = new Size(600, 0),
            UseMnemonic = false
        };
        wrappingLabels.Add(label);
        return label;
    }

    private static LinkLabel Link(string text) =>
        new() {
            AutoSize = true,
            Text = text,
            Font = UiTheme.Caption,
            LinkColor = UiTheme.Brand800,
            ActiveLinkColor = UiTheme.Brand900,
            VisitedLinkColor = UiTheme.Brand800,
            LinkBehavior = LinkBehavior.HoverUnderline,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };

}
