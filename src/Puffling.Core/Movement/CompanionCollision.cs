using System.Numerics;
using Puffling.Core.Behavior;

namespace Puffling.Core.Movement;

/// <summary>
/// Keeps companions from overlapping and gives them a soft, floaty shove when they collide —
/// most noticeably when one is flicked into another. Pure position/velocity correction: it never
/// touches behavior state, so a colliding pair keeps doing whatever it was doing (wandering,
/// idling, ...) and simply gets pushed apart. Choreographed group routines (dance/chase/spar/
/// pong/staring contest) are skipped since those already keep pets at deliberate close offsets.
/// </summary>
public static class CompanionCollision
{
    /// <summary>Collision radius as a fraction of a companion's canvas size — smaller than the
    /// full icon box since the rendered character doesn't fill every corner of it.</summary>
    private const float RadiusFactor = 0.32f;

    /// <summary>Coefficient of restitution: 1 = a perfectly elastic billiard-ball bounce (swaps
    /// velocities outright, felt too sharp/strong), 0 = no bounce at all, both just stop pushing
    /// into each other. A soft plush toy lands in between.</summary>
    private const float Restitution = 0.35f;

    public static void Resolve(IReadOnlyList<Companion> companions)
    {
        for (int i = 0; i < companions.Count; i++)
            for (int j = i + 1; j < companions.Count; j++)
                ResolvePair(companions[i], companions[j]);
    }

    private static void ResolvePair(Companion a, Companion b)
    {
        if (a.CurrentBehavior.IsGroupPlay() || b.CurrentBehavior.IsGroupPlay()) return;

        float minDist = (a.CanvasSize + b.CanvasSize) * RadiusFactor;
        Vector2 delta = b.Motion.Position - a.Motion.Position;
        float dist = delta.Length();
        if (dist >= minDist) return;

        Vector2 normal = dist > 0.0001f ? delta / dist : new Vector2(1f, 0f);
        float overlap = minDist - dist;

        // A pet being actively dragged is glued to the cursor and must never be repositioned by
        // a collision; it still shoves whatever it hits.
        bool aPinned = a.CurrentBehavior == BehaviorKind.Dragged;
        bool bPinned = b.CurrentBehavior == BehaviorKind.Dragged;

        if (!aPinned && !bPinned)
        {
            a.Motion.Position -= normal * (overlap * 0.5f);
            b.Motion.Position += normal * (overlap * 0.5f);
        }
        else if (aPinned && !bPinned) b.Motion.Position += normal * overlap;
        else if (bPinned && !aPinned) a.Motion.Position -= normal * overlap;

        // A dragged pet's own Motion.Velocity is stale (its position is driven directly by the
        // cursor); use the drag gesture's actual velocity for the impulse it imparts instead.
        Vector2 va = a.Brain.Current is DraggedBehavior da ? da.DragVelocity : a.Motion.Velocity;
        Vector2 vb = b.Brain.Current is DraggedBehavior db ? db.DragVelocity : b.Motion.Velocity;
        float van = Vector2.Dot(va, normal);
        float vbn = Vector2.Dot(vb, normal);
        if (vbn - van >= 0f) return; // already separating

        // Equal-mass collision with restitution: at Restitution=1 this swaps the along-normal
        // components outright (a hard billiard bounce); lower values blend toward both simply
        // sharing the average speed (a soft, floaty bump) instead.
        float share = (1f + Restitution) * 0.5f * (van - vbn);
        if (!aPinned) a.Motion.Velocity = va - normal * share;
        if (!bPinned) b.Motion.Velocity = vb + normal * share;
    }
}
