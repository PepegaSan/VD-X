namespace VDX;

/// <summary>
/// Keeps the user on their desktop after one of a program's windows was moved away from it: programs usually activate
/// the moved window right away, which makes Windows switch to its desktop, and for a short time such a switch is undone.
/// A switch the user makes themselves (desktop hotkey) is never undone: it moves the guard to the chosen desktop, or
/// ends it when the user went to the program's desktop on purpose.
/// </summary>
sealed class StayGuard(Func<int> currentDesktop, Func<bool> movedWindowInFront, Action<int> goToDesktop)
{
    public static readonly TimeSpan Time = TimeSpan.FromSeconds(10);

    // The last desktop switch the user made with a hotkey, so a guard can tell it apart from the one Windows made.
    static int _userSwitches;
    static int _userDesktop;

    int _stayOn = -1;   // desktop to take the user back to; -1 = not armed
    int _target;
    int _seenUserSwitches;
    DateTime _until;

    public bool IsArmed => _stayOn >= 0;

    /// <summary>Called for every desktop switch the user makes with a hotkey.</summary>
    public static void NoteUserSwitch(int desktop)
    {
        _userDesktop = desktop;
        _userSwitches++;
    }

    /// <summary>A window was just moved from <paramref name="stayOn"/>, the user's desktop, to <paramref name="target"/>.</summary>
    public void Arm(int stayOn, int target)
    {
        _stayOn = stayOn;
        _target = target;
        _seenUserSwitches = _userSwitches;
        _until = DateTime.UtcNow + Time;
    }

    /// <summary>Call regularly while armed; undoes the switch Windows made for the program.</summary>
    public void Check()
    {
        if (_stayOn < 0)
            return;
        if (DateTime.UtcNow > _until)
        {
            _stayOn = -1;
            return;
        }
        if (_seenUserSwitches != _userSwitches)
        {
            _seenUserSwitches = _userSwitches;
            if (_userDesktop == _target)
            {
                _stayOn = -1;   // they went to the program's desktop on purpose
                return;
            }
            _stayOn = _userDesktop;
        }
        if (currentDesktop() == _target && movedWindowInFront())
            goToDesktop(_stayOn);
    }
}