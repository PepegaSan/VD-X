namespace VDX.Tests;

public class LTests
{
    [Fact]
    public void EnglishByDefaultGermanWhenChosen()
    {
        var before = L.Language;
        try
        {
            Assert.Equal(Language.English, new AppSettings().Language);
            L.Language = Language.English;
            Assert.Equal("Exit", L.T("Exit", "Beenden"));
            L.Language = Language.German;
            Assert.Equal("Beenden", L.T("Exit", "Beenden"));
        }
        finally
        {
            L.Language = before;
        }
    }
}