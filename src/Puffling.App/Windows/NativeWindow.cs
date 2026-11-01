using System.Runtime.InteropServices;
using Puffling.App.Native;
using Puffling.App.Platform;
using static Puffling.App.Native.Win32;

namespace Puffling.App.Windows;

/// <summary>
/// Base for our Win32 windows: registers the class once and routes messages to a virtual
/// method. Exceptions must never cross the native boundary (that kills the process without
/// a useful error), so every message is handled inside a try/catch that logs and falls back
/// to default processing.
/// </summary>
public abstract class NativeWindow
{
    private static readonly Dictionary<IntPtr, NativeWindow> Live = new();
    private static readonly HashSet<string> Registered = new();
    // Single static delegate shared by all classes; kept alive for the life of the process.
    private static readonly WndProc Proc = StaticWndProc;
    private static readonly IntPtr ProcPtr = Marshal.GetFunctionPointerForDelegate(Proc);
    private static NativeWindow? _creating;

    public IntPtr Handle { get; private set; }
    public static IntPtr Instance { get; } = GetModuleHandleW(null);

    protected static void EnsureClass(string name, uint style, IntPtr cursor, IntPtr background)
    {
        if (!Registered.Add(name)) return;
        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            style = style,
            lpfnWndProc = ProcPtr,
            hInstance = Instance,
            hCursor = cursor,
            hbrBackground = background,
            lpszClassName = name,
        };
        if (RegisterClassExW(ref wc) == 0)
            throw new InvalidOperationException($"RegisterClassEx({name}) failed: {Marshal.GetLastWin32Error()}");
    }

    protected void Create(string cls, string? title, uint exStyle, uint style, int x, int y, int w, int h, IntPtr owner)
    {
        _creating = this;
        try
        {
            Handle = CreateWindowExW(exStyle, cls, title, style, x, y, w, h, owner, IntPtr.Zero, Instance, IntPtr.Zero);
        }
        finally { _creating = null; }
        if (Handle == IntPtr.Zero)
            throw new InvalidOperationException($"CreateWindowEx({cls}) failed: {Marshal.GetLastWin32Error()}");
        Live[Handle] = this;
    }

    protected abstract IntPtr WndProc(uint msg, IntPtr wParam, IntPtr lParam);

    protected IntPtr Default(uint msg, IntPtr wParam, IntPtr lParam) => DefWindowProcW(Handle, msg, wParam, lParam);

    private static IntPtr StaticWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (!Live.TryGetValue(hwnd, out var w))
        {
            // Messages sent during CreateWindowEx (WM_NCCREATE, WM_CREATE...) arrive before we know the handle.
            if (_creating is { } c) { c.Handle = hwnd; Live[hwnd] = c; w = c; }
            else return DefWindowProcW(hwnd, msg, wParam, lParam);
        }
        try
        {
            IntPtr r = w.WndProc(msg, wParam, lParam);
            if (msg == WM_DESTROY) Live.Remove(hwnd);
            return r;
        }
        catch (Exception ex)
        {
            Log.Error($"Unhandled exception in {w.GetType().Name} (msg 0x{msg:X4})", ex);
            if (msg == WM_DESTROY) Live.Remove(hwnd);
            return DefWindowProcW(hwnd, msg, wParam, lParam);
        }
    }
}
