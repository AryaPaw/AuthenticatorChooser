using AuthenticatorChooser.Ui;
using FluentAssertions;
using System.Windows.Forms;

namespace AuthenticatorChooser.Tests;

public sealed class StatusDialogsTests {

    [Fact]
    public void Show_UsesHandlerInsteadOfMessageBox() {
        DialogResult shown = StatusDialogs.Show(
            null,
            "portable copy",
            "AuthenticatorChooser",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1);
        shown.Should().Be(DialogResult.OK);
    }

    [Fact]
    public void Show_YesNo_DefaultsToNoInTests() {
        DialogResult shown = StatusDialogs.Show(
            null,
            "reset?",
            "AuthenticatorChooser",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        shown.Should().Be(DialogResult.No);
    }

}
