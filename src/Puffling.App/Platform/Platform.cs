using System.Diagnostics;
using System.Numerics;
using Microsoft.Win32;
using Puffling.App.Native;
using Puffling.Core.Behavior;
using Puffling.Core.Movement;
using static Puffling.App.Native.Win32;

namespace Puffling.App.Platform;

/// <summary>Tiny size-capped file logger in %LOCALAPPDATA%\Puffling. Never throws.</summary>
public static class Log
{
    private static readonly object Gate = new();
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Puffling", "puffling.log");

    public static void Info(string msg) => Write("INFO ", msg);
    public static void Error(string msg, Exception? ex = null) => Write("ERROR", ex is null ? msg : $"{msg}: {ex}");

    private static void Write(string level, string msg)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var fi = new FileInfo(FilePath);
                if (fi.Exists && fi.Length > 512 * 1024) File.Move(FilePath, FilePath + ".old", overwrite: true);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {msg}{Environment.NewLine}");
            }
        }
        catch { /* logging must never take the app down */ }
    }
}

/// <summary>Builds a <see cref="ScreenGeometry"/> snapshot from the live monitor layout.</summary>
public static class DisplayProvider
{
    public static ScreenGeometry Query()
    {
        var list = new List<MonitorInfo>();
        MonitorEnumProc proc = (IntPtr h, IntPtr hdc, ref RECT r, IntPtr data) =>
        {
            var mi = new MONITORINFOEXW { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEXW>() };
            if (!GetMonitorInfoW(h, ref mi)) return true;
            float scale = 1f;
            if (GetDpiForMonitor(h, 0 /* MDT_EFFECTIVE_DPI */, out uint dx, out _) == 0 && dx > 0) scale = dx / 96f;
            list.Add(new MonitorInfo(mi.szDevice ?? $"mon{list.Count}",
                ToRect(mi.rcMonitor), ToRect(mi.rcWork), scale, (mi.dwFlags & MONITORINFOF_PRIMARY) != 0));
            return true;
        };
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, proc, IntPtr.Zero);
        GC.KeepAlive(proc);
        if (list.Count == 0)
        {
            // Can happen transiently during display reconfiguration; fall back to something sane.
            Log.Info("EnumDisplayMonitors returned no monitors; using fallback geometry.");
            return ScreenGeometry.Single(1920, 1080);
        }
        return new ScreenGeometry(list);
    }

    /// <summary>Order-independent fingerprint so we only rebuild when something really changed.</summary>
    public static string Fingerprint(ScreenGeometry g) => string.Join("|", g.Monitors
        .OrderBy(m => m.Id)
        .Select(m => $"{m.Id}:{m.Bounds}:{m.WorkArea}:{m.Scale}"));

    private static RectI ToRect(RECT r) => new(r.Left, r.Top, r.Right, r.Bottom);
}

/// <summary>
/// Detects a fullscreen app (game, video, presentation) so the pet can get out of the way.
/// Two signals: the shell's own notification state (exclusive D3D fullscreen, presentation mode),
/// and a borderless foreground window covering its whole monitor.
/// </summary>
public static class FullscreenDetector
{
    private static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
        { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };

    /// <returns>The fullscreen monitor's bounds, a zero rect for "unknown monitor, assume all", or null.</returns>
    public static RectI? Detect()
    {
        if (SHQueryUserNotificationState(out int state) == 0 && state is 3 /*D3D fullscreen*/ or 4 /*presentation*/)
            return new RectI(0, 0, 0, 0);

        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero) return null;
        GetWindowThreadProcessId(fg, out uint pid);
        if (pid == (uint)Environment.ProcessId) return null;

        var buf = new char[64];
        int n = GetClassNameW(fg, buf, buf.Length);
        if (n > 0 && ShellClasses.Contains(new string(buf, 0, n))) return null;

        // Maximised windows keep their caption; real fullscreen windows don't. This avoids
        // hiding the pet for every maximised window when the taskbar is set to auto-hide.
        if ((GetWindowStyle(fg) & WS_CAPTION) == WS_CAPTION) return null;
        if (!GetWindowRect(fg, out var wr)) return null;

        IntPtr mon = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
        var mi = new MONITORINFOEXW { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEXW>() };
        if (!GetMonitorInfoW(mon, ref mi)) return null;
        var m = mi.rcMonitor;
        bool covers = wr.Left <= m.Left && wr.Top <= m.Top && wr.Right >= m.Right && wr.Bottom >= m.Bottom;
        return covers ? new RectI(m.Left, m.Top, m.Right, m.Bottom) : null;
    }

    public static bool AffectsPoint(RectI? fs, Vector2 p) =>
        fs is { } r && (r.Width == 0 || r.Contains(p));
}

/// <summary>
/// Infers what the user is probably doing right now from running process names and the
/// foreground window title, so Puffling can react. Deliberately a coarse, best-effort heuristic:
/// an unrecognized app simply yields <see cref="SystemActivity.None"/> rather than a wrong guess.
/// Reads only process/window names, never file contents or network activity.
/// </summary>
public static class SystemActivityDetector
{
    private static readonly HashSet<string> AgentProcesses = new(StringComparer.OrdinalIgnoreCase)
        { "claude", "codex", "aider", "ollama" };
    private static readonly HashSet<string> CodingProcesses = new(StringComparer.OrdinalIgnoreCase)
        { "devenv", "code", "cursor", "rider64", "idea64", "pycharm64", "webstorm64", "clion64",
          "sublime_text", "notepad++" };
    private static readonly HashSet<string> MusicProcesses = new(StringComparer.OrdinalIgnoreCase)
        { "spotify", "wmplayer", "itunes", "foobar2000", "musicbee", "groove" };
    private static readonly HashSet<string> MovieProcesses = new(StringComparer.OrdinalIgnoreCase)
        { "vlc", "mpc-hc64", "mpc-hc", "mpv", "potplayer64", "potplayer", "mpc-be64" };
    private static readonly string[] MovieTitleHints =
        { "youtube", "netflix", "twitch", "prime video", "disney+", "- vlc media player" };

    public static SystemActivity Detect()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
            using (process) names.Add(process.ProcessName);

        // Priority: an agent actively working, then focused coding, then watching, then listening —
        // roughly most specific/noteworthy to most ambient.
        if (names.Overlaps(AgentProcesses)) return SystemActivity.AgentRunning;
        if (names.Overlaps(CodingProcesses)) return SystemActivity.Coding;

        string title = ForegroundTitle();
        if (names.Overlaps(MovieProcesses) || Array.Exists(MovieTitleHints,
                h => title.Contains(h, StringComparison.OrdinalIgnoreCase)))
            return SystemActivity.Movie;
        if (names.Overlaps(MusicProcesses)) return SystemActivity.Music;
        return SystemActivity.None;
    }

    private static string ForegroundTitle()
    {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero) return "";
        var sb = new System.Text.StringBuilder(256);
        GetWindowTextW(fg, sb, sb.Capacity);
        return sb.ToString();
    }
}

/// <summary>"Start with Windows" via the per-user Run key (no admin rights, no scheduled task).</summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Puffling";

    public static string LaunchCommand()
    {
        string exe = Environment.ProcessPath ?? "";
        // Running as "dotnet Puffling.dll" (dev builds): register the host + dll.
        if (Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            return $"\"{exe}\" \"{Path.Combine(AppContext.BaseDirectory, "Puffling.dll")}\"";
        return $"\"{exe}\"";
    }

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (enabled)
            {
                string cmd = LaunchCommand();
                if (!string.Equals(key.GetValue(ValueName) as string, cmd, StringComparison.Ordinal))
                    key.SetValue(ValueName, cmd); // also repairs a stale path after the exe was moved
            }
            else if (key.GetValue(ValueName) != null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex) { Log.Error("Updating startup registration failed", ex); }
    }
}
