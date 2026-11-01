using System.Numerics;
using Puffling.App.Native;
using Puffling.Core.Interaction;
using Puffling.Core.Rendering;
using static Puffling.App.Native.Win32;

namespace Puffling.App.Windows;

/// <summary>
/// The transparent, borderless, per-pixel-alpha window that shows the character.
///
/// Why UpdateLayeredWindow: with a per-pixel-alpha layered window, Windows itself hit-tests by
/// alpha — fully transparent pixels are click-through, opaque pixels receive the mouse. Since the
/// renderer emits only alpha 0 or 255, the clickable area is EXACTLY the visible character, with
/// no invisible rectangle blocking the desktop and no hit-test polling on our side.
///
/// WS_EX_NOACTIVATE + MA_NOACTIVATE: clicking the pet never steals keyboard focus.
/// WS_EX_TOOLWINDOW + owned by a hidden window: no taskbar button, not in Alt-Tab.
/// </summary>
public sealed unsafe class OverlayWindow : NativeWindow, IDisposable
{
    private const string ClassName = "Puffling.Overlay";
    private readonly PointerTracker _pointer;
    private readonly Func<double> _clock;
    private readonly Action<int, int> _onContextMenu;

    private IntPtr _screenDc, _memDc, _dib, _oldBmp, _bits;
    private int _dibSize;
    private bool _trackingLeave, _visible, _topmost;
    private ulong _lastHash;
    private int _lastLeft = int.MinValue, _lastTop = int.MinValue;
    private readonly IntPtr _handCursor = LoadCursorW(IntPtr.Zero, IDC_HAND);
    private readonly IntPtr _dragCursor = LoadCursorW(IntPtr.Zero, IDC_SIZEALL);

    public OverlayWindow(IntPtr owner, PointerTracker pointer, Func<double> clock, Action<int, int> onContextMenu, bool topmost)
    {
        _pointer = pointer; _clock = clock; _onContextMenu = onContextMenu; _topmost = topmost;
        EnsureClass(ClassName, CS_DBLCLKS, IntPtr.Zero, IntPtr.Zero);
        uint ex = WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | (topmost ? WS_EX_TOPMOST : 0);
        Create(ClassName, "Puffling", ex, WS_POPUP, 0, 0, 1, 1, owner);
        _screenDc = GetDC(IntPtr.Zero);
        _memDc = CreateCompatibleDC(_screenDc);
    }

    public void SetTopmost(bool on)
    {
        _topmost = on;
        SetWindowPos(Handle, on ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>Re-asserts topmost (other topmost windows, e.g. the taskbar, can end up above us).</summary>
    public void ReassertTopmost()
    {
        if (_topmost && _visible) SetTopmost(true);
    }

    public void Hide()
    {
        if (!_visible) return;
        ShowWindow(Handle, SW_HIDE);
        _visible = false;
    }

    /// <summary>Pushes a frame to the screen. Skips the call entirely if neither pixels nor position changed.</summary>
    public void Present(FrameBuffer fb, int left, int top)
    {
        int size = fb.Width;
        ulong hash = fb.ComputeHash();
        bool pixelsChanged = hash != _lastHash || size != _dibSize;
        bool moved = left != _lastLeft || top != _lastTop;
        if (!pixelsChanged && !moved && _visible) return;

        if (size != _dibSize) RecreateDib(size);
        if (pixelsChanged) fb.Color.AsSpan().CopyTo(new Span<uint>((void*)_bits, size * size));

        var dst = new POINT(left, top);
        var sz = new SIZE(size, size);
        var src = new POINT(0, 0);
        var blend = new BLENDFUNCTION { BlendOp = AC_SRC_OVER, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };
        UpdateLayeredWindow(Handle, _screenDc, ref dst, ref sz, _memDc, ref src, 0, ref blend, ULW_ALPHA);

        _lastHash = hash; _lastLeft = left; _lastTop = top;
        if (!_visible)
        {
            ShowWindow(Handle, SW_SHOWNOACTIVATE);
            _visible = true;
            if (_topmost) SetTopmost(true);
        }
    }

    private void RecreateDib(int size)
    {
        if (_dib != IntPtr.Zero) { SelectObject(_memDc, _oldBmp); DeleteObject(_dib); }
        var bmi = new BITMAPINFOHEADER
        {
            biSize = (uint)sizeof(BITMAPINFOHEADER), biWidth = size, biHeight = -size, // negative = top-down
            biPlanes = 1, biBitCount = 32, biCompression = BI_RGB,
        };
        _dib = CreateDIBSection(_screenDc, ref bmi, DIB_RGB_COLORS, out _bits, IntPtr.Zero, 0);
        if (_dib == IntPtr.Zero) throw new InvalidOperationException("CreateDIBSection failed");
        _oldBmp = SelectObject(_memDc, _dib);
        _dibSize = size;
    }

    private Vector2 CursorScreen()
    {
        GetCursorPos(out var p);
        return new Vector2(p.X, p.Y);
    }

    protected override IntPtr WndProc(uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_MOUSEACTIVATE:
                return MA_NOACTIVATE;

            case WM_SETCURSOR:
                SetCursor(_pointer.IsDragging ? _dragCursor : _handCursor);
                return 1;

            case WM_LBUTTONDOWN:
                SetCapture(Handle);
                _pointer.PointerDown(CursorScreen(), _clock());
                return IntPtr.Zero;

            case WM_LBUTTONDBLCLK:
                SetCapture(Handle);
                _pointer.DoubleClick(CursorScreen(), _clock());
                return IntPtr.Zero;

            case WM_LBUTTONUP:
                _pointer.PointerUp(CursorScreen(), _clock());
                if (GetCapture() == Handle) ReleaseCapture();
                return IntPtr.Zero;

            case WM_MOUSEMOVE:
                if (!_trackingLeave)
                {
                    var tme = new TRACKMOUSEEVENT { cbSize = (uint)sizeof(TRACKMOUSEEVENT), dwFlags = TME_LEAVE, hwndTrack = Handle };
                    _trackingLeave = TrackMouseEvent(ref tme);
                }
                // Screen coordinates (not client): the window itself moves while bobbing, and we
                // must not mistake our own motion for the user stroking the pet.
                _pointer.PointerMove(CursorScreen(), _clock());
                return IntPtr.Zero;

            case WM_MOUSELEAVE:
                _trackingLeave = false;
                _pointer.PointerLeave(CursorScreen());
                return IntPtr.Zero;

            case WM_CAPTURECHANGED:
                if (lParam != Handle) _pointer.Cancel(CursorScreen());
                return IntPtr.Zero;

            case WM_RBUTTONUP:
            {
                var p = CursorScreen();
                _onContextMenu((int)p.X, (int)p.Y);
                return IntPtr.Zero;
            }
        }
        return Default(msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_dib != IntPtr.Zero) { SelectObject(_memDc, _oldBmp); DeleteObject(_dib); _dib = IntPtr.Zero; }
        if (_memDc != IntPtr.Zero) { DeleteDC(_memDc); _memDc = IntPtr.Zero; }
        if (_screenDc != IntPtr.Zero) { ReleaseDC(IntPtr.Zero, _screenDc); _screenDc = IntPtr.Zero; }
        if (Handle != IntPtr.Zero) DestroyWindow(Handle);
    }
}
