using System.Text;
using Puffling.App.Native;
using Puffling.Core.Settings;
using static Puffling.App.Native.Win32;

namespace Puffling.App.Windows;

public sealed class MicrosoftSettingsWindow : NativeWindow
{
    private const string ClassName = "Puffling.MicrosoftSettings";
    private const int Client = 101, Time = 102, Lead = 103, Enabled = 104, Connect = 105, Disconnect = 106, Preview = 107;
    private readonly Action<CompanionSettings> _onChanged;
    private readonly Action _onConnect, _onDisconnect, _onClosed, _onPreview;
    private readonly IntPtr _client, _time, _lead, _enabled, _status;
    private readonly IntPtr _font;
    private CompanionSettings _settings;
    private bool _syncing;

    public MicrosoftSettingsWindow(CompanionSettings settings, Action<CompanionSettings> onChanged,
        Action onConnect, Action onDisconnect, Action onPreview, Action onClosed)
    {
        _settings = settings.Clone();
        _onChanged = onChanged; _onConnect = onConnect; _onDisconnect = onDisconnect; _onPreview = onPreview; _onClosed = onClosed;
        EnsureClass(ClassName, CS_HREDRAW | CS_VREDRAW, LoadCursorW(IntPtr.Zero, IDC_ARROW), (IntPtr)(COLOR_BTNFACE + 1));
        uint dpi = GetDpiForSystem();
        int S(int n) => n * (int)dpi / 96;
        Create(ClassName, "Puffling · Microsoft reminders", WS_EX_CONTROLPARENT | WS_EX_DLGMODALFRAME,
            WS_CAPTION | WS_SYSMENU, CW_USEDEFAULT, CW_USEDEFAULT, S(430), S(330), IntPtr.Zero);
        _font = CreateFontW(-(9 * (int)dpi / 72), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");

        Child("STATIC", "Microsoft app (client) ID", SS_LEFT, S(18), S(15), S(390), S(20), 0);
        _client = Child("EDIT", "", WS_BORDER | ES_AUTOHSCROLL | WS_TABSTOP, S(18), S(37), S(390), S(24), Client);
        Child("STATIC", "Daily To Do time (HH:mm, local time)", SS_LEFT, S(18), S(74), S(390), S(20), 0);
        _time = Child("EDIT", "", WS_BORDER | ES_AUTOHSCROLL | WS_TABSTOP, S(18), S(96), S(95), S(24), Time);
        Child("STATIC", "Remind me before events (minutes)", SS_LEFT, S(145), S(74), S(250), S(20), 0);
        _lead = Child("EDIT", "", WS_BORDER | ES_AUTOHSCROLL | WS_TABSTOP, S(145), S(96), S(95), S(24), Lead);
        _enabled = Child("BUTTON", "Show calendar and To Do reminders", BS_AUTOCHECKBOX | WS_TABSTOP,
            S(18), S(136), S(390), S(24), Enabled);
        Child("BUTTON", "Connect Microsoft", BS_PUSHBUTTON | WS_TABSTOP, S(18), S(174), S(185), S(29), Connect);
        Child("BUTTON", "Disconnect", BS_PUSHBUTTON | WS_TABSTOP, S(215), S(174), S(110), S(29), Disconnect);
        Child("BUTTON", "Preview", BS_PUSHBUTTON | WS_TABSTOP, S(333), S(174), S(75), S(29), Preview);
        _status = Child("STATIC", "", SS_LEFT, S(18), S(218), S(390), S(55), 0);
        Sync(settings);
        SetStatus("Enter an app ID, then connect your Microsoft account.");
        ShowWindow(Handle, SW_SHOW);
        SetForegroundWindow(Handle);
    }

    private IntPtr Child(string cls, string text, uint style, int x, int y, int w, int h, int id)
    {
        IntPtr hwnd = CreateWindowExW(0, cls, text, WS_CHILD | WS_VISIBLE | style,
            x, y, w, h, Handle, (IntPtr)id, Instance, IntPtr.Zero);
        SendMessageW(hwnd, WM_SETFONT, _font, (IntPtr)1);
        return hwnd;
    }

    public void Sync(CompanionSettings s)
    {
        _syncing = true;
        try
        {
            _settings = s.Clone();
            SetWindowTextW(_client, s.MicrosoftClientId);
            SetWindowTextW(_time, s.DailyReminderTime);
            SetWindowTextW(_lead, s.EventReminderMinutes.ToString());
            SendMessageW(_enabled, BM_SETCHECK, (IntPtr)(s.MicrosoftReminders ? 1 : 0), IntPtr.Zero);
        }
        finally { _syncing = false; }
    }

    public void SetStatus(string status) => SetWindowTextW(_status, status);
    public void BringToFront() { ShowWindow(Handle, SW_SHOW); SetForegroundWindow(Handle); }

    private static string Read(IntPtr hwnd)
    {
        var text = new StringBuilder(256);
        GetWindowTextW(hwnd, text, text.Capacity);
        return text.ToString().Trim();
    }

    private void Commit()
    {
        var next = _settings.Clone();
        next.MicrosoftClientId = Read(_client);
        next.DailyReminderTime = Read(_time);
        if (int.TryParse(Read(_lead), out int minutes)) next.EventReminderMinutes = minutes;
        next.MicrosoftReminders = SendMessageW(_enabled, BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)1;
        _settings = next.Normalize();
        _onChanged(_settings.Clone());
    }

    protected override IntPtr WndProc(uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_COMMAND && !_syncing)
        {
            int id = LoWord(wParam), notification = HiWord(wParam);
            if (notification == EN_KILLFOCUS && id is Client or Time or Lead) { Commit(); return IntPtr.Zero; }
            if (notification == BN_CLICKED)
            {
                if (id == Enabled) Commit();
                else if (id == Connect) { Commit(); _onConnect(); }
                else if (id == Disconnect) _onDisconnect();
                else if (id == Preview) _onPreview();
                return IntPtr.Zero;
            }
        }
        if (msg == WM_CLOSE) { Commit(); DestroyWindow(Handle); return IntPtr.Zero; }
        if (msg == WM_DESTROY)
        {
            DeleteObject(_font);
            _onClosed();
            return IntPtr.Zero;
        }
        return Default(msg, wParam, lParam);
    }
}
