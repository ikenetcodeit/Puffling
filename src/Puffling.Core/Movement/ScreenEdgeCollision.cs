namespace Puffling.Core.Movement;

/// <summary>
/// Keeps free motion (most noticeably a post-flick glide) from carrying a companion far off
/// screen. The moment a position would no longer be comfortably visible on any monitor, it's
/// clamped back to the nearest safe point and the velocity component that caused the overshoot is
/// reflected inward — an instant bounce off the edge, rather than drifting out of sight for a
/// moment before the slower <c>ReturningBehavior</c> steers it back.
///
/// A multi-monitor layout is unaffected: <see cref="ScreenGeometry.IsComfortablyVisible"/> treats
/// adjacent monitors as one continuous safe area, so travelling from one display onto another
/// never counts as "off screen" and never bounces.
/// </summary>
public static class ScreenEdgeCollision
{
    /// <summary>How much speed survives the bounce — a soft thud, not a superball.</summary>
    private const float Restitution = 0.45f;

    public static void Bounce(Motion motion, ScreenGeometry geometry, int margin)
    {
        var before = motion.Position;
        if (geometry.IsComfortablyVisible(before, margin)) return;

        var area = geometry.MonitorAt(before).WorkArea.Deflate(margin);
        var after = area.Clamp(before);
        var v = motion.Velocity;
        if (after.X != before.X) v.X = -v.X * Restitution;
        if (after.Y != before.Y) v.Y = -v.Y * Restitution;

        motion.Position = after;
        motion.Velocity = v;
    }
}
