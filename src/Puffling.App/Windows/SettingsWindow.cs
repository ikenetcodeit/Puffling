using Puffling.App.Native;
using Puffling.Core.Settings;
using static Puffling.App.Native.Win32;

namespace Puffling.App.Windows;

/// <summary>
/// Compact settings panel built from standard Win32 controls. Changes apply live (no OK/Apply
/// step): each change produces a new settings object and hands it to the controller, which is
/// the single owner of settings state.
/// </summary>
public sealed class SettingsWindow : NativeWindow
{
    private const string ClassName = "Puffling.Settings";
    private const int SliderBase = 100, CheckBase = 200, BtnReset = 300, BtnExit = 301, BtnClose = 302, BtnMicrosoft = 303;

    private sealed record SliderDef(string Label, int Min, int Max, Func<CompanionSettings, int> Get, Action<CompanionSettings, int> Set, Func<int, string> Format);
    private sealed record CheckDef(string Label, Func<CompanionSettings, bool> Get, Action<CompanionSettings, bool> Set);

    private static readonly SliderDef[] Sliders =
    {
        new("Character size", CompanionSettings.MinScale, CompanionSettings.MaxScale, s => s.Scale, (s, v) => s.Scale = v, v => $"{v} px"),
        new("Movement speed", 25, 200, s => (int)MathF.Round(s.MovementSpeed * 100), (s, v) => s.MovementSpeed = v / 100f, v => $"{v}%"),
        new("Animation intensity", 30, 150, s => (int)MathF.Round(s.AnimationIntensity * 100), (s, v) => s.AnimationIntensity = v / 100f, v => $"{v}%"),
        new("Idle activity frequency", 20, 200, s => (int)MathF.Round(s.ActivityFrequency * 100), (s, v) => s.ActivityFrequency = v / 100f, v => $"{v}%"),
        new("Frame-rate limit", 10, 60, s => s.FpsLimit, (s, v) => s.FpsLimit = v, v => $"{v} fps"),
    };

    private static readonly CheckDef[] Checks =
    {
        new("Wander around on its own", s => s.AutonomousMovement, (s, v) => s.AutonomousMovement = v),
        new("Follow the mouse cursor", s => s.FollowCursor, (s, v) => s.FollowCursor = v),
        new("Freeze position", s => s.FreezePosition, (s, v) => s.FreezePosition = v),
        new("Always on top", s => s.AlwaysOnTop, (s, v) => s.AlwaysOnTop = v),
        new("Start with Windows", s => s.StartWithWindows, (s, v) => s.StartWithWindows = v),
        new("Pause animations", s => s.Paused, (s, v) => s.Paused = v),
        new("Sleep mode (stay asleep)", s => s.SleepMode, (s, v) => s.SleepMode = v),
        new("Get sleepy when idle", s => s.AutoSleep, (s, v) => s.AutoSleep = v),
        new("Performance mode (15 fps, no particles)", s => s.PerformanceMode, (s, v) => s.PerformanceMode = v),
        new("Hide during fullscreen apps", s => s.HideInFullscreen, (s, v) => s.HideInFullscreen = v),
    };

    private readonly Action<CompanionSettings> _onChanged;
    private readonly Action _onReset, _onExit, _onClosed, _onMicrosoft;
    private readonly IntPtr[] _sliderHwnds = new IntPtr[Sliders.Length];
    private readonly IntPtr[] _labelHwnds = new IntPtr[Sliders.Length];
    private readonly IntPtr[] _checkHwnds = new IntPtr[Checks.Length];
    private CompanionSettings _settings;
    private IntPtr _font;
    private bool _syncing;

    public SettingsWindow(CompanionSettings settings, Action<CompanionSettings> onChanged, Action onReset, Action onExit, Action onClosed, Action onMicrosoft)
    {
        _settings = settings.Clone();
        _onChanged = onChanged; _onReset = onReset; _onExit = onExit; _onClosed = onClosed; _onMicrosoft = onMicrosoft;

        var icc = new INITCOMMONCONTROLSEX { dwSize = 8, dwICC = ICC_BAR_CLASSES | ICC_STANDARD_CLASSES };
        InitCommonControlsEx(ref icc);
        EnsureClass(ClassName, CS_HREDRAW | CS_VREDRAW, LoadCursorW(IntPtr.Zero, IDC_ARROW), (IntPtr)(COLOR_BTNFACE + 1));

        uint dpi = GetDpiForSystem();
        int S(int v) => v * (int)dpi / 96;
        int width = S(400), height = S(40) + S(56) * Sliders.Length + S(26) * Checks.Length + S(142);
        Create(ClassName, "Puffling settings", WS_EX_CONTROLPARENT | WS_EX_DLGMODALFRAME, WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX,
               CW_USEDEFAULT, CW_USEDEFAULT, width, height, IntPtr.Zero);

        _font = CreateFontW(-(9 * (int)dpi / 72), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");

        int x = S(18), w = S(350), y = S(14);
        for (int i = 0; i < Sliders.Length; i++)
        {
            _labelHwnds[i] = Child("STATIC", "", SS_LEFT, x, y, w, S(20), 0);
            _sliderHwnds[i] = Child("msctls_trackbar32", "", TBS_HORZ | TBS_NOTICKS | WS_TABSTOP, x - S(4), y + S(20), w + S(8), S(28), SliderBase + i);
            SendMessageW(_sliderHwnds[i], TBM_SETRANGE, (IntPtr)1, MakeLParam(Sliders[i].Min, Sliders[i].Max));
            SendMessageW(_sliderHwnds[i], TBM_SETPAGESIZE, IntPtr.Zero, (IntPtr)5);
            y += S(56);
        }
        y += S(4);
        for (int i = 0; i < Checks.Length; i++)
        {
            _checkHwnds[i] = Child("BUTTON", Checks[i].Label, BS_AUTOCHECKBOX | WS_TABSTOP, x, y, w, S(22), CheckBase + i);
            y += S(26);
        }
        y += S(12);
        Child("BUTTON", "Microsoft reminders...", BS_PUSHBUTTON | WS_TABSTOP, x, y, w, S(28), BtnMicrosoft);
        y += S(36);
        int bw = S(110);
        Child("BUTTON", "Reset position", BS_PUSHBUTTON | WS_TABSTOP, x, y, bw, S(28), BtnReset);
        Child("BUTTON", "Exit Puffling", BS_PUSHBUTTON | WS_TABSTOP, x + bw + S(10), y, bw, S(28), BtnExit);
        Child("BUTTON", "Close", BS_DEFPUSHBUTTON | WS_TABSTOP, x + w - S(90), y, S(90), S(28), BtnClose);

        Sync(_settings);
        ShowWindow(Handle, SW_SHOW);
        SetForegroundWindow(Handle); // user explicitly opened it: focus is appropriate here
    }

    private IntPtr Child(string cls, string text, uint style, int x, int y, int w, int h, int id)
    {
        IntPtr hwnd = CreateWindowExW(0, cls, text, WS_CHILD | WS_VISIBLE | style, x, y, w, h, Handle, (IntPtr)id, Instance, IntPtr.Zero);
        SendMessageW(hwnd, WM_SETFONT, _font, (IntPtr)1);
        return hwnd;
    }

    /// <summary>Refresh controls from settings changed elsewhere (tray menu, brain waking up...).</summary>
    public void Sync(CompanionSettings s)
    {
        _syncing = true;
        try
        {
            _settings = s.Clone();
            for (int i = 0; i < Sliders.Length; i++)
            {
                int v = Sliders[i].Get(s);
                SendMessageW(_sliderHwnds[i], TBM_SETPOS, (IntPtr)1, (IntPtr)v);
                SetWindowTextW(_labelHwnds[i], $"{Sliders[i].Label}: {Sliders[i].Format(v)}");
            }
            for (int i = 0; i < Checks.Length; i++)
                SendMessageW(_checkHwnds[i], BM_SETCHECK, (IntPtr)(Checks[i].Get(s) ? 1 : 0), IntPtr.Zero);
        }
        finally { _syncing = false; }
    }

    public void BringToFront()
    {
        ShowWindow(Handle, SW_SHOW);
        SetForegroundWindow(Handle);
    }

    protected override IntPtr WndProc(uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_HSCROLL when !_syncing:
                for (int i = 0; i < Sliders.Length; i++)
                {
                    if (_sliderHwnds[i] != lParam) continue;
                    int v = (int)SendMessageW(lParam, TBM_GETPOS, IntPtr.Zero, IntPtr.Zero);
                    SetWindowTextW(_labelHwnds[i], $"{Sliders[i].Label}: {Sliders[i].Format(v)}");
                    var next = _settings.Clone();
                    Sliders[i].Set(next, v);
                    Commit(next);
                }
                return IntPtr.Zero;

            case WM_COMMAND when !_syncing && HiWord(wParam) == BN_CLICKED:
            {
                int id = LoWord(wParam);
                if (id is >= CheckBase and < CheckBase + 100 && id - CheckBase < Checks.Length)
                {
                    int i = id - CheckBase;
                    bool on = SendMessageW(_checkHwnds[i], BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)1;
                    var next = _settings.Clone();
                    Checks[i].Set(next, on);
                    Commit(next);
                }
                else if (id == BtnReset) _onReset();
                else if (id == BtnMicrosoft) _onMicrosoft();
                else if (id == BtnExit) _onExit();
                else if (id == BtnClose || id == 2 /* IDCANCEL via Esc */) DestroyWindow(Handle);
                return IntPtr.Zero;
            }

            case WM_CLOSE:
                DestroyWindow(Handle);
                return IntPtr.Zero;

            case WM_DESTROY:
                if (_font != IntPtr.Zero) { DeleteObject(_font); _font = IntPtr.Zero; }
                _onClosed();
                return IntPtr.Zero;
        }
        return Default(msg, wParam, lParam);
    }

    private void Commit(CompanionSettings next)
    {
        _settings = next.Normalize();
        _onChanged(_settings.Clone());
    }
}
