using System.ComponentModel;
using System.Diagnostics;

namespace VDX;

/// <summary>Starts programs and moves their windows to a fixed desktop.</summary>
static class ProgramLauncher
{
    // Big programs can need a minute or more on a cold start before anything shows up (DaVinci Resolve: about 60 s).
    static readonly TimeSpan FirstWindowTimeout = TimeSpan.FromMinutes(3);
    // Splash screens and launchers come first and the main window later, so new windows keep being moved
    // for this long after the first one…
    static readonly TimeSpan WatchTime = TimeSpan.FromMinutes(3);
    // …unless the program is already gone and nothing new has appeared for a while.
    static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(10);
    // Programs often activate a window right after it was moved, which makes Windows switch to that desktop;
    // for this long after a move such a switch is undone so the user stays where they were.
    static readonly TimeSpan StayGuardTime = TimeSpan.FromSeconds(10);
    static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    static readonly List<Launch> Launching = [];

    /// <summary>True while windows of this process are expected from a launch; <see cref="NewWindowRouter"/> leaves them alone.</summary>
    public static bool IsLaunching(string processPath) => Launching.Any(launch => launch.Owns(processPath));

    /// <summary>Starts all programs in parallel. Returns one message per program that failed. Call on the UI thread.</summary>
    public static async Task<List<string>> RunAllAsync(IEnumerable<StartupProgram> programs)
    {
        var errors = new List<string>();
        await Task.WhenAll(programs.Where(p => p.Path.Trim().Length > 0).Select(async program =>
        {
            try
            {
                await RunAsync(program);
            }
            catch (Exception ex) when (ex is DesktopActionException or Win32Exception or InvalidOperationException)
            {
                errors.Add($"{Path.GetFileName(CleanPath(program.Path))}: {ex.Message}");
            }
        }));
        return errors;
    }

    /// <summary>Starts the program and returns once its first window is on the target desktop; later windows follow in the background.</summary>
    static async Task RunAsync(StartupProgram program)
    {
        int target = Math.Max(program.Desktop, 1) - 1;
        DesktopActions.EnsureDesktopExists(target);

        var launch = new Launch(CleanPath(program.Path), CleanPath(program.Window), target);
        Launching.Add(launch);
        try
        {
            launch.Start(program.Arguments);
            var started = DateTime.UtcNow;
            while (!launch.MoveNewWindows())
            {
                if (DateTime.UtcNow - started >= FirstWindowTimeout)
                    throw new DesktopActionException(L.T(
                        $"no window of {launch.WindowSource} appeared within {FirstWindowTimeout.TotalSeconds:0} seconds (or it could not be moved).",
                        $"innerhalb von {FirstWindowTimeout.TotalSeconds:0} Sekunden kam kein Fenster von {launch.WindowSource} (oder es ließ sich nicht verschieben)."));
                await Task.Delay(PollInterval);
            }
        }
        catch
        {
            Launching.Remove(launch);
            launch.Dispose();
            throw;
        }
        _ = KeepMovingAsync(launch);
    }

    /// <summary>Keeps moving windows that the program opens after its first one (e.g. the main window after a splash screen).</summary>
    static async Task KeepMovingAsync(Launch launch)
    {
        try
        {
            var firstMove = DateTime.UtcNow;
            var lastMove = firstMove;
            while (DateTime.UtcNow - firstMove < WatchTime)
            {
                await Task.Delay(PollInterval);
                if (launch.MoveNewWindows())
                    lastMove = DateTime.UtcNow;
                else if (DateTime.UtcNow - lastMove >= SettleTime && !launch.IsRunning)
                    break;
            }
        }
        finally
        {
            Launching.Remove(launch);
            launch.Dispose();
        }
    }

    static string CleanPath(string path) => Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));

    /// <summary>One started program: which windows belong to it and the desktop they go to.</summary>
    sealed class Launch : IDisposable
    {
        readonly string _path;
        readonly int _target;
        // "Window of": a folder (every exe below it counts, e.g. a game's install folder) or an exe name.
        // Empty = the started program itself. With an entry, the started program is only a launcher whose own windows stay put.
        readonly string? _folder;
        readonly string _exeName;
        readonly bool _byNameOnly;
        // Exes seen so far (the program, or whatever ran from the folder), to tell whether it is still alive.
        readonly HashSet<string> _exeNames = new(StringComparer.OrdinalIgnoreCase);
        // Windows that already existed before the start are never ours, whatever process they belong to.
        readonly HashSet<IntPtr> _known = NativeMethods.GetAppWindows().ToHashSet();
        Process? _process;
        int? _processId;   // null if the shell handed the file to an already running app
        int _stayOn = -1;  // desktop the user was on when one of our windows was moved away from it
        DateTime _guardUntil;

        public Launch(string path, string window, int target)
        {
            _path = path;
            _target = target;
            _byNameOnly = window.Length > 0;
            if (window.Length > 0 && Directory.Exists(window))
            {
                _folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(window));
                _exeName = "";
            }
            else
            {
                // "Diablo IV", "Diablo IV.exe" and a full path all become "Diablo IV.exe".
                string name = Path.GetFileName(window);
                _exeName = name.Length == 0 ? Path.GetFileName(path) : Path.HasExtension(name) ? name : name + ".exe";
                _exeNames.Add(_exeName);
            }
        }

        /// <summary>What windows are matched by, for messages: the folder or the exe name.</summary>
        public string WindowSource => _folder ?? _exeName;

        /// <summary>Whether a process running from this path is one whose windows belong to this launch.</summary>
        public bool Owns(string processPath)
        {
            if (processPath.Length == 0)
                return false;
            if (_folder is not null)
                return processPath.StartsWith(_folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            return string.Equals(Path.GetFileName(processPath), _exeName, StringComparison.OrdinalIgnoreCase);
        }

        public void Start(string arguments)
        {
            _process = Process.Start(new ProcessStartInfo(_path, arguments)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.IsPathRooted(_path) ? Path.GetDirectoryName(_path) ?? "" : "",
            });
            _processId = _process?.Id;
        }

        /// <summary>The started process, or any process of an exe we have seen (launchers hand over to a second one), is still alive.</summary>
        public bool IsRunning
        {
            get
            {
                try
                {
                    if (_process is { HasExited: false })
                        return true;
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                }
                foreach (string exeName in _exeNames)
                {
                    var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exeName));
                    foreach (var process in processes)
                        process.Dispose();
                    if (processes.Length > 0)
                        return true;
                }
                return false;
            }
        }

        /// <summary>Moves windows that appeared since the last call to the target desktop. True if at least one is there now.</summary>
        public bool MoveNewWindows()
        {
            bool any = false;
            int current = Vda.GetCurrentDesktopNumber();
            foreach (IntPtr hwnd in NativeMethods.GetAppWindows())
            {
                if (!_known.Add(hwnd) || !IsOurs(hwnd))
                    continue;
                if (Vda.GetWindowDesktopNumber(hwnd) == _target)
                    any = true;
                else if (Vda.MoveWindowToDesktopNumber(hwnd, _target) != -1)
                {
                    any = true;
                    if (current != _target)
                    {
                        _stayOn = current;
                        _guardUntil = DateTime.UtcNow + StayGuardTime;
                    }
                }
            }
            KeepUserOnTheirDesktop();
            return any;
        }

        // Shortly after a move, the program usually activates the moved window and Windows follows it
        // to the target desktop; take the user back to where they were.
        void KeepUserOnTheirDesktop()
        {
            if (_stayOn < 0)
                return;
            if (DateTime.UtcNow > _guardUntil)
            {
                _stayOn = -1;
                return;
            }
            if (Vda.GetCurrentDesktopNumber() == _target && IsOurs(NativeMethods.GetForegroundWindow()))
                Vda.GoToDesktopNumber(_stayOn);
        }

        // Match by process, or by exe name / folder for launchers that hand over to a second process.
        bool IsOurs(IntPtr hwnd)
        {
            int processId = NativeMethods.GetWindowProcessId(hwnd);
            if (!_byNameOnly && processId == _processId)
                return true;
            string processPath = NativeMethods.GetProcessPath(processId);
            if (!Owns(processPath))
                return false;
            _exeNames.Add(Path.GetFileName(processPath));
            return true;
        }

        public void Dispose() => _process?.Dispose();
    }
}
