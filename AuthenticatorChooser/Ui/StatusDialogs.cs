using System.Windows.Forms;

namespace AuthenticatorChooser.Ui;

internal static class StatusDialogs {

    internal static Func<IWin32Window?, string, string, MessageBoxButtons, MessageBoxIcon, MessageBoxDefaultButton, DialogResult>? Handler;

    public static DialogResult Show(
        IWin32Window? owner,
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton) {
        Func<IWin32Window?, string, string, MessageBoxButtons, MessageBoxIcon, MessageBoxDefaultButton, DialogResult>? handler = Handler;
        if (handler is not null) {
            return handler(owner, text, caption, buttons, icon, defaultButton);
        }

        return owner is null
            ? MessageBox.Show(text, caption, buttons, icon, defaultButton)
            : MessageBox.Show(owner, text, caption, buttons, icon, defaultButton);
    }

}
