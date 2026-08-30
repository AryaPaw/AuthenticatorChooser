using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AuthenticatorChooser.Ui;

internal enum ButtonTone {
    Primary,
    Secondary
}

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

    public static void PaintRounded(Graphics graphics, Rectangle bounds, int radius, Color fill, Color border) {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        if (bounds.Width <= 0 || bounds.Height <= 0) {
            return;
        }

        using GraphicsPath path = RoundedRect(bounds, radius);
        using SolidBrush brush = new(fill);
        using Pen pen = new(border);
        graphics.FillPath(brush, path);
        graphics.DrawPath(pen, path);
    }

}

internal sealed class CardPanel: Panel {

    public CardPanel() {
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Padding = new Padding(20);
        Margin = Padding.Empty;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e) {
        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Surface);
        Rectangle box = new(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        UiDrawing.PaintRounded(e.Graphics, box, UiTheme.CardRadius, UiTheme.Card, UiTheme.Border);
    }

}

internal sealed class SegmentTrack: Panel {

    public SegmentTrack(ThemedButton first, ThemedButton second) {
        DoubleBuffered = true;
        Height = UiTheme.TabHeight + (UiTheme.SegmentInset * 2);
        MinimumSize = new Size(0, Height);
        MaximumSize = new Size(0, Height);
        Padding = new Padding(UiTheme.SegmentInset);
        BackColor = Color.Transparent;
        Margin = Padding.Empty;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        TableLayoutPanel grid = new() {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        first.Stretch = true;
        second.Stretch = true;
        first.Margin = new Padding(0, 0, 2, 0);
        second.Margin = new Padding(2, 0, 0, 0);
        first.Dock = DockStyle.Fill;
        second.Dock = DockStyle.Fill;
        grid.Controls.Add(first, 0, 0);
        grid.Controls.Add(second, 1, 0);
        Controls.Add(grid);
    }

    protected override void OnPaintBackground(PaintEventArgs e) {
        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Surface);
        Rectangle box = new(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        UiDrawing.PaintRounded(e.Graphics, box, UiTheme.TrackRadius, UiTheme.Track, UiTheme.Border);
    }

}

internal sealed class ThemedButton: Button {

    private ButtonTone tone;
    private bool stretch;

    public ThemedButton(string text, bool primary, int minWidth = 112): this(text, primary ? ButtonTone.Primary : ButtonTone.Secondary, minWidth) {
    }

    public ThemedButton(string text, ButtonTone tone, int minWidth = 112) {
        this.tone = tone;
        Text = text;
        Font = UiTheme.Button;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Margin = new Padding(0, 0, 8, 0);
        Padding = new Padding(16, 0, 16, 0);
        TextAlign = ContentAlignment.MiddleCenter;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
        UseCompatibleTextRendering = false;
        CausesValidation = false;
        TabStop = true;
        MinContentWidth = minWidth;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        UpdateStyles();
        ApplyPalette();
        FitToText();
    }

    private int MinContentWidth { get; }

    public bool Stretch {
        get => stretch;
        set {
            stretch = value;
            Margin = value ? Padding.Empty : new Padding(0, 0, 8, 0);
            FitToText();
        }
    }

    public bool Primary {
        get => tone == ButtonTone.Primary;
        set {
            ButtonTone next = value ? ButtonTone.Primary : ButtonTone.Secondary;
            if (tone == next) {
                return;
            }

            tone = next;
            ApplyPalette();
            Invalidate();
        }
    }

    protected override void OnTextChanged(EventArgs e) {
        base.OnTextChanged(e);
        FitToText();
    }

    protected override void OnMouseEnter(EventArgs e) {
        base.OnMouseEnter(e);
        BackColor = HoverFill();
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e) {
        base.OnMouseLeave(e);
        ApplyPalette();
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        BackColor = PressFill();
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e) {
        base.OnMouseUp(e);
        BackColor = ClientRectangle.Contains(PointToClient(MousePosition)) ? HoverFill() : RestFill();
        if (!ClientRectangle.Contains(PointToClient(MousePosition))) {
            ApplyPalette();
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(PaintBehind());
        Rectangle box = new(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        UiDrawing.PaintRounded(e.Graphics, box, UiTheme.ButtonRadius, BackColor, FlatAppearance.BorderColor);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            ClientRectangle,
            ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private Color PaintBehind() {
        if (stretch) {
            return UiTheme.Track;
        }

        Control? current = Parent;
        while (current is not null) {
            if (current.BackColor.A == 255) {
                return current.BackColor;
            }

            current = current.Parent;
        }

        return UiTheme.Surface;
    }

    private void FitToText() {
        Size textSize = TextRenderer.MeasureText(Text, Font);
        int chromeHeight = stretch ? UiTheme.TabHeight : UiTheme.ButtonHeight;
        int width = Math.Max(MinContentWidth, textSize.Width + 32);
        if (stretch) {
            MinimumSize = new Size(0, chromeHeight);
            MaximumSize = Size.Empty;
            Height = chromeHeight;
            return;
        }

        Size fitted = new(width, chromeHeight);
        MinimumSize = fitted;
        MaximumSize = fitted;
        Size = fitted;
    }

    private void ApplyPalette() {
        BackColor = RestFill();
        ForeColor = tone == ButtonTone.Secondary ? UiTheme.Ink : UiTheme.OnBrand;
        FlatAppearance.BorderColor = BorderFill();
    }

    private Color RestFill() =>
        tone switch {
            ButtonTone.Primary => UiTheme.Brand800,
            ButtonTone.Secondary => UiTheme.Card,
            _ => throw Exhaustive(tone)
        };

    private Color HoverFill() =>
        tone switch {
            ButtonTone.Primary => UiTheme.Brand900,
            ButtonTone.Secondary => UiTheme.Brand50,
            _ => throw Exhaustive(tone)
        };

    private Color PressFill() =>
        tone switch {
            ButtonTone.Primary => UiTheme.Brand950,
            ButtonTone.Secondary => UiTheme.Track,
            _ => throw Exhaustive(tone)
        };

    private Color BorderFill() =>
        tone switch {
            ButtonTone.Primary => UiTheme.Brand800,
            ButtonTone.Secondary => UiTheme.Border,
            _ => throw Exhaustive(tone)
        };

    private static Exception Exhaustive(ButtonTone tone) =>
        new InvalidOperationException($"Unhandled button tone {tone}");

}

internal sealed class StatusBadge: Label {

    public StatusBadge() {
        AutoSize = true;
        Padding = new Padding(10, 4, 10, 4);
        TextAlign = ContentAlignment.MiddleCenter;
        Font = UiTheme.Caption;
        ForeColor = UiTheme.OnBrand;
        Margin = new Padding(0, 0, 12, 0);
        UseMnemonic = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(UiTheme.Brand950);
        Rectangle box = new(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        UiDrawing.PaintRounded(e.Graphics, box, Math.Max(8, Height / 2), BackColor, BackColor);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            ClientRectangle,
            ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    public void ShowRunning(bool running) {
        Text = running ? "Running" : "Paused";
        BackColor = running ? UiTheme.Success : UiTheme.Warning;
        Invalidate();
    }

}
