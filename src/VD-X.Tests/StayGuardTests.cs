namespace VDX.Tests;

/// <summary>
/// After a started program's window was moved away from the user's desktop, the program activates it and Windows
/// switches to its desktop; the guard takes the user back. Bug: for ten seconds after such a move it also undid the
/// switches the user made with a hotkey, so "next desktop" landed on the wrong desktop or seemed to do nothing.
/// </summary>
public class StayGuardTests
{
    const int Desktop1 = 0, Desktop2 = 1, Desktop3 = 2;

    int _current = Desktop1;
    bool _programInFront;
    readonly List<int> _guardSwitches = [];

    StayGuard NewGuard() => new(() => _current, () => _programInFront, desktop =>
    {
        _guardSwitches.Add(desktop);
        _current = desktop;
    });

    /// <summary>What a desktop hotkey does (DesktopActions.GoTo).</summary>
    void UserPressesHotkeyTo(int desktop)
    {
        _current = desktop;
        StayGuard.NoteUserSwitch(desktop);
    }

    /// <summary>The program activates its moved window and Windows follows it there.</summary>
    void ProgramPullsUserTo(int desktop)
    {
        _current = desktop;
        _programInFront = true;
    }

    [Fact]
    public void TakesTheUserBackWhenTheProgramPullsThemToItsDesktop()
    {
        var guard = NewGuard();
        guard.Arm(stayOn: Desktop1, target: Desktop3);   // a game window was moved from desktop 1 to desktop 3

        ProgramPullsUserTo(Desktop3);
        guard.Check();

        Assert.Equal(new[] { Desktop1 }, _guardSwitches);
    }

    [Fact]
    public void LeavesTheUserOnTheProgramsDesktopWhenTheyWentThereWithAHotkey()
    {
        var guard = NewGuard();
        guard.Arm(stayOn: Desktop1, target: Desktop3);

        UserPressesHotkeyTo(Desktop2);   // "next desktop" twice: 1 -> 2 -> 3, where the game is in front
        guard.Check();
        UserPressesHotkeyTo(Desktop3);
        _programInFront = true;
        guard.Check();

        Assert.Empty(_guardSwitches);
        Assert.False(guard.IsArmed);
    }

    [Fact]
    public void TakesTheUserBackToTheDesktopTheyChoseWithAHotkey()
    {
        var guard = NewGuard();
        guard.Arm(stayOn: Desktop1, target: Desktop3);

        UserPressesHotkeyTo(Desktop2);
        guard.Check();
        ProgramPullsUserTo(Desktop3);
        guard.Check();

        Assert.Equal(new[] { Desktop2 }, _guardSwitches);
    }
}