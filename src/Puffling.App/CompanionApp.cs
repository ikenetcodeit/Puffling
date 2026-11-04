using System.Diagnostics;
using System.Numerics;
using System.Collections.Concurrent;
using Puffling.App.Microsoft;
using Puffling.App.Native;
using Puffling.App.Platform;
using Puffling.App.Windows;
using Puffling.Core;
using Puffling.Core.Behavior;
using Puffling.Core.Character;
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
        Dance = 1, Happy, Signature, ComeHere, Sleep, Wake, Follow, Wander, Freeze, OnTop, Pause, SleepMode,
        Spawn, Remove, Settings, Reset, Exit,
    }

    private sealed class CompanionInstance : IDisposable
    {
        public Companion Pet { get; }
        public OverlayWindow Window { get; }
        public Vector2 LastSavedPosition { get; set; }
        public bool HiddenForFullscreen { get; set; }

        public CompanionInstance(IntPtr owner, CompanionSettings settings, ScreenGeometry geometry,
            Func<double> clock, Action<CompanionInstance, int, int> onMenu, CompanionVariant variant)
        {
            Pet = new Companion(settings, geometry, variant: variant);
            Window = new OverlayWindow(owner, Pet.Pointer, clock, (x, y) => onMenu(this, x, y), settings.AlwaysOnTop);
            LastSavedPosition = Pet.Position;
        }

        public void Dispose() => Window.Dispose();
    }

    private readonly SettingsStore _store;
    private CompanionSettings _settings;
    private readonly List<CompanionInstance> _companions = new();
    private readonly List<Companion> _visiblePets = new(CompanionSettings.MaxCompanions);
    private readonly GroupInteractionCoordinator _groupInteractions = new();
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
    private SystemActivity _systemActivity;
    private double _lastActivityCheck;
    private bool _activityChecking;
    private string? _activeBubble;
    private double _bubbleUntil;
    private readonly uint _taskbarCreated;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private SettingsWindow? _settingsWindow;

    private double _lastFrame, _lastHousekeeping, _lastGeometryCheck, _lastSave, _lastTopmost;
    private string _geometryFingerprint;
    private bool _geometryDirty, _settingsDirty, _exiting;
    private uint _timerInterval;
    private int _consecutiveFrameErrors;

    private CompanionInstance Primary => _companions[0];
    private CompanionInstance? Speaker => _companions.FirstOrDefault(c => !c.HiddenForFullscreen);
    private bool AllHiddenForFullscreen => _companions.All(c => c.HiddenForFullscreen);

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
        // Each simulation gets a copy; this controller remains the single owner of shared settings.
        int restoredCount = _settings.CompanionCount;
        AddCompanion(geometry, _settings.LastX, _settings.LastY);
        for (int i = 1; i < restoredCount; i++)
        {
            var saved = i - 1 < _settings.AdditionalPositions.Count ? _settings.AdditionalPositions[i - 1] : (SavedCompanionPosition?)null;
            AddCompanion(geometry, saved?.X, saved?.Y);
        }
        _bubble = new SpeechBubbleWindow(Handle, _settings.AlwaysOnTop);
        if (_settings.MicrosoftClientId.Length > 0) _microsoft = new MicrosoftRemindersClient(_settings.MicrosoftClientId);
        _tray = new TrayIcon(Handle);
        _tray.Show(Tooltip());

        StartupRegistration.Apply(_settings.StartWithWindows);
        SetFrameInterval();
        Log.Info($"Started. Monitors: {_geometryFingerprint}");
    }

    private double Now() => _clock.Elapsed.TotalSeconds;

    private CompanionInstance AddCompanion(ScreenGeometry geometry, float? x, float? y, CompanionInstance? near = null)
    {
        if (_companions.Count >= CompanionSettings.MaxCompanions)
            throw new InvalidOperationException("Companion limit reached.");
        var own = _settings.Clone();
        if (_companions.Count > 0 && (x is null || y is null))
        {
            var anchor = near ?? _companions[^1];
            Vector2 p = CompanionPlacement.Find(geometry, anchor.Pet.Position,
                _companions.Select(c => c.Pet.Position).ToArray(), anchor.Pet.Brain.Margin);
            x = p.X; y = p.Y;
        }
        own.LastX = x; own.LastY = y;
        var instance = new CompanionInstance(Handle, own, geometry, Now, (slot, px, py) => ShowContextMenu(px, py, slot),
            CompanionVariants.ForSlot(_companions.Count));
        instance.Pet.Brain.SettingsMutated += () =>
        {
            var next = _settings.Clone();
            next.SleepMode = instance.Pet.Settings.SleepMode;
            ApplySettings(next);
        };
        _companions.Add(instance);
        return instance;
    }

    // ------------------------------------------------------------------ frame loop

    private void OnFrame()
    {
        double now = Now();
        float dt = (float)(now - _lastFrame);
        _lastFrame = now;

        if (now - _lastHousekeeping >= 1.0) Housekeeping(now);
        UpdateBubble(now);
        if (AllHiddenForFullscreen) return;

        GetCursorPos(out var cp);
        var input = new FrameInputs(new Vector2(cp.X, cp.Y), UserIdleSeconds());
        _visiblePets.Clear();
        foreach (var instance in _companions)
            if (!instance.HiddenForFullscreen) _visiblePets.Add(instance.Pet);
        _groupInteractions.Tick(now, _visiblePets);
        foreach (var instance in _companions)
        {
            if (instance.HiddenForFullscreen) continue;
            instance.Pet.Brain.CompanionsPresent = _visiblePets.Count;
            instance.Pet.Tick(dt, input);
        }
        CompanionCollision.Resolve(_visiblePets);
        foreach (var instance in _companions)
        {
            if (instance.HiddenForFullscreen) continue;
            var r = instance.Pet.Render();
            instance.Window.Present(r.Frame, r.Left, r.Top);
        }
        if (_activeBubble != null && Speaker is { } speaker)
            _bubble.MoveNear(speaker.Pet.Position, speaker.Pet.CurrentMonitor);

        SetFrameInterval();
        _consecutiveFrameErrors = 0;
    }

    /// <summary>Once-a-second work: fullscreen detection, display changes, topmost, saving.</summary>
    private void Housekeeping(double now)
    {
        _lastHousekeeping = now;

        var fullscreen = _settings.HideInFullscreen ? FullscreenDetector.Detect() : default;
        bool fullscreenChanged = false;
        foreach (var instance in _companions)
        {
            bool fs = _settings.HideInFullscreen && FullscreenDetector.AffectsPoint(fullscreen, instance.Pet.Position);
            if (fs == instance.HiddenForFullscreen) continue;
            instance.HiddenForFullscreen = fs;
            fullscreenChanged = true;
            if (fs) instance.Window.Hide();
        }
        if (fullscreenChanged)
        {
            if (Speaker == null) _bubble.Hide();
            Log.Info(AllHiddenForFullscreen ? "Fullscreen app detected: hiding and idling." : "Fullscreen state changed: resuming visible companions.");
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
                foreach (var instance in _companions) instance.Pet.UpdateGeometry(g);
                Log.Info($"Display layout changed: {fp}");
            }
        }

        if (now - _lastTopmost > 5.0)
        {
            _lastTopmost = now;
            foreach (var instance in _companions) instance.Window.ReassertTopmost();
        }

        if (now - _lastSave > 30.0 && _companions.Any(c => Vector2.Distance(c.Pet.Position, c.LastSavedPosition) > 2f))
            _settingsDirty = true;
        if (_settingsDirty && now - _lastSave > 2.0) Save();
        CheckMicrosoftReminders();
        CheckSystemActivity(now);
    }

    /// <summary>Polls, off the render loop, what the user is probably doing (listening to music,
    /// watching a video, coding, or running a coding agent) and lets each companion's brain react.</summary>
    private void CheckSystemActivity(double now)
    {
        if (_activityChecking || now - _lastActivityCheck < 5.0) return;
        _activityChecking = true;
        _lastActivityCheck = now;
        _ = Task.Run(() =>
        {
            var activity = SystemActivityDetector.Detect();
            QueueOnUi(() =>
            {
                _activityChecking = false;
                if (activity == _systemActivity) return;
                _systemActivity = activity;
                foreach (var instance in _companions) instance.Pet.Brain.SystemActivity = activity;
                string message = ActivityMessage(activity);
                if (message.Length > 0) Say(message);
            });
        });
    }

    private static string ActivityMessage(SystemActivity a) => a switch
    {
        SystemActivity.Music => "Vibing to your music.",
        SystemActivity.Movie => "Shh... movie time.",
        SystemActivity.Coding => "In the zone, coding.",
        SystemActivity.AgentRunning => "An agent is busy at work.",
        _ => "",
    };

    private void QueueOnUi(Action action)
    {
        if (_exiting) return;
        _uiWork.Enqueue(action);
        PostMessageW(Handle, UiWorkMessage, IntPtr.Zero, IntPtr.Zero);
    }

    private void Say(string message) => _bubbleMessages.Enqueue(message);

    private void UpdateBubble(double now)
    {
        var speaker = Speaker;
        if (speaker == null) return;
        if (_activeBubble != null && !_bubble.IsVisible)
        {
            _bubbleUntil = now + 12;
            _bubble.Show(_activeBubble, speaker.Pet.Position, speaker.Pet.CurrentMonitor);
            return;
        }
        if (_activeBubble != null && now >= _bubbleUntil) { _bubble.Hide(); _activeBubble = null; }
        if (_activeBubble != null || _bubbleMessages.Count == 0) return;
        _activeBubble = _bubbleMessages.Dequeue();
        _bubbleUntil = now + 12;
        _bubble.Show(_activeBubble, speaker.Pet.Position, speaker.Pet.CurrentMonitor);
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
        if (AllHiddenForFullscreen) fps = 1;                                   // just keep checking
        else if (_companions.Any(c => c.Pet.Pointer.IsDragging)) fps = 60;     // drag must feel glued to the cursor
        else if (_settings.Paused) fps = 4;                                  // still respond to input/drag
        else if (_companions.Where(c => !c.HiddenForFullscreen).All(c => c.Pet.IsSleeping)) fps = Math.Min(12, _settings.EffectiveFps);
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
        _settings.CompanionCount = _companions.Count;
        foreach (var instance in _companions) instance.Pet.ApplySettings(_settings.Clone());
        if (prev.AlwaysOnTop != _settings.AlwaysOnTop)
            foreach (var instance in _companions) instance.Window.SetTopmost(_settings.AlwaysOnTop);
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
            var pos = Primary.Pet.Position;
            _settings.LastX = pos.X;
            _settings.LastY = pos.Y;
            _settings.CompanionCount = _companions.Count;
            _settings.AdditionalPositions = _companions.Skip(1)
                .Select(c => new SavedCompanionPosition(c.Pet.Position.X, c.Pet.Position.Y)).ToList();
            _store.Save(_settings);
            foreach (var instance in _companions) instance.LastSavedPosition = instance.Pet.Position;
            _settingsDirty = false;
        }
        catch (Exception ex) { Log.Error("Saving settings failed", ex); }
        _lastSave = Now();
    }

    private string Tooltip() =>
        $"Puffling ({_companions.Count}/{CompanionSettings.MaxCompanions})" +
        (_settings.Paused ? " (paused)" : _settings.SleepMode ? " (sleeping)" : "");

    // ------------------------------------------------------------------ menus

    private void ShowContextMenu(int x, int y, CompanionInstance? selected = null)
    {
        bool fromTray = selected == null;
        var target = selected ?? Primary;
        IntPtr menu = CreatePopupMenu();
        try
        {
            bool asleep = target.Pet.Brain.IsAsleepish;
            void Item(Cmd id, string text, bool check = false, bool enabled = true) =>
                AppendMenuW(menu, MF_STRING | (check ? MF_CHECKED : 0) | (enabled ? 0 : MF_GRAYED), (IntPtr)(int)id, text);
            void Sep() => AppendMenuW(menu, MF_SEPARATOR, IntPtr.Zero, null);

            Item(Cmd.Dance, "Dance!");
            Item(Cmd.Happy, "Cheer up");
            Item(Cmd.Signature, target.Pet.Variant switch
            {
                CompanionVariant.Red => "Lead a rally",
                CompanionVariant.Blue => "Do a twirl",
                _ => "Show a dash",
            });
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
            Item(Cmd.Spawn, $"Spawn another ({_companions.Count}/{CompanionSettings.MaxCompanions})",
                enabled: _companions.Count < CompanionSettings.MaxCompanions);
            Item(Cmd.Remove, fromTray ? "Remove newest" : "Remove this Puffling",
                enabled: _companions.Count > 1);
            Sep();
            Item(Cmd.Settings, "Settings…");
            Item(Cmd.Reset, "Reset position");
            Sep();
            Item(Cmd.Exit, "Exit");

            // Required so the menu closes when the user clicks elsewhere (documented Win32 quirk).
            SetForegroundWindow(Handle);
            int chosen = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_NONOTIFY, x, y, Handle, IntPtr.Zero);
            PostMessageW(Handle, WM_NULL, IntPtr.Zero, IntPtr.Zero);
            if (chosen != 0) Execute((Cmd)chosen, target, fromTray);
        }
        finally { DestroyMenu(menu); }
    }

    private void Execute(Cmd c, CompanionInstance target, bool fromTray)
    {
        CompanionSettings Toggle(Action<CompanionSettings> f) { var n = _settings.Clone(); f(n); return n; }
        switch (c)
        {
            case Cmd.Dance: target.Pet.Execute(CompanionCommand.Dance); break;
            case Cmd.Happy: target.Pet.Execute(CompanionCommand.Happy); break;
            case Cmd.Signature: target.Pet.Execute(CompanionCommand.Signature); break;
            case Cmd.ComeHere: target.Pet.Execute(CompanionCommand.ComeHere); break;
            case Cmd.Sleep: target.Pet.Execute(CompanionCommand.Sleep); break;
            case Cmd.Wake: target.Pet.Execute(CompanionCommand.Wake); break;
            case Cmd.Follow: ApplySettings(Toggle(s => s.FollowCursor = !s.FollowCursor)); break;
            case Cmd.Wander: ApplySettings(Toggle(s => s.AutonomousMovement = !s.AutonomousMovement)); break;
            case Cmd.Freeze: ApplySettings(Toggle(s => s.FreezePosition = !s.FreezePosition)); break;
            case Cmd.OnTop: ApplySettings(Toggle(s => s.AlwaysOnTop = !s.AlwaysOnTop)); break;
            case Cmd.Pause: ApplySettings(Toggle(s => s.Paused = !s.Paused)); break;
            case Cmd.SleepMode: ApplySettings(Toggle(s => s.SleepMode = !s.SleepMode)); break;
            case Cmd.Spawn: SpawnCompanion(target); break;
            case Cmd.Remove: RemoveCompanion(fromTray ? _companions[^1] : target); break;
            case Cmd.Settings: OpenSettings(); break;
            case Cmd.Reset: ResetPosition(target); break;
            case Cmd.Exit: Exit(); break;
        }
    }

    private void SpawnCompanion(CompanionInstance near)
    {
        if (_companions.Count >= CompanionSettings.MaxCompanions) return;
        var instance = AddCompanion(near.Pet.Geometry, null, null, near);
        instance.HiddenForFullscreen = _settings.HideInFullscreen &&
            FullscreenDetector.AffectsPoint(FullscreenDetector.Detect(), instance.Pet.Position);
        instance.Pet.Execute(CompanionCommand.Happy);
        _settings.CompanionCount = _companions.Count;
        _tray.SetTooltip(Tooltip());
        Save();
        SetFrameInterval();
    }

    private void RemoveCompanion(CompanionInstance instance)
    {
        if (_companions.Count <= 1 || !_companions.Remove(instance)) return;
        instance.Dispose();
        if (Speaker == null) _bubble.Hide();
        _settings.CompanionCount = _companions.Count;
        _tray.SetTooltip(Tooltip());
        Save();
        SetFrameInterval();
    }

    private void ResetPosition(CompanionInstance target)
    {
        _geometryDirty = true;
        var geometry = DisplayProvider.Query();
        target.Pet.UpdateGeometry(geometry);
        target.Pet.Execute(CompanionCommand.ResetPosition);
        if (target != Primary)
        {
            Vector2 p = CompanionPlacement.Find(geometry, Primary.Pet.Position,
                _companions.Where(c => c != target).Select(c => c.Pet.Position).ToArray(), target.Pet.Brain.Margin);
            target.Pet.Motion.Teleport(p);
        }
        _settingsDirty = true;
    }

    private void OpenSettings()
    {
        if (_settingsWindow != null) { _settingsWindow.BringToFront(); return; }
        _settingsWindow = new SettingsWindow(_settings, ApplySettings, () => ResetPosition(Primary), Exit,
            () => _settingsWindow = null, OpenMicrosoftSettings);
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
        foreach (var instance in _companions) instance.Dispose();
        _bubble.Dispose();
        _microsoft?.Dispose();
    }
}
