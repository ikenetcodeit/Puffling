using System.Numerics;
using Puffling.Core.Animation;
using Puffling.Core.Behavior;
using Puffling.Core.Interaction;
using Puffling.Core.Movement;
using Puffling.Core.Rendering;
using Puffling.Core.Settings;

namespace Puffling.Core;

public readonly record struct FrameInputs(Vector2 Cursor, float UserIdleSeconds);

/// <summary>Where and what to draw. Left/Top/Size are physical px on the virtual desktop.</summary>
public readonly record struct RenderResult(FrameBuffer Frame, int Left, int Top, int Size);

/// <summary>
/// Platform-independent facade: the whole simulation (brain + motion + animation + rendering)
/// behind a tiny API. The Windows shell only has to feed it time, cursor and pointer events and
/// put its pixels on screen. Tests and the preview tool drive exactly the same object.
/// </summary>
public sealed class Companion
{
    private readonly CharacterRenderer _renderer = new();
    private Vector2 _lastCursor;
    private bool _haveCursor;

    public CompanionSettings Settings { get; private set; }
    public ScreenGeometry Geometry { get; private set; }
    public Motion Motion { get; } = new();
    public Animator Animator { get; }
    public Brain Brain { get; }
    public PointerTracker Pointer { get; } = new();

    public Companion(CompanionSettings settings, ScreenGeometry geometry, int? seed = null)
    {
        Settings = settings.Normalize();
        Geometry = geometry;
        var rng = seed is { } s ? new Random(s) : new Random();
        Animator = new Animator(rng);

        var wa = geometry.Primary.WorkArea;
        float dpi = geometry.Primary.Scale;
        Vector2 start = new(wa.Right - 120 * dpi, wa.Bottom - 60 * dpi);
        if (settings.LastX is { } x && settings.LastY is { } y) start = new Vector2(x, y);
        Motion.Teleport(start);

        Brain = new Brain(Motion, Animator, geometry, Settings, rng);
        RefreshScaleInputs();
        // Saved position might be on a monitor that no longer exists.
        if (!geometry.IsComfortablyVisible(Motion.Position, Brain.Margin))
            Motion.Teleport(geometry.NearestSafePoint(Motion.Position, Brain.Margin));
    }

    public Vector2 Position => Motion.Position;
    public MonitorInfo CurrentMonitor => Geometry.MonitorAt(Motion.Position);
    public BehaviorKind CurrentBehavior => Brain.Current.Kind;
    public bool IsSleeping => Brain.Current.Kind == BehaviorKind.Sleeping;

    /// <summary>Canvas (= window) edge length in physical px: the user's logical size × monitor DPI.</summary>
    public int CanvasSize => Math.Max(16, (int)MathF.Round(Settings.Scale * CurrentMonitor.Scale));

    public void ApplySettings(CompanionSettings s)
    {
        Settings = s.Normalize();
        Brain.Settings = Settings;
    }

    /// <summary>Call on WM_DISPLAYCHANGE / DPI change / monitor hot-plug.</summary>
    public void UpdateGeometry(ScreenGeometry g)
    {
        Geometry = g;
        Brain.Geometry = g;
        RefreshScaleInputs();
        // Completely off every monitor (e.g. its monitor was unplugged): snap back immediately.
        // Merely uncomfortable positions are handled smoothly by the Returning behaviour.
        if (!g.IsOnAnyMonitor(Motion.Position))
            Motion.Teleport(g.NearestSafePoint(Motion.Position, Brain.Margin));
    }

    public void Tick(float dt, in FrameInputs input)
    {
        dt = Math.Clamp(dt, 0f, 0.1f); // after sleep/hibernate or a debugger pause, don't jump
        if (dt > 0)
        {
            if (!_haveCursor) { _lastCursor = input.Cursor; _haveCursor = true; }
            Vector2 inst = (input.Cursor - _lastCursor) / dt;
            Brain.CursorVelocity = Vector2.Lerp(Brain.CursorVelocity, inst, 0.5f);
        }
        _lastCursor = input.Cursor;
        Brain.Cursor = input.Cursor;
        Brain.UserIdleSeconds = input.UserIdleSeconds;
        RefreshScaleInputs();

        while (Pointer.TryDequeue(out var e)) Brain.Handle(e);

        if (Settings.Paused) return;
        Animator.Intensity = Settings.AnimationIntensity;
        Brain.Tick(dt);
    }

    public void Execute(CompanionCommand c) => Brain.Execute(c);

    public RenderResult Render()
    {
        int size = CanvasSize;
        var fb = _renderer.Render(Animator.Current, Animator.TentaclePhase, Animator.ParticleTime, size,
                                  particles: !Settings.PerformanceMode);
        // Clip offsets are authored at 48 px; scale them with the character.
        Vector2 offset = Animator.WindowOffset * (size / 48f);
        int left = (int)MathF.Round(Motion.Position.X + offset.X - size / 2f);
        int top = (int)MathF.Round(Motion.Position.Y + offset.Y - size / 2f);
        return new RenderResult(fb, left, top, size);
    }

    private void RefreshScaleInputs()
    {
        float dpi = CurrentMonitor.Scale;
        Brain.DpiScale = dpi;
        // Keep the whole canvas plus the largest hop offset (≈6 logical px) inside the work area.
        Brain.Margin = CanvasSize / 2 + (int)MathF.Ceiling(6 * dpi * Settings.Scale / 48f) + 2;
        Pointer.DragThreshold = 4f * dpi;
        Pointer.StrokeLength = 3f * dpi;
    }
}
