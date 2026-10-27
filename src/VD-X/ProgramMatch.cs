namespace VDX;

/// <summary>
/// Matches processes against a user entry that is either an exe name ("Diablo IV", "Diablo IV.exe" or a full exe path)
/// or an install folder (every exe below it counts, e.g. a game folder with its launcher and version subfolders).
/// </summary>
static class ProgramMatch
{
    public static bool IsFolder(string program) => program.Length > 0 && Directory.Exists(program);

    /// <summary>The entry in its canonical form: the full folder path, or "Name.exe".</summary>
    public static string Normalize(string program)
    {
        if (IsFolder(program))
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(program));
        string name = Path.GetFileName(program);
        return name.Length == 0 || Path.HasExtension(name) ? name : name + ".exe";
    }

    public static bool Matches(string program, string processPath)
    {
        if (program.Length == 0 || processPath.Length == 0)
            return false;
        string normalized = Normalize(program);
        if (IsFolder(program))
            return processPath.StartsWith(normalized + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        return string.Equals(Path.GetFileName(processPath), normalized, StringComparison.OrdinalIgnoreCase);
    }
}
