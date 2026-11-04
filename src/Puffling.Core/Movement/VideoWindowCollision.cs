using Puffling.Core.Behavior;

namespace Puffling.Core.Movement;

/// <summary>
/// Keeps a companion from lingering inside a windowed (non-fullscreen) video's bounds. Wander,
/// Travel, and waiting-for-input already steer their own destinations clear of it
/// (<see cref="Brain.AvoidVideo"/>), but a behavior that doesn't choose a destination at all (an
/// idle flourish, Happy, Dance, ...) can still glide through on residual velocity if it's
/// interrupted mid-transit right as it crosses the window. The moment that happens, this pushes
/// the position back out to the nearest edge and reflects the velocity component that carried it
/// in — the same instant-bounce shape as <see cref="ScreenEdgeCollision"/>, just for an interior
/// rectangle instead of the screen boundary.
/// </summary>
public static class VideoWindowCollision
{
    private const float Restitution = 0.3f;

    public static void Bounce(Motion motion, VideoViewport? video)
    {
        if (video is not { Fullscreen: false } v || !v.Bounds.Contains(motion.Position)) return;

        var b = v.Bounds;
        var p = motion.Position;
        float distLeft = p.X - b.Left, distRight = b.Right - p.X, distTop = p.Y - b.Top, distBottom = b.Bottom - p.Y;
        float nearest = MathF.Min(MathF.Min(distLeft, distRight), MathF.Min(distTop, distBottom));

        var v2 = motion.Velocity;
        if (nearest == distLeft) { p.X = b.Left - 1; if (v2.X > 0) v2.X = -v2.X * Restitution; }
        else if (nearest == distRight) { p.X = b.Right + 1; if (v2.X < 0) v2.X = -v2.X * Restitution; }
        else if (nearest == distTop) { p.Y = b.Top - 1; if (v2.Y > 0) v2.Y = -v2.Y * Restitution; }
        else { p.Y = b.Bottom + 1; if (v2.Y < 0) v2.Y = -v2.Y * Restitution; }

        motion.Position = p;
        motion.Velocity = v2;
    }
}
