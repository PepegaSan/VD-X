using System.Globalization;

namespace VDX;

/// <summary>Tiny two-language helper: German UI on German Windows, English everywhere else.</summary>
static class L
{
    static readonly bool German = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";

    public static string T(string english, string german) => German ? german : english;
}
