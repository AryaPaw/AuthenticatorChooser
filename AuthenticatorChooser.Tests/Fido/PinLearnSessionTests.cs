using FluentAssertions;

namespace AuthenticatorChooser.Tests;

public sealed class PinLearnSessionTests {

    [Fact]
    public void WindowClosedAfterTyping_CommitsOnce() {
        PinLearnSession session = new();
        foreach (char c in "13579") {
            session.OnCharacter(c);
        }

        session.TakeCommitOnWindowClosed().Should().Be("13579");
        session.TakeCommitOnWindowClosed().Should().BeNull();
    }

    [Fact]
    public void EnterThenClosed_UsesCandidateNotLaterKeys() {
        PinLearnSession session = new();
        foreach (char c in "2468") {
            session.OnCharacter(c);
        }

        session.OnEnter();
        session.OnCharacter('9');
        session.TakeCommitOnWindowClosed().Should().Be("2468");
    }

    [Fact]
    public void FieldCleared_DiscardsWrongAttempt() {
        PinLearnSession session = new();
        foreach (char c in "1111") {
            session.OnCharacter(c);
        }

        session.OnEnter();
        session.OnFieldCleared();
        foreach (char c in "2468") {
            session.OnCharacter(c);
        }

        session.TakeCommitOnWindowClosed().Should().Be("2468");
    }

    [Fact]
    public void BackspaceAndShortPin_DoNotCommit() {
        PinLearnSession session = new();
        session.OnCharacter('1');
        session.OnCharacter('2');
        session.OnCharacter('3');
        session.OnBackspace();
        session.TakeCommitOnWindowClosed().Should().BeNull();

        PinLearnSession complete = new();
        complete.OnCharacter('1');
        complete.OnCharacter('2');
        complete.OnCharacter('3');
        complete.OnBackspace();
        complete.OnCharacter('3');
        complete.OnCharacter('4');
        complete.TakeCommitOnWindowClosed().Should().Be("1234");
    }

    [Fact]
    public void ControlCharacters_AreIgnored() {
        PinLearnSession session = new();
        session.OnCharacter('\u0001');
        foreach (char c in "2468") {
            session.OnCharacter(c);
        }

        session.TakeCommitOnWindowClosed().Should().Be("2468");
    }

    [Fact]
    public void CanCommit_MatchesTakeCommitWithoutConsuming() {
        PinLearnSession empty = new();
        empty.CanCommit().Should().BeFalse();

        PinLearnSession shortPin = new();
        shortPin.OnCharacter('1');
        shortPin.OnCharacter('2');
        shortPin.OnCharacter('3');
        shortPin.CanCommit().Should().BeFalse();
        shortPin.TakeCommitOnWindowClosed().Should().BeNull();

        PinLearnSession live = new();
        foreach (char c in "2468") {
            live.OnCharacter(c);
        }

        live.CanCommit().Should().BeTrue();
        live.TakeCommitOnWindowClosed().Should().Be("2468");
    }

    [Fact]
    public void FieldEmptied_FreezesTypedPinInsteadOfDiscarding() {
        PinLearnSession session = new();
        foreach (char c in "2468") {
            session.OnCharacter(c);
        }

        session.OnFieldEmptied();
        session.CapturedLength.Should().Be(4);
        session.TakeCommitOnWindowClosed().Should().Be("2468");
    }

    [Fact]
    public void FieldEmptied_DoesNotWipeIncompleteTypedPin() {
        PinLearnSession session = new();
        session.OnCharacter('1');
        session.OnCharacter('2');
        session.OnFieldEmptied();
        session.OnCharacter('3');
        session.OnCharacter('4');
        session.TakeCommitOnWindowClosed().Should().Be("1234");
    }

}
