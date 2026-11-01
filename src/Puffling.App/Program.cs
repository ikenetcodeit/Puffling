using Puffling.App;
using Puffling.App.Native;
using Puffling.App.Platform;
using static Puffling.App.Native.Win32;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        // Must happen before any window exists. The manifest declares the same thing for the
        // published .exe; this call covers "dotnet Puffling.dll" dev runs where no manifest applies.
        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        using var mutex = new Mutex(initiallyOwned: true, @"Local\Puffling.DesktopCompanion", out bool firstInstance);
        if (!firstInstance)
        {
            MessageBoxW(IntPtr.Zero, "Puffling is already running. Look for it in the notification area (system tray).",
                        "Puffling", MB_OK | MB_ICONINFORMATION);
            return 0;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Unhandled exception", e.ExceptionObject as Exception);

        CompanionApp app;
        try { app = new CompanionApp(); }
        catch (Exception ex)
        {
            Log.Error("Startup failed", ex);
            MessageBoxW(IntPtr.Zero, $"Puffling could not start:\n{ex.Message}\n\nLog: {Log.FilePath}", "Puffling", MB_OK | MB_ICONERROR);
            return 1;
        }

        using (app)
        {
            while (GetMessageW(out MSG msg, IntPtr.Zero, 0, 0) > 0)
            {
                // Lets Tab / Esc / Enter work inside the settings panel.
                IntPtr dlg = app.SettingsWindowHandle;
                if (dlg != IntPtr.Zero && IsDialogMessageW(dlg, ref msg)) continue;
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);
            }
        }
        Log.Info("Exited cleanly.");
        return 0;
    }
}
