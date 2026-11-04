using System.Linq;
using System.Numerics;
using Puffling.Core.Animation;
using Puffling.Core.Character;

namespace Puffling.Core.Behavior;

public enum BehaviorKind
{
    Idle, Wander, Travel, Curious, Happy, Excited, Surprised, Dance, Affectionate,
    Follow, ComeHere, Drowsy, Sleeping, Waking, Returning, Dragged, Settling,
    PairPlay, TrioPlay, GroupChase, GroupSpar, GroupPong, GroupStare, Signature,
    Grooving, Watching, Coding, AgentBusy, Searching, Declined, WatchingMovie,
    AgentWaiting, AgentSucceeded, AgentFailed,
    Shy, Hovering, Mischievous,
    Humming, Inspecting, Aloof,
    Admiring, Checking, Challenging,
}

public static class BehaviorKinds
{
    /// <summary>A synchronized, choreographed routine (dance/chase/spar/pong/staring contest)
    /// where pets are deliberately kept at specific close offsets — collision pushing must leave
    /// these alone or it would fight the choreography.</summary>
    public static bool IsGroupPlay(this BehaviorKind kind) => kind is
        BehaviorKind.PairPlay or BehaviorKind.TrioPlay or BehaviorKind.GroupChase or
        BehaviorKind.GroupSpar or BehaviorKind.GroupPong or BehaviorKind.GroupStare;
}

/// <summary>
/// One state of the behaviour state machine. A behaviour owns BOTH what the character does
/// (motion) and how it looks (clip), so movement and animation can never disagree — e.g. there
/// is no way to be "sleeping" while a wander target is still pulling the character around.
/// </summary>
public abstract class Behavior
{
    public abstract BehaviorKind Kind { get; }
    /// <summary>Whether contextual reactions (curiosity, sleepiness, ...) may pre-empt this state.
    /// Direct user input can always pre-empt (the brain enforces that).</summary>
    public virtual bool Interruptible => true;
    public float Elapsed { get; private set; }

    internal void Tick(float dt) => Elapsed += dt;
    public virtual void Enter(Brain b) { }
    /// <summary>Returns false when finished; the brain then picks what comes next.</summary>
    public abstract bool Update(Brain b, float dt);
    public virtual void Exit(Brain b) { }
    public override string ToString() => Kind.ToString();
}

public sealed class IdleBehavior : Behavior
{
    private float _duration, _driftTimer;
    private Vector2 _anchor;
    public override BehaviorKind Kind => BehaviorKind.Idle;
    public override void Enter(Brain b)
    {
        _duration = b.RandomRange(4f, 9f) * b.Variant.IdleDuration() / b.Settings.ActivityFrequency;
        _anchor = b.Motion.Position;
        b.Motion.Release(b.Speed(80));
        b.Animator.Play(Clips.Idle, 0.5f);
    }
    public override bool Update(Brain b, float dt)
    {
        // Tiny lazy drift around the spot so idling never looks frozen.
        _driftTimer -= dt;
        if (_driftTimer <= 0 && b.CanMove)
        {
            _driftTimer = b.RandomRange(2.5f, 5f);
            var r = 5f * b.DpiScale;
            b.Motion.MoveTo(_anchor + new Vector2(b.RandomRange(-r, r), b.RandomRange(-r * 0.5f, r * 0.5f)), b.Speed(6), b.Speed(10));
        }
        return Elapsed < _duration;
    }
}

public sealed class WanderBehavior : Behavior
{
    public override BehaviorKind Kind => BehaviorKind.Wander;
    public override void Enter(Brain b)
    {
        var p = b.AvoidVideo(b.Geometry.RandomPointNear(b.Motion.Position, 40 * b.DpiScale, 170 * b.DpiScale, b.Margin, b.Rng));
        b.Motion.MoveTo(p, b.Speed(42) * b.Variant.WanderSpeed(), b.Speed(55));
        b.Animator.Play(Clips.Travel, 0.6f);
    }
    public override bool Update(Brain b, float dt) => !b.Motion.Arrived && Elapsed < 15f;
}

public sealed class TravelBehavior : Behavior
{
    private float _timeout;
    public override BehaviorKind Kind => BehaviorKind.Travel;
    public override void Enter(Brain b)
    {
        var from = b.Motion.Position;
        var p = b.AvoidVideo(b.Geometry.RandomFarPoint(from, 320 * b.DpiScale, b.Margin, b.Rng));
        float speed = b.Speed(165) * b.Variant.WanderSpeed();
        b.Motion.MoveTo(p, speed, b.Speed(120));
        b.Animator.Play(Clips.Travel, 0.6f);
        // A cross-monitor hop can be much farther than a same-screen stroll; size the timeout to
        // the actual distance so it has time to arrive instead of being cut off mid-flight and
        // handed to ReturningBehavior right as it crosses a display seam.
        _timeout = Math.Max(30f, Vector2.Distance(from, p) / Math.Max(1f, speed) * 1.5f + 5f);
    }
    public override bool Update(Brain b, float dt) => !b.Motion.Arrived && Elapsed < _timeout;
}

public sealed class CuriousBehavior : Behavior
{
    private readonly bool _atCursor;
    private float _duration;
    public CuriousBehavior(bool atCursor) => _atCursor = atCursor;
    public override BehaviorKind Kind => BehaviorKind.Curious;
    public override void Enter(Brain b)
    {
        _duration = b.RandomRange(2.5f, 5f) * b.Variant.Curiosity();
        b.Motion.Release(b.Speed(120));
        if (!_atCursor)
        {
            float a = b.RandomRange(0, MathF.Tau);
            b.LookOverride = new Vector2(MathF.Cos(a), MathF.Sin(a) * 0.6f);
        }
        b.Animator.Play(Clips.Curious, 0.35f);
    }
    public override bool Update(Brain b, float dt)
    {
        if (_atCursor)
        {
            float d = b.CursorDistanceLogical;
            if (d > 200) return false; // lost interest
            // Shy approach: drift a bit closer if the cursor is lingering, but never under it.
            if (b.CanMove && Elapsed > 1.2f && d > 90)
                b.Motion.MoveTo(b.PointNearCursor(70), b.Speed(30), b.Speed(40));
        }
        return Elapsed < _duration;
    }
    public override void Exit(Brain b) => b.LookOverride = null;
}

/// <summary>Plays one finite clip in place (Happy, Excited, Surprised, Dance, Settle...).</summary>
public sealed class ClipBehavior : Behavior
{
    private readonly BehaviorKind _kind;
    private readonly IClip _clip;
    private readonly float _blend;
    public ClipBehavior(BehaviorKind kind, IClip clip, float blend = 0.2f) { _kind = kind; _clip = clip; _blend = blend; }
    public override BehaviorKind Kind => _kind;
    public override bool Interruptible => _kind is not (BehaviorKind.Dance or BehaviorKind.Signature or BehaviorKind.Settling);
    public override void Enter(Brain b)
    {
        if (_kind != BehaviorKind.Settling) b.Motion.Release(b.Speed(200));
        b.Animator.Play(_clip, _blend, restartIfSame: true);
        if (_kind == BehaviorKind.Surprised && b.CanMove)
        {
            // Startle: a little hop away from the cursor.
            var away = b.Motion.Position - b.Cursor;
            if (away.LengthSquared() > 1) b.Motion.Velocity += Vector2.Normalize(away) * b.Speed(90);
        }
    }
    public override bool Update(Brain b, float dt) => !b.Animator.ClipFinished;
}

/// <summary>A shared dance: two mirror each other, while three circle a common centre.</summary>
public sealed class GroupPlayBehavior : Behavior
{
    private readonly Vector2 _centre;
    private readonly int _slot, _count;
    private readonly float _radius;
    public override BehaviorKind Kind => _count == 2 ? BehaviorKind.PairPlay : BehaviorKind.TrioPlay;
    public override bool Interruptible => false;

    public GroupPlayBehavior(Vector2 centre, int slot, int count, float radius)
    {
        if (count is not (2 or 3) || slot < 0 || slot >= count)
            throw new ArgumentOutOfRangeException(nameof(slot));
        _centre = centre;
        _slot = slot;
        _count = count;
        _radius = radius;
    }

    public override void Enter(Brain b)
    {
        b.Animator.Play(_count == 2 ? Clips.PairPlay(_slot) : Clips.TrioPlay(_slot), 0.35f);
    }

    public override bool Update(Brain b, float dt)
    {
        float danceTime = MathF.Max(0, Elapsed - 1.5f);
        Vector2 offset;
        if (_count == 2)
        {
            float angle = danceTime * MathF.Tau / 6f;
            float side = _slot == 0 ? -1f : 1f;
            offset = new Vector2(side * _radius * MathF.Cos(angle),
                                 side * _radius * 0.42f * MathF.Sin(angle));
        }
        else
        {
            float angle = -MathF.PI / 2f + _slot * MathF.Tau / 3f + danceTime * MathF.Tau / 8f;
            offset = new Vector2(_radius * MathF.Cos(angle), _radius * 0.65f * MathF.Sin(angle));
        }

        if (b.CanMove)
        {
            var target = b.Geometry.MonitorAt(_centre).WorkArea.Deflate(b.Margin).Clamp(_centre + offset * b.DpiScale);
            b.Motion.MoveTo(target, b.Speed(_count == 2 ? 100 : 115), b.Speed(220));
        }
        else b.Motion.Release(b.Speed(200));

        var toward = _centre - b.Motion.Position;
        b.LookOverride = toward.LengthSquared() > 1 ? Vector2.Normalize(toward) : Vector2.Zero;
        return Elapsed < (_count == 2 ? 7.5f : 9.5f);
    }

    public override void Exit(Brain b)
    {
        b.Motion.Release(b.Speed(170));
        b.LookOverride = null;
    }
}

/// <summary>Shared chase, gentle bounce sparring, or a three-pet pong rally.</summary>
public sealed class GroupGameBehavior : Behavior
{
    private readonly Vector2 _centre;
    private readonly int _slot, _count;
    private readonly float _radius;
    private readonly GroupGameKind _game;
    public override BehaviorKind Kind => _game switch
    {
        GroupGameKind.Chase => BehaviorKind.GroupChase,
        GroupGameKind.Spar => BehaviorKind.GroupSpar,
        GroupGameKind.Stare => BehaviorKind.GroupStare,
        _ => BehaviorKind.GroupPong,
    };
    public override bool Interruptible => false;

    public GroupGameBehavior(Vector2 centre, int slot, int count, float radius, GroupGameKind game)
    {
        if (count is not (2 or 3) || slot < 0 || slot >= count || (game == GroupGameKind.Pong && count != 3) ||
            (game == GroupGameKind.Stare && count != 2))
            throw new ArgumentOutOfRangeException(nameof(slot));
        _centre = centre; _slot = slot; _count = count; _radius = radius; _game = game;
    }

    public override void Enter(Brain b) => b.Animator.Play(Clips.GroupGame(_game, _slot), 0.35f);

    public override bool Update(Brain b, float dt)
    {
        float t = MathF.Max(0, Elapsed - 0.7f);
        Vector2 offset;
        float speed;
        switch (_game)
        {
            case GroupGameKind.Chase:
                float angle = t * 1.35f - _slot * MathF.Tau / _count;
                offset = new Vector2(_radius * MathF.Cos(angle), _radius * 0.6f * MathF.Sin(angle));
                speed = 150;
                break;
            case GroupGameKind.Spar:
                float a = _slot * MathF.Tau / _count;
                float distance = _radius * (0.50f + 0.50f * Hop(t * 3.4f + _slot * 0.4f));
                offset = new Vector2(distance * MathF.Cos(a), distance * 0.6f * MathF.Sin(a));
                speed = 125;
                break;
            case GroupGameKind.Stare:
                // Hold still, facing each other at close range.
                float side = _slot == 0 ? -1f : 1f;
                offset = new Vector2(side * _radius * 0.42f, 0f);
                speed = 45;
                break;
            default: // Three-pet pong: the third pet is the ball between two bobbing paddles.
                float ballY = _radius * 0.36f * MathF.Sin(t * 1.8f);
                offset = _slot switch
                {
                    0 => new Vector2(-_radius, ballY * 0.85f),
                    1 => new Vector2(_radius, ballY * 0.85f),
                    _ => new Vector2(_radius * 0.72f * MathF.Sin(t * 2.7f), ballY),
                };
                speed = _slot == 2 ? 180 : 100;
                break;
        }

        if (b.CanMove)
        {
            var target = b.Geometry.MonitorAt(_centre).WorkArea.Deflate(b.Margin).Clamp(_centre + offset * b.DpiScale);
            b.Motion.MoveTo(target, b.Speed(speed), b.Speed(380));
        }
        else b.Motion.Release(b.Speed(200));

        Vector2 look = _centre - b.Motion.Position;
        b.LookOverride = look.LengthSquared() > 1 ? Vector2.Normalize(look) : Vector2.Zero;
        return Elapsed < 8.5f;
    }

    public override void Exit(Brain b)
    {
        b.Motion.Release(b.Speed(170));
        b.LookOverride = null;
    }

    private static float Hop(float x) => MathF.Abs(MathF.Sin(x));
}

/// <summary>
/// A fullscreen video was detected. On a multi-monitor rig, an interested companion already on
/// the video's monitor relocates to another display and faces back toward it; elsewhere it just
/// settles and watches. Ends itself the moment the video stops being fullscreen (<see cref="Brain.Video"/>),
/// so it never needs an external interrupt to wind down.
/// </summary>
public sealed class WatchingMovieBehavior : Behavior
{
    public override BehaviorKind Kind => BehaviorKind.WatchingMovie;
    public override bool Interruptible => false; // takes priority over ordinary wandering/social picks
    public override void Enter(Brain b) => b.Animator.Play(Clips.Watching, 0.5f);

    public override bool Update(Brain b, float dt)
    {
        if (b.Video is not { Fullscreen: true } v) return false;

        if (b.Geometry.Monitors.Count > 1)
        {
            var videoMonitor = b.Geometry.MonitorAt(v.Bounds.Center);
            var here = b.Geometry.MonitorAt(b.Motion.Position);
            if (b.CanMove && here.Id == videoMonitor.Id)
            {
                // Only ever cross to a monitor the straight line actually stays visible on —
                // otherwise the safety check mid-transit would hand this off to ReturningBehavior
                // and undo the move.
                var other = b.Geometry.Monitors.FirstOrDefault(m => m.Id != videoMonitor.Id &&
                    b.Geometry.PathIsVisible(b.Motion.Position, m.WorkArea.Deflate(b.Margin).Center));
                if (other != null) b.Motion.MoveTo(other.WorkArea.Deflate(b.Margin).Center, b.Speed(130), b.Speed(200));
                else b.Motion.Release(b.Speed(40));
            }
            else b.Motion.Release(b.Speed(40));

            Vector2 toward = videoMonitor.Bounds.Center - b.Motion.Position;
            b.LookOverride = toward.LengthSquared() > 1 ? Vector2.Normalize(toward) : Vector2.Zero;
        }
        else b.Motion.Release(b.Speed(40));

        return true;
    }

    public override void Exit(Brain b)
    {
        b.LookOverride = null;
        b.Motion.Release(b.Speed(170));
    }
}

/// <summary>
/// Ambient reaction while an agent lifecycle event reports it's waiting on the user. Most
/// variants just hold the impatient pose a while; Green's bolder, more restless temperament means
/// it sometimes gives up on waiting and wanders off a short distance instead, rather than holding
/// still — the "green wandering off" beat the roadmap calls for.
/// </summary>
public sealed class AgentWaitingBehavior : Behavior
{
    public override BehaviorKind Kind => BehaviorKind.AgentWaiting;
    private bool _wanderedOff;
    private float _duration;

    public override void Enter(Brain b)
    {
        _wanderedOff = b.Variant == CompanionVariant.Green && b.CanMove && b.Rng.NextDouble() < 0.5;
        if (_wanderedOff)
        {
            var p = b.AvoidVideo(b.Geometry.RandomPointNear(b.Motion.Position, 60 * b.DpiScale, 220 * b.DpiScale, b.Margin, b.Rng));
            b.Motion.MoveTo(p, b.Speed(55) * b.Variant.WanderSpeed(), b.Speed(70));
            b.Animator.Play(Clips.Travel, 0.4f);
            _duration = 8f;
        }
        else
        {
            b.Motion.Release(b.Speed(40));
            b.Animator.Play(Clips.AgentWaiting, 0.3f);
            _duration = b.RandomRange(4f, 7f);
        }
    }

    public override bool Update(Brain b, float dt) => _wanderedOff ? !b.Motion.Arrived && Elapsed < _duration : Elapsed < _duration;
}

public sealed class AffectionateBehavior : Behavior
{
    public double LastPet;
    public override BehaviorKind Kind => BehaviorKind.Affectionate;
    public override void Enter(Brain b)
    {
        LastPet = b.Now;
        b.Motion.Release(b.Speed(150));
        b.Animator.Play(Clips.Affection, 0.3f);
    }
    public override bool Update(Brain b, float dt) => b.Now - LastPet < (b.Variant switch
    {
        CompanionVariant.Blue => 3.2,
        CompanionVariant.Green => 1.9,
        _ => 2.5,
    });
}

public sealed class FollowBehavior : Behavior
{
    private readonly bool _indefinite;
    private float _duration;
    public FollowBehavior(bool indefinite) => _indefinite = indefinite;
    public override BehaviorKind Kind => BehaviorKind.Follow;
    public override void Enter(Brain b)
    {
        _duration = b.RandomRange(6f, 11f);
        b.Animator.Play(Clips.Follow, 0.4f);
    }
    public override bool Update(Brain b, float dt)
    {
        if (!b.CanMove) return false;
        // Stay beside the cursor, never under it, so we don't intercept the user's clicks.
        b.Motion.MoveTo(b.PointNearCursor(58), b.Speed(230), b.Speed(380));
        return _indefinite ? b.Settings.FollowCursor : Elapsed < _duration;
    }
}

public sealed class ComeHereBehavior : Behavior
{
    public override BehaviorKind Kind => BehaviorKind.ComeHere;
    public override void Enter(Brain b) => b.Animator.Play(Clips.Follow, 0.3f);
    public override bool Update(Brain b, float dt)
    {
        b.Motion.MoveTo(b.PointNearCursor(56), b.Speed(280), b.Speed(450));
        return !(b.Motion.Arrived || Elapsed > 8f);
    }
    public override void Exit(Brain b) => b.QueueNext(BehaviorKind.Happy);
}

public sealed class DrowsyBehavior : Behavior
{
    public override BehaviorKind Kind => BehaviorKind.Drowsy;
    public override void Enter(Brain b)
    {
        b.Motion.Release(b.Speed(60));
        b.Animator.Play(Clips.Drowsy, 0.8f);
    }
    public override bool Update(Brain b, float dt) => !b.Animator.ClipFinished;
    public override void Exit(Brain b) => b.QueueNext(BehaviorKind.Sleeping);
}

public sealed class SleepingBehavior : Behavior
{
    private float _duration;
    public override BehaviorKind Kind => BehaviorKind.Sleeping;
    public override void Enter(Brain b)
    {
        _duration = b.RandomRange(90f, 240f);
        b.Motion.Release(b.Speed(40));
        b.Animator.Play(Clips.Sleeping, 1.2f);
    }
    public override bool Update(Brain b, float dt) => b.Settings.SleepMode || Elapsed < _duration;
    public override void Exit(Brain b) => b.Sleepiness = 0f;
}

public sealed class ReturningBehavior : Behavior
{
    public override BehaviorKind Kind => BehaviorKind.Returning;
    public override bool Interruptible => false;
    public override void Enter(Brain b) => b.Animator.Play(Clips.Travel, 0.3f);
    public override bool Update(Brain b, float dt)
    {
        // Re-evaluated every frame: monitors may change while we are on the way.
        b.Motion.MoveTo(b.Geometry.NearestSafePoint(b.Motion.Position, b.Margin), b.Speed(240), b.Speed(400));
        return !b.Motion.Arrived && Elapsed < 10f;
    }
    public override void Exit(Brain b)
    {
        // Safety net: if steering could not get us back (e.g. absurd geometry), snap.
        if (!b.Geometry.IsComfortablyVisible(b.Motion.Position, b.Margin))
            b.Motion.Teleport(b.Geometry.NearestSafePoint(b.Motion.Position, b.Margin));
    }
}

public sealed class DraggedBehavior : Behavior
{
    public Vector2 GrabOffset;
    public Vector2 DragVelocity;
    public bool Released;
    public override BehaviorKind Kind => BehaviorKind.Dragged;
    public override bool Interruptible => false;
    public override void Enter(Brain b)
    {
        GrabOffset = b.Motion.Position - b.Cursor;
        b.Motion.Teleport(b.Motion.Position);
        b.Animator.Play(Clips.Dragged, 0.12f);
    }
    public override bool Update(Brain b, float dt) => !Released;
}
