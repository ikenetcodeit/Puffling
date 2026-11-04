using System.Numerics;
using Puffling.App.Native;
using Puffling.Core.Movement;
using static Puffling.App.Native.Win32;

namespace Puffling.App.Windows;

/// <summary>A small non-activating speech bubble that follows Puffling while a reminder is visible.</summary>
public sealed class SpeechBubbleWindow : NativeWindow, IDisposable
{
    private const string ClassName = "Puffling.SpeechBubble";
    private const uint Background = 0xE8FBEF;
    private readonly IntPtr _brush, _font, _label;
    private readonly int _width, _height;
    private bool _visible, _topmost;
    public bool IsVisible => _visible;

    public SpeechBubbleWindow(IntPtr owner, bool topmost)
    {
        _topmost = topmost;
        _brush = CreateSolidBrush(Background);
        EnsureClass(ClassName, 0, LoadCursorW(IntPtr.Zero, IDC_ARROW), _brush);
        uint dpi = GetDpiForSystem();
        _width = 220 * (int)dpi / 96;
        _height = 64 * (int)dpi / 96;
        Create(ClassName, "Puffling reminder", WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | (topmost ? WS_EX_TOPMOST : 0),
            WS_POPUP, 0, 0, _width, _height, owner);
        IntPtr region = CreateRoundRectRgn(0, 0, _width, _height, 14 * (int)dpi / 96, 14 * (int)dpi / 96);
        if (!SetWindowRgn(Handle, region, true)) DeleteObject(region);
        // Windows' built-in bitmap "Terminal" font, drawn without antialiasing (quality 3 =
        // NONANTIALIASED_QUALITY, charset 255 = OEM_CHARSET) for a pixelated notification look.
        _font = CreateFontW(-(10 * (int)dpi / 72), 0, 0, 0, 400, 0, 0, 0, 255, 0, 0, 3, 1, "Terminal");
        int pad = 10 * (int)dpi / 96;
        _label = CreateWindowExW(0, "STATIC", "", WS_CHILD | WS_VISIBLE | SS_LEFT,
            pad, pad, _width - 2 * pad, _height - 2 * pad, Handle, IntPtr.Zero, Instance, IntPtr.Zero);
        SendMessageW(_label, WM_SETFONT, _font, (IntPtr)1);
    }

    public void Show(string message, Vector2 pet, MonitorInfo monitor)
    {
        SetWindowTextW(_label, message);
        _visible = true;
        MoveNear(pet, monitor);
        ShowWindow(Handle, SW_SHOWNOACTIVATE);
    }

    public void MoveNear(Vector2 pet, MonitorInfo monitor)
    {
        if (!_visible) return;
        int gap = 20 * (int)GetDpiForWindow(Handle) / 96;
        var area = monitor.WorkArea;
        int x = (int)pet.X - _width - gap;
        if (x < area.Left) x = (int)pet.X + gap;
        x = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - _width));
        int y = Math.Clamp((int)pet.Y - _height / 2, area.Top, Math.Max(area.Top, area.Bottom - _height));
        SetWindowPos(Handle, _topmost ? HWND_TOPMOST : HWND_NOTOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public void Hide() { ShowWindow(Handle, SW_HIDE); _visible = false; }
    public void SetTopmost(bool topmost)
    {
        _topmost = topmost;
        SetWindowPos(Handle, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    protected override IntPtr WndProc(uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_CTLCOLORSTATIC)
        {
            SetTextColor(wParam, 0x263C34);
            SetBkColor(wParam, Background);
            return _brush;
        }
        if (msg == WM_MOUSEACTIVATE) return (IntPtr)MA_NOACTIVATE;
        return Default(msg, wParam, lParam);
    }

    public void Dispose()
    {
        DestroyWindow(Handle);
        DeleteObject(_font);
        DeleteObject(_brush);
    }
}
