using System.Runtime.InteropServices;
using Puffling.App.Native;
using Puffling.Core.Animation;
using Puffling.Core.Rendering;
using static Puffling.App.Native.Win32;

namespace Puffling.App.Windows;

/// <summary>Notification-area icon. The icon image is rendered from the character model itself,
/// at the exact small-icon size for the current DPI (no blurry downscaling).</summary>
public sealed unsafe class TrayIcon : IDisposable
{
    public const uint CallbackMessage = WM_APP + 1;
    private readonly IntPtr _owner;
    private IntPtr _icon;
    private bool _added;

    public TrayIcon(IntPtr owner) { _owner = owner; }

    /// <summary>Adds (or re-adds after Explorer restarts) the icon.</summary>
    public void Show(string tooltip)
    {
        if (_icon == IntPtr.Zero)
        {
            uint dpi = GetDpiForSystem();
            int size = GetSystemMetricsForDpi(SM_CXSMICON, dpi);
            _icon = CreateCharacterIcon(size > 0 ? size : 16);
        }
        var data = Data(tooltip);
        if (_added) Shell_NotifyIconW(NIM_DELETE, ref data);
        _added = Shell_NotifyIconW(NIM_ADD, ref data);
    }

    public void SetTooltip(string tooltip)
    {
        if (!_added) return;
        var data = Data(tooltip);
        Shell_NotifyIconW(NIM_MODIFY, ref data);
    }

    private NOTIFYICONDATAW Data(string tip) => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
        hWnd = _owner,
        uID = 1,
        uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
        uCallbackMessage = CallbackMessage,
        hIcon = _icon,
        szTip = tip.Length > 127 ? tip[..127] : tip,
        szInfo = "", szInfoTitle = "",
    };

    public static IntPtr CreateCharacterIcon(int size)
    {
        var pose = Pose.Rest;
        pose.Expression = Expression.Happy;
        var fb = new CharacterRenderer().Render(pose, 0.6f, 0f, Math.Max(16, size), particles: false);
        int s = fb.Width;

        IntPtr screen = GetDC(IntPtr.Zero);
        var bmi = new BITMAPINFOHEADER { biSize = (uint)sizeof(BITMAPINFOHEADER), biWidth = s, biHeight = -s, biPlanes = 1, biBitCount = 32, biCompression = BI_RGB };
        IntPtr color = CreateDIBSection(screen, ref bmi, DIB_RGB_COLORS, out IntPtr bits, IntPtr.Zero, 0);
        ReleaseDC(IntPtr.Zero, screen);
        if (color == IntPtr.Zero) return IntPtr.Zero;
        fb.Color.AsSpan().CopyTo(new Span<uint>((void*)bits, s * s)); // alpha is 0 or 255, so straight == premultiplied
        IntPtr mask = CreateBitmap(s, s, 1, 1, IntPtr.Zero);

        var ii = new ICONINFO { fIcon = 1, hbmColor = color, hbmMask = mask };
        IntPtr icon = CreateIconIndirect(ref ii);
        DeleteObject(color);
        DeleteObject(mask);
        return icon;
    }

    public void Dispose()
    {
        if (_added)
        {
            var data = Data("");
            Shell_NotifyIconW(NIM_DELETE, ref data);
            _added = false;
        }
        if (_icon != IntPtr.Zero) { DestroyIcon(_icon); _icon = IntPtr.Zero; }
    }
}
