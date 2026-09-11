using System.Runtime.CompilerServices;
using System.Windows.Forms;
using AuthenticatorChooser.Ui;

namespace AuthenticatorChooser.Tests;

internal static class StatusDialogTestInit {

    [ModuleInitializer]
    internal static void SuppressModalDialogs() {
        StatusDialogs.Handler = (_, _, _, buttons, _, _) =>
            buttons is MessageBoxButtons.YesNo or MessageBoxButtons.YesNoCancel
                ? DialogResult.No
                : DialogResult.OK;
    }

}
