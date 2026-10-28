namespace VDX.Tests;

public class ProgramLauncherTests
{
    [Fact]
    public async Task SkipsProgramsSwitchedOff()
    {
        // Desktop 0 is "Off" in the list: the entry stays, but nothing is started.
        // Starting this path would fail and come back as an error message.
        var errors = await ProgramLauncher.RunAllAsync([new StartupProgram { Desktop = 0, Path = @"C:\does\not\exist\game.exe" }]);

        Assert.Empty(errors);
    }

    [Fact]
    public void AnEntryWithoutADesktopIsOff()
    {
        Assert.False(new StartupProgram { Desktop = 0 }.Enabled);
        Assert.True(new StartupProgram { Desktop = 3 }.Enabled);
    }
}