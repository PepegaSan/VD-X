namespace VDX;

enum Language
{
    English,
    German,
}

/// <summary>Tiny two-language helper: English unless German is chosen in the settings.</summary>
static class L
{
    public static Language Language { get; set; } = Language.English;

    public static string T(string english, string german) => Language == Language.German ? german : english;
}
