using AuthenticatorChooser.Windows11;
using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class StrategyTitleTests {

    [Fact]
    public void Win1123H2_HandlesSignInTitle() {
        ChooserOptions options = new(new AppState());
        Win1123H2Strategy strategy = new(options);
        strategy.canHandleTitle(I18N.getStrings(I18N.Key.SIGN_IN_WITH_YOUR_PASSKEY).First()).Should().BeTrue();
        strategy.canHandleTitle("Not a FIDO title").Should().BeFalse();
    }

    [Fact]
    public void Win1125H2_HandlesChoosePasskeyTitle() {
        ChooserOptions options = new(new AppState());
        Win1125H2Strategy strategy = new(options);
        strategy.canHandleTitle(I18N.getStrings(I18N.Key.CHOOSE_A_PASSKEY).First()).Should().BeTrue();
        strategy.canHandleTitle("Not a FIDO title").Should().BeFalse();
    }

    [Fact]
    public void Win1123H2_HandlesMakingSureWhenSkipAll() {
        AppState state = new();
        state.SkipAllNonSecurityKeyOptions = true;
        Win1123H2Strategy strategy = new(new ChooserOptions(state));
        strategy.canHandleTitle(I18N.getStrings(I18N.Key.MAKING_SURE_ITS_YOU).First()).Should().BeTrue();
    }

}
