using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AuthenticatorChooser.Ui;

internal static class UiDrawing {

    public static GraphicsPath RoundedRect(Rectangle bounds, int radius) {
        GraphicsPath path = new();
        int diameter = Math.Max(2, radius * 2);
        if (bounds.Width <= diameter || bounds.Height <= diameter) {
            path.AddRectangle(bounds);
            return path;
        }

        Rectangle arc = new(bounds.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

}

internal sealed class CardPanel: Panel {

    public CardPanel() {
        DoubleBuffered = true;
        BackColor = UiTheme.Card;
        Padding = new Padding(20, 18, 20, 18);
        Margin = new Padding(0, 0, 0, 12);
    }

    protected override void OnResize(EventArgs e) {
        base.OnResize(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle box = new(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        using GraphicsPath path = UiDrawing.RoundedRect(box, UiTheme.Radius);
        using SolidBrush fill = new(UiTheme.Card);
        using Pen border = new(UiTheme.Border);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
        base.OnPaint(e);
    }

}

internal sealed class ThemedButton: Button {

    public bool Primary { get; }

    public ThemedButton(string text, bool primary) {
        Primary = primary;
        Text = text;
        Font = UiTheme.Button;
        FlatStyle = FlatStyle.Flat;
        Size = new Size(148, 40);
        MinimumSize = new Size(148, 40);
        MaximumSize = new Size(148, 40);
        Margin = new Padding(0, 0, 12, 0);
        Padding = Padding.Empty;
        TextAlign = ContentAlignment.MiddleCenter;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
        UseCompatibleTextRendering = true;
        CausesValidation = false;
        FlatAppearance.BorderSize = 1;
        FlatAppearance.MouseOverBackColor = primary ? UiTheme.Brand900 : UiTheme.Brand50;
        FlatAppearance.MouseDownBackColor = primary ? UiTheme.Brand950 : UiTheme.Border;
        ApplyPalette();
    }

    private void ApplyPalette() {
        if (Primary) {
            BackColor = UiTheme.Brand800;
            ForeColor = UiTheme.OnBrand;
            FlatAppearance.BorderColor = UiTheme.Brand800;
            return;
        }

        BackColor = UiTheme.Card;
        ForeColor = UiTheme.Ink;
        FlatAppearance.BorderColor = UiTheme.Border;
    }

}

internal sealed class StatusBadge: Label {

    public StatusBadge() {
        AutoSize = true;
        Padding = new Padding(10, 4, 10, 4);
        TextAlign = ContentAlignment.MiddleCenter;
        Font = UiTheme.Caption;
        ForeColor = UiTheme.OnBrand;
        Margin = new Padding(0, 0, 12, 0);
    }

    public void ShowRunning(bool running) {
        Text = running ? "Running" : "Paused";
        BackColor = running ? UiTheme.Success : UiTheme.Warning;
    }

}
