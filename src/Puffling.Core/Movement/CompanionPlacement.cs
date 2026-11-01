using System.Numerics;

namespace Puffling.Core.Movement;

/// <summary>Places a newly spawned companion near another one without stacking them.</summary>
public static class CompanionPlacement
{
    private static readonly Vector2[] Offsets =
    [
        new(-100, 0), new(100, 0), new(0, -100), new(0, 100),
        new(-140, -70), new(140, -70), new(-140, 70), new(140, 70),
    ];

    public static Vector2 Find(ScreenGeometry geometry, Vector2 anchor, IReadOnlyList<Vector2> occupied, int margin)
    {
        float scale = geometry.MonitorAt(anchor).Scale;
        Vector2 best = geometry.NearestSafePoint(anchor + Offsets[0] * scale, margin);
        float bestSeparation = -1;
        foreach (var offset in Offsets)
        {
            Vector2 candidate = geometry.NearestSafePoint(anchor + offset * scale, margin);
            float separation = occupied.Count == 0 ? float.MaxValue : occupied.Min(p => Vector2.DistanceSquared(p, candidate));
            if (separation > bestSeparation) { best = candidate; bestSeparation = separation; }
        }
        return best;
    }
}
