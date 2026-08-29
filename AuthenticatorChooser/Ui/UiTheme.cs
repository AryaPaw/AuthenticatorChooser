using System.Drawing;

namespace AuthenticatorChooser;

internal static class UiTheme {

    public static readonly Color Brand950 = Color.FromArgb(7, 59, 55);
    public static readonly Color Brand900 = Color.FromArgb(12, 90, 84);
    public static readonly Color Brand800 = Color.FromArgb(17, 136, 127);
    public static readonly Color Brand50 = Color.FromArgb(241, 253, 252);
    public static readonly Color Surface = Color.FromArgb(246, 247, 248);
    public static readonly Color Card = Color.White;
    public static readonly Color Ink = Color.FromArgb(28, 31, 38);
    public static readonly Color Muted = Color.FromArgb(88, 96, 116);
    public static readonly Color Border = Color.FromArgb(214, 217, 224);
    public static readonly Color Success = Color.FromArgb(46, 184, 119);
    public static readonly Color Warning = Color.FromArgb(212, 141, 17);
    public static readonly Color OnBrand = Color.White;
    public static readonly Color OnHeaderMuted = Color.FromArgb(196, 230, 226);

    public const int Radius = 10;
    public const int PagePad = 24;

    public static readonly Font Title = new("Segoe UI", 18F, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font Section = new("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font Body = new("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font BodyBold = new("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font Caption = new("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font Button = new("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);

}
