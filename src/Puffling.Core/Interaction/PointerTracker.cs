using System.Numerics;

namespace Puffling.Core.Interaction;

public enum InteractionKind { Click, DoubleClick, DragStart, DragMove, DragEnd, Petting, HoverStart, HoverEnd }

public readonly record struct InteractionEvent(InteractionKind Kind, Vector2 Position, Vector2 Velocity);

/// <summary>
/// Turns raw pointer messages (screen coordinates, physical px) into semantic gestures.
/// It knows nothing about Windows: the shell feeds it, the brain consumes its events.
///
/// Gestures:
///  * Click      – press + release without moving more than the drag threshold.
///  * DoubleClick– forwarded from the OS (respects the user's double-click speed setting).
///  * Drag       – press + movement beyond the threshold.
///  * Petting    – hovering with no button and stroking back and forth: three or more
///                 direction reversals (each after >= stroke-length travel) within 1.5 s.
/// </summary>
public sealed class PointerTracker
{
    private readonly Queue<InteractionEvent> _events = new();
    private readonly Queue<double> _reversals = new();

    private bool _down, _dragging, _hovering;
    private Vector2 _downPos, _lastPos;
    private double _downTime, _lastMoveTime, _lastPetEvent = double.NegativeInfinity;
    private Vector2 _dragVelocity;
    private bool _suppressNextClick;
    private int _strokeDir; // -1 / +1 along the dominant axis
    private float _strokeTravel;
    private int _strokeAxis; // 0 = x, 1 = y

    /// <summary>Physical pixels; set from DPI (≈ 4 logical px).</summary>
    public float DragThreshold { get; set; } = 4f;
    /// <summary>Physical pixels a stroke must travel before a reversal counts.</summary>
    public float StrokeLength { get; set; } = 3f;

    public bool IsDragging => _dragging;
    public bool IsHovering => _hovering;

    public bool TryDequeue(out InteractionEvent e) => _events.TryDequeue(out e);

    public void PointerDown(Vector2 p, double t)
    {
        _down = true; _dragging = false;
        _downPos = _lastPos = p;
        _downTime = _lastMoveTime = t;
        _dragVelocity = Vector2.Zero;
    }

    public void PointerMove(Vector2 p, double t)
    {
        double dt = Math.Max(1e-3, t - _lastMoveTime);
        Vector2 delta = p - _lastPos;

        if (_down)
        {
            if (!_dragging && Vector2.Distance(p, _downPos) > DragThreshold)
            {
                _dragging = true;
                _events.Enqueue(new(InteractionKind.DragStart, _downPos, Vector2.Zero));
            }
            if (_dragging)
            {
                // Exponentially smoothed velocity, used for tentacle dangling and the release throw.
                var inst = delta / (float)dt;
                _dragVelocity = Vector2.Lerp(_dragVelocity, inst, 0.35f);
                _events.Enqueue(new(InteractionKind.DragMove, p, _dragVelocity));
            }
        }
        else
        {
            if (!_hovering)
            {
                _hovering = true;
                _events.Enqueue(new(InteractionKind.HoverStart, p, Vector2.Zero));
            }
            TrackStroke(delta, t);
        }

        _lastPos = p;
        _lastMoveTime = t;
    }

    public void PointerUp(Vector2 p, double t)
    {
        if (!_down) return;
        _down = false;
        bool suppress = _suppressNextClick;
        _suppressNextClick = false;
        if (_dragging)
        {
            _dragging = false;
            // If the pointer stopped before release, there is no throw.
            var v = t - _lastMoveTime > 0.08 ? Vector2.Zero : _dragVelocity;
            _events.Enqueue(new(InteractionKind.DragEnd, p, v));
        }
        else if (suppress)
        {
            // Second release of a double-click: already reported as DoubleClick.
        }
        else if (t - _downTime < 0.8)
        {
            _events.Enqueue(new(InteractionKind.Click, p, Vector2.Zero));
        }
    }

    public void DoubleClick(Vector2 p, double t)
    {
        _events.Enqueue(new(InteractionKind.DoubleClick, p, Vector2.Zero));
        // A double-click also produces down/up; treat the second press as consumed.
        _down = true; _dragging = false; _downPos = p; _downTime = t; _lastPos = p; _lastMoveTime = t;
        _suppressNextClick = true;
    }

    /// <summary>Called by the shell when capture is lost (e.g. alt-tab during drag).</summary>
    public void Cancel(Vector2 p)
    {
        if (_dragging) _events.Enqueue(new(InteractionKind.DragEnd, p, Vector2.Zero));
        _down = false; _dragging = false;
    }

    public void PointerLeave(Vector2 p)
    {
        if (_down) return; // captured; leave is meaningless mid-drag
        if (_hovering) _events.Enqueue(new(InteractionKind.HoverEnd, p, Vector2.Zero));
        _hovering = false;
        _reversals.Clear();
        _strokeDir = 0; _strokeTravel = 0;
    }

    private void TrackStroke(Vector2 delta, double t)
    {
        if (delta == Vector2.Zero) return;
        int axis = MathF.Abs(delta.X) >= MathF.Abs(delta.Y) ? 0 : 1;
        float d = axis == 0 ? delta.X : delta.Y;
        int dir = Math.Sign(d);
        if (dir == 0) return;

        if (axis != _strokeAxis) { _strokeAxis = axis; _strokeDir = dir; _strokeTravel = MathF.Abs(d); return; }
        if (dir == _strokeDir) { _strokeTravel += MathF.Abs(d); return; }

        // Direction reversed: count it only if the previous stroke was long enough (filters jitter).
        if (_strokeTravel >= StrokeLength) _reversals.Enqueue(t);
        _strokeDir = dir;
        _strokeTravel = MathF.Abs(d);

        while (_reversals.Count > 0 && t - _reversals.Peek() > 1.5) _reversals.Dequeue();
        if (_reversals.Count >= 3 && t - _lastPetEvent > 0.5)
        {
            _lastPetEvent = t;
            _events.Enqueue(new(InteractionKind.Petting, _lastPos, Vector2.Zero));
        }
    }
}
