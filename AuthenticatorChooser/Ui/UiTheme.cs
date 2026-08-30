using System.Drawing;

namespace AuthenticatorChooser.Ui;

internal static class UiTheme {

    public static readonly Color Brand950 = Color.FromArgb(11, 46, 84);
    public static readonly Color Brand900 = Color.FromArgb(14, 78, 140);
    public static readonly Color Brand800 = Color.FromArgb(14, 124, 194);
    public static readonly Color Brand50 = Color.FromArgb(240, 248, 255);
    public static readonly Color Surface = Color.FromArgb(244, 246, 250);
    public static readonly Color Track = Color.FromArgb(226, 236, 247);
    public static readonly Color Card = Color.White;
    public static readonly Color Ink = Color.FromArgb(22, 32, 48);
    public static readonly Color Muted = Color.FromArgb(84, 98, 118);
    public static readonly Color Border = Color.FromArgb(206, 218, 232);
    public static readonly Color Success = Color.FromArgb(46, 184, 119);
    public static readonly Color Warning = Color.FromArgb(212, 141, 17);
    public static readonly Color OnBrand = Color.White;
    public static readonly Color OnHeaderMuted = Color.FromArgb(186, 220, 246);

    public const int CardRadius = 12;
    public const int ButtonRadius = 8;
    public const int TrackRadius = 12;
    public const int SegmentInset = 4;
    public const int ButtonHeight = 36;
    public const int TabHeight = 52;
    public const int PagePad = 24;
    public const int StackGap = 8;

    public static readonly Font Title = new("Segoe UI", 18F, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font Section = new("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font Body = new("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font BodyBold = new("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font Caption = new("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font Button = new("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point);

}
