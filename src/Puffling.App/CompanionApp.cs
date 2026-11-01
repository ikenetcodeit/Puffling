using System.Diagnostics;
using System.Numerics;
using System.Collections.Concurrent;
using Puffling.App.Microsoft;
using Puffling.App.Native;
using Puffling.App.Platform;
using Puffling.App.Windows;
using Puffling.Core;
using Puffling.Core.Behavior;
using Puffling.Core.Movement;
using Puffling.Core.Reminders;
using Puffling.Core.Settings;
using static Puffling.App.Native.Win32;

namespace Puffling.App;

/// <summary>
/// Application controller and lifecycle owner. A hidden window gives us a message target for
/// the frame timer, tray callbacks, menus and system broadcasts (display/DPI changes, power
/// events, Explorer restarts, session end).
///
/// Ownership: this class is the single owner of the settings object. Every change — settings
/// window, tray menu, or the brain itself — goes through <see cref="ApplySettings"/>, which updates
/// the simulation, the OS integration (topmost, startup) and schedules a save.
/// </summary>
public sealed class CompanionApp : NativeWindow, IDisposable
{
    private const string ClassName = "Puffling.Controller";
    private static readonly IntPtr FrameTimer = (IntPtr)1;
    private const uint UiWorkMessage = WM_APP + 2;

    private enum Cmd
    {
        Dance = 1, Happy, ComeHere, Sleep, Wake, Follow, Wander, Freeze, OnTop, Pause, SleepMode, Settings, Reset, Exit,
    }

    private readonly SettingsStore _store;
    private CompanionSettings _settings;
    private readonly Companion _companion;
    private readonly OverlayWindow _overlay;
    private readonly TrayIcon _tray;
    private readonly SpeechBubbleWindow _bubble;
    private readonly ConcurrentQueue<Action> _uiWork = new();
    private readonly Queue<string> _bubbleMessages = new();
    private readonly ReminderPlanner _reminderPlanner = new();
    private MicrosoftRemindersClient? _microsoft;
    private ReminderSnapshot? _reminderSnapshot;
    private MicrosoftSettingsWindow? _microsoftWindow;
    private DateTimeOffset _lastMicrosoftFetch = DateTimeOffset.MinValue;
    private bool _microsoftFetching, _microsoftConnecting;
    private bool _microsoftErrorNotified;
    private string? _activeBubble;
    private double _bubbleUntil;
    private readonly uint _taskbarCreated;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private SettingsWindow? _settingsWindow;

    private double _lastFrame, _lastHousekeeping, _lastGeometryCheck, _lastSave, _lastTopmost;
    private string _geometryFingerprint;
    private bool _geometryDirty, _settingsDirty, _hiddenForFullscreen, _exiting;
    private uint _timerInterval;
    private Vector2 _lastSavedPos;
    private int _consecutiveFrameErrors;

    public IntPtr SettingsWindowHandle => _settingsWindow?.Handle ?? IntPtr.Zero;
    public IntPtr MicrosoftSettingsWindowHandle => _microsoftWindow?.Handle ?? IntPtr.Zero;

    public CompanionApp()
    {
        _store = new SettingsStore(SettingsStore.DefaultPath());
        _settings = _store.Load(Log.Info);

        EnsureClass(ClassName, 0, IntPtr.Zero, IntPtr.Zero);
        Create(ClassName, "Puffling", WS_EX_TOOLWINDOW, WS_POPUP, 0, 0, 0, 0, IntPtr.Zero);
        _taskbarCreated = RegisterWindowMessageW("TaskbarCreated");

        var geometry = DisplayProvider.Query();
        _geometryFingerprint = DisplayProvider.Fingerprint(geometry);
        // The simulation gets its own copy; this class remains the single owner of _settings.
        _companion = new Companion(_settings.Clone(), geometry);
        _companion.Brain.SettingsMutated += () => ApplySettings(_companion.Settings.Clone());
        _lastSavedPos = _companion.Position;

        _overlay = new OverlayWindow(Handle, _companion.Pointer, Now, ShowContextMenu, _settings.AlwaysOnTop);
        _bubble = new SpeechBubbleWindow(Handle, _settings.AlwaysOnTop);
        if (_settings.MicrosoftClientId.Length > 0) _microsoft = new MicrosoftRemindersClient(_settings.MicrosoftClientId);
        _tray = new TrayIcon(Handle);
        _tray.Show(Tooltip());

        StartupRegistration.Apply(_settings.StartWithWindows);
        SetFrameInterval();
        Log.Info($"Started. Monitors: {_geometryFingerprint}");
    }

    private double Now() => _clock.Elapsed.TotalSeconds;

    // ------------------------------------------------------------------ frame loop

    private void OnFrame()
    {
        double now = Now();
        float dt = (float)(now - _lastFrame);
        _lastFrame = now;

        if (now - _lastHousekeeping >= 1.0) Housekeeping(now);
        UpdateBubble(now);
        if (_hiddenForFullscreen) return;

        GetCursorPos(out var cp);
        var input = new FrameInputs(new Vector2(cp.X, cp.Y), UserIdleSeconds());
        _companion.Tick(dt, input);
        var r = _companion.Render();
        _overlay.Present(r.Frame, r.Left, r.Top);
        if (_activeBubble != null) _bubble.MoveNear(_companion.Position, _companion.Brain.Geometry.MonitorAt(_companion.Position));

        SetFrameInterval();
        _consecutiveFrameErrors = 0;
    }

    /// <summary>Once-a-second work: fullscreen detection, display changes, topmost, saving.</summary>
    private void Housekeeping(double now)
    {
        _lastHousekeeping = now;

        bool fs = _settings.HideInFullscreen && FullscreenDetector.AffectsPoint(FullscreenDetector.Detect(), _companion.Position);
        if (fs != _hiddenForFullscreen)
        {
            _hiddenForFullscreen = fs;
            if (fs) { _overlay.Hide(); _bubble.Hide(); }
            Log.Info(fs ? "Fullscreen app detected: hiding and idling." : "Fullscreen app gone: resuming.");
            _lastFrame = Now(); // don't feed a huge dt after resuming
            SetFrameInterval();
        }

        // Display layout: react to change notifications immediately, and poll every 3 s as a safety
        // net (some docking/undocking sequences don't deliver a usable WM_DISPLAYCHANGE).
        if (_geometryDirty || now - _lastGeometryCheck > 3.0)
        {
            _lastGeometryCheck = now;
            _geometryDirty = false;
            var g = DisplayProvider.Query();
            string fp = DisplayProvider.Fingerprint(g);
            if (fp != _geometryFingerprint)
            {
                _geometryFingerprint = fp;
                _companion.UpdateGeometry(g);
                Log.Info($"Display layout changed: {fp}");
            }
        }

        if (now - _lastTopmost > 5.0) { _lastTopmost = now; _overlay.ReassertTopmost(); }

        var pos = _companion.Position;
        if (Vector2.Distance(pos, _lastSavedPos) > 2f && now - _lastSave > 30.0) _settingsDirty = true;
        if (_settingsDirty && now - _lastSave > 2.0) Save();
        CheckMicrosoftReminders();
    }

    private void QueueOnUi(Action action)
    {
        if (_exiting) return;
        _uiWork.Enqueue(action);
        PostMessageW(Handle, UiWorkMessage, IntPtr.Zero, IntPtr.Zero);
    }

    private void Say(string message) => _bubbleMessages.Enqueue(message);

    private void UpdateBubble(double now)
    {
        if (_activeBubble != null && now >= _bubbleUntil) { _bubble.Hide(); _activeBubble = null; }
        if (_hiddenForFullscreen || _activeBubble != null || _bubbleMessages.Count == 0) return;
        _activeBubble = _bubbleMessages.Dequeue();
        _bubbleUntil = now + 12;
        _bubble.Show(_activeBubble, _companion.Position, _companion.Brain.Geometry.MonitorAt(_companion.Position));
    }

    private void CheckMicrosoftReminders()
    {
        if (!_settings.MicrosoftReminders || _microsoft == null) return;
        var now = DateTimeOffset.Now;
        if (!_microsoftFetching && now - _lastMicrosoftFetch >= TimeSpan.FromMinutes(5))
        {
            _microsoftFetching = true;
            _lastMicrosoftFetch = now;
            var client = _microsoft;
            _ = Task.Run(async () =>
            {
                try
                {
                    var snapshot = await client.FetchAsync(DateTimeOffset.Now);
                    QueueOnUi(() => { if (_microsoft == client) { _reminderSnapshot = snapshot; _microsoftErrorNotified = false; _microsoftWindow?.SetStatus("Connected · reminders are active."); } _microsoftFetching = false; });
                }
                catch (Exception ex)
                {
                    Log.Error("Microsoft reminders refresh failed", ex);
                    QueueOnUi(() =>
                    {
                        _microsoftFetching = false;
                        _reminderSnapshot = null;
                        _microsoftWindow?.SetStatus("Could not refresh. Check your sign-in and connection.");
                        if (!_microsoftErrorNotified) { Say("I couldn't check Microsoft reminders. Please reconnect in Settings."); _microsoftErrorNotified = true; }
                    });
                }
            });
        }
        if (_reminderSnapshot is { } current)
            foreach (string message in _reminderPlanner.Due(now, current, _settings.DailyReminderTime, _settings.EventReminderMinutes)) Say(message);
    }

    private void SetFrameInterval()
    {
        int fps;
        if (_hiddenForFullscreen) fps = 1;                                   // just keep checking
        else if (_companion.Pointer.IsDragging) fps = 60;                    // drag must feel glued to the cursor
        else if (_settings.Paused) fps = 4;                                  // still respond to input/drag
        else if (_companion.IsSleeping) fps = Math.Min(12, _settings.EffectiveFps);
        else fps = _settings.EffectiveFps;

        uint interval = (uint)Math.Max(10, 1000 / fps);
        if (interval == _timerInterval) return;
        _timerInterval = interval;
        SetTimer(Handle, FrameTimer, interval, IntPtr.Zero);
    }

    private static float UserIdleSeconds()
    {
        var lii = new LASTINPUTINFO { cbSize = 8 };
        if (!GetLastInputInfo(ref lii)) return 0;
        uint ticks = unchecked((uint)Environment.TickCount - lii.dwTime);
        return ticks / 1000f;
    }

    // ------------------------------------------------------------------ settings

    private void ApplySettings(CompanionSettings next)
    {
        var prev = _settings;
        _settings = next.Normalize();
        _companion.ApplySettings(_settings.Clone());
        if (prev.AlwaysOnTop != _settings.AlwaysOnTop) _overlay.SetTopmost(_settings.AlwaysOnTop);
        if (prev.AlwaysOnTop != _settings.AlwaysOnTop) _bubble.SetTopmost(_settings.AlwaysOnTop);
        if (prev.MicrosoftClientId != _settings.MicrosoftClientId)
        {
            _microsoft = _settings.MicrosoftClientId.Length > 0 ? new MicrosoftRemindersClient(_settings.MicrosoftClientId) : null;
            _reminderSnapshot = null;
            _lastMicrosoftFetch = DateTimeOffset.MinValue;
            _microsoftErrorNotified = false;
        }
        if (prev.MicrosoftReminders && !_settings.MicrosoftReminders)
        {
            _reminderSnapshot = null;
            _bubbleMessages.Clear();
            _activeBubble = null;
            _bubble.Hide();
        }
        if (!prev.MicrosoftReminders && _settings.MicrosoftReminders) _lastMicrosoftFetch = DateTimeOffset.MinValue;
        if (prev.StartWithWindows != _settings.StartWithWindows) StartupRegistration.Apply(_settings.StartWithWindows);
        _settingsWindow?.Sync(_settings);
        _microsoftWindow?.Sync(_settings);
        _tray.SetTooltip(Tooltip());
        _settingsDirty = true;
        SetFrameInterval();
    }

    private void Save()
    {
        try
        {
            var pos = _companion.Position;
            _settings.LastX = pos.X;
            _settings.LastY = pos.Y;
            _store.Save(_settings);
            _lastSavedPos = pos;
            _settingsDirty = false;
        }
        catch (Exception ex) { Log.Error("Saving settings failed", ex); }
        _lastSave = Now();
    }

    private string Tooltip() =>
        "Puffling" + (_settings.Paused ? " (paused)" : _settings.SleepMode ? " (sleeping)" : "");

    // ------------------------------------------------------------------ menus

    private void ShowContextMenu(int x, int y)
    {
        IntPtr menu = CreatePopupMenu();
        try
        {
            bool asleep = _companion.Brain.IsAsleepish;
            void Item(Cmd id, string text, bool check = false, bool enabled = true) =>
                AppendMenuW(menu, MF_STRING | (check ? MF_CHECKED : 0) | (enabled ? 0 : MF_GRAYED), (IntPtr)(int)id, text);
            void Sep() => AppendMenuW(menu, MF_SEPARATOR, IntPtr.Zero, null);

            Item(Cmd.Dance, "Dance!");
            Item(Cmd.Happy, "Cheer up");
            Item(Cmd.ComeHere, "Come here");
            if (asleep) Item(Cmd.Wake, "Wake up"); else Item(Cmd.Sleep, "Go to sleep");
            Sep();
            Item(Cmd.Follow, "Follow the mouse", _settings.FollowCursor);
            Item(Cmd.Wander, "Wander on its own", _settings.AutonomousMovement);
            Item(Cmd.Freeze, "Freeze position", _settings.FreezePosition);
            Item(Cmd.OnTop, "Always on top", _settings.AlwaysOnTop);
            Item(Cmd.Pause, "Pause animations", _settings.Paused);
            Item(Cmd.SleepMode, "Sleep mode", _settings.SleepMode);
            Sep();
            Item(Cmd.Settings, "Settings…");
            Item(Cmd.Reset, "Reset position");
            Sep();
            Item(Cmd.Exit, "Exit");

            // Required so the menu closes when the user clicks elsewhere (documented Win32 quirk).
            SetForegroundWindow(Handle);
            int chosen = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_NONOTIFY, x, y, Handle, IntPtr.Zero);
            PostMessageW(Handle, WM_NULL, IntPtr.Zero, IntPtr.Zero);
            if (chosen != 0) Execute((Cmd)chosen);
        }
        finally { DestroyMenu(menu); }
    }

    private void Execute(Cmd c)
    {
        CompanionSettings Toggle(Action<CompanionSettings> f) { var n = _settings.Clone(); f(n); return n; }
        switch (c)
        {
            case Cmd.Dance: _companion.Execute(CompanionCommand.Dance); break;
            case Cmd.Happy: _companion.Execute(CompanionCommand.Happy); break;
            case Cmd.ComeHere: _companion.Execute(CompanionCommand.ComeHere); break;
            case Cmd.Sleep: _companion.Execute(CompanionCommand.Sleep); break;
            case Cmd.Wake: _companion.Execute(CompanionCommand.Wake); break;
            case Cmd.Follow: ApplySettings(Toggle(s => s.FollowCursor = !s.FollowCursor)); break;
            case Cmd.Wander: ApplySettings(Toggle(s => s.AutonomousMovement = !s.AutonomousMovement)); break;
            case Cmd.Freeze: ApplySettings(Toggle(s => s.FreezePosition = !s.FreezePosition)); break;
            case Cmd.OnTop: ApplySettings(Toggle(s => s.AlwaysOnTop = !s.AlwaysOnTop)); break;
            case Cmd.Pause: ApplySettings(Toggle(s => s.Paused = !s.Paused)); break;
            case Cmd.SleepMode: ApplySettings(Toggle(s => s.SleepMode = !s.SleepMode)); break;
            case Cmd.Settings: OpenSettings(); break;
            case Cmd.Reset: ResetPosition(); break;
            case Cmd.Exit: Exit(); break;
        }
    }

    private void ResetPosition()
    {
        _geometryDirty = true;
        _companion.UpdateGeometry(DisplayProvider.Query());
        _companion.Execute(CompanionCommand.ResetPosition);
        _settingsDirty = true;
    }

    private void OpenSettings()
    {
        if (_settingsWindow != null) { _settingsWindow.BringToFront(); return; }
        _settingsWindow = new SettingsWindow(_settings, ApplySettings, ResetPosition, Exit, () => _settingsWindow = null, OpenMicrosoftSettings);
    }

    private void OpenMicrosoftSettings()
    {
        if (_microsoftWindow != null) { _microsoftWindow.BringToFront(); return; }
        _microsoftWindow = new MicrosoftSettingsWindow(_settings, ApplySettings, ConnectMicrosoft, DisconnectMicrosoft,
            () => Say("Hi! I'll remind you right here when something is coming up."),
            () => _microsoftWindow = null);
        if (_microsoft != null)
        {
            var client = _microsoft;
            _ = Task.Run(async () =>
            {
                try { bool connected = await client.HasAccountAsync(); QueueOnUi(() => _microsoftWindow?.SetStatus(connected ? "Connected · reminders are ready." : "Enter an app ID, then connect your account.")); }
                catch (Exception ex) { Log.Error("Microsoft account check failed", ex); }
            });
        }
    }

    private void ConnectMicrosoft()
    {
        if (_microsoftConnecting) return;
        if (_microsoft == null) { _microsoftWindow?.SetStatus("Enter a valid Microsoft app (client) ID first."); return; }
        _microsoftConnecting = true;
        _microsoftWindow?.SetStatus("Finish signing in with your browser…");
        var client = _microsoft;
        _ = Task.Run(async () =>
        {
            try
            {
                string user = await client.ConnectAsync();
                QueueOnUi(() =>
                {
                    _microsoftConnecting = false;
                    if (_microsoft != client) return;
                    var next = _settings.Clone(); next.MicrosoftReminders = true; ApplySettings(next);
                    _lastMicrosoftFetch = DateTimeOffset.MinValue;
                    _microsoftWindow?.SetStatus($"Connected as {user}. Reminders are on.");
                    Say("Microsoft is connected. I'll keep an eye on your day!");
                });
            }
            catch (Exception ex)
            {
                Log.Error("Microsoft sign-in failed", ex);
                QueueOnUi(() => { _microsoftConnecting = false; _microsoftWindow?.SetStatus("Sign-in failed. Check the app ID and redirect URI."); });
            }
        });
    }

    private void DisconnectMicrosoft()
    {
        if (_microsoft == null) return;
        var client = _microsoft;
        _microsoftWindow?.SetStatus("Disconnecting…");
        _ = Task.Run(async () =>
        {
            try
            {
                await client.DisconnectAsync();
                QueueOnUi(() =>
                {
                    if (_microsoft != client) return;
                    var next = _settings.Clone(); next.MicrosoftReminders = false; ApplySettings(next);
                    _reminderSnapshot = null;
                    _microsoftWindow?.SetStatus("Disconnected. Reminders are off.");
                });
            }
            catch (Exception ex) { Log.Error("Microsoft disconnect failed", ex); QueueOnUi(() => _microsoftWindow?.SetStatus("Could not disconnect. See puffling.log.")); }
        });
    }

    public void Exit()
    {
        if (_exiting) return;
        _exiting = true;
        Save();
        if (_settingsWindow != null) DestroyWindow(_settingsWindow.Handle);
        if (_microsoftWindow != null) DestroyWindow(_microsoftWindow.Handle);
        DestroyWindow(Handle); // -> WM_DESTROY -> PostQuitMessage
    }

    // ------------------------------------------------------------------ messages

    protected override IntPtr WndProc(uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case UiWorkMessage:
                while (_uiWork.TryDequeue(out var action)) action();
                return IntPtr.Zero;
            case WM_TIMER when wParam == FrameTimer:
                try { OnFrame(); }
                catch (Exception ex)
                {
                    // A rendering/simulation bug must not crash the user's desktop pet loop;
                    // but a persistent failure should not spin forever either.
                    Log.Error("Frame failed", ex);
                    if (++_consecutiveFrameErrors > 100)
                    {
                        MessageBoxW(IntPtr.Zero, $"Puffling hit a repeating error and will close.\nDetails: {Log.FilePath}", "Puffling", MB_OK | MB_ICONERROR);
                        Exit();
                    }
                }
                return IntPtr.Zero;

            case TrayIcon.CallbackMessage:
            {
                uint mouse = (uint)(lParam.ToInt64() & 0xFFFF);
                if (mouse == WM_RBUTTONUP) { GetCursorPos(out var p); ShowContextMenu(p.X, p.Y); }
                else if (mouse == WM_LBUTTONUP) OpenSettings();
                return IntPtr.Zero;
            }

            case WM_DISPLAYCHANGE:
            case WM_DPICHANGED:
                _geometryDirty = true;
                _lastHousekeeping = 0; // handle on the next frame
                return Default(msg, wParam, lParam);

            case WM_SETTINGCHANGE when wParam == (IntPtr)0x2F /* SPI_SETWORKAREA: taskbar moved/resized */:
                _geometryDirty = true;
                _lastHousekeeping = 0;
                return Default(msg, wParam, lParam);

            case WM_POWERBROADCAST when wParam == (IntPtr)PBT_APMRESUMEAUTOMATIC || wParam == (IntPtr)PBT_APMRESUMESUSPEND:
                Log.Info("Resumed from sleep.");
                _lastFrame = Now();
                _geometryDirty = true;
                _lastHousekeeping = 0;
                return (IntPtr)1;

            case WM_QUERYENDSESSION:
                Save();
                return (IntPtr)1;

            case WM_ENDSESSION when wParam != IntPtr.Zero:
                Save();
                return IntPtr.Zero;

            case WM_DESTROY:
                KillTimer(Handle, FrameTimer);
                PostQuitMessage(0);
                return IntPtr.Zero;
        }

        if (msg == _taskbarCreated && _taskbarCreated != 0)
        {
            _tray.Show(Tooltip()); // Explorer restarted: our tray icon vanished with it
            return IntPtr.Zero;
        }
        return Default(msg, wParam, lParam);
    }

    public void Dispose()
    {
        _tray.Dispose();
        _overlay.Dispose();
        _bubble.Dispose();
        _microsoft?.Dispose();
    }
}
