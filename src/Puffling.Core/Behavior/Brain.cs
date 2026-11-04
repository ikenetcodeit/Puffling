using System.Numerics;
using Puffling.Core.Animation;
using Puffling.Core.Character;
using Puffling.Core.Interaction;
using Puffling.Core.Movement;
using Puffling.Core.Settings;

namespace Puffling.Core.Behavior;

public enum CompanionCommand { Dance, Sleep, Wake, ComeHere, ResetPosition, Happy, Signature }

/// <summary>
/// The behaviour state machine ("personality").
///
/// Priority order, evaluated every tick:
///   1. Direct user input (drag, click, pet, double-click) — always wins.
///   2. Safety: get back on-screen (Returning).
///   3. Contextual reactions — cursor nearby, cursor rushing in, sleepiness, follow mode.
///   4. Autonomous choice when the current activity ends — weighted random with cooldowns,
///      a repeat penalty, and weights shaped by context (user busy => calmer).
/// After any activity the character goes back to Idle, so the baseline personality is calm
/// with bursts of play.
/// </summary>
public sealed class Brain
{
    public Random Rng { get; }
    public Motion Motion { get; }
    public Animator Animator { get; }
    public ScreenGeometry Geometry { get; set; }
    public CompanionSettings Settings { get; set; }
    public CompanionVariant Variant { get; }

    // World inputs, refreshed by the Companion each tick (physical px).
    public Vector2 Cursor;
    public Vector2 CursorVelocity;
    public float UserIdleSeconds;
    public float DpiScale = 1f;
    public int Margin = 30;
    public double Now { get; private set; }

    public float Sleepiness;
    public Vector2? LookOverride;
    public Behavior Current { get; private set; }

    private SystemActivity _systemActivity;
    /// <summary>What the host app thinks the user is doing right now (set by the App, polled
    /// off the render loop). Changing it reacts immediately if the character is free to be
    /// interrupted, and otherwise biases the next few idle choices.</summary>
    public SystemActivity SystemActivity
    {
        get => _systemActivity;
        set
        {
            if (_systemActivity == value) return;
            _systemActivity = value;
            // None and AgentIdle both mean "nothing noteworthy" — no pose change to react with.
            if (value is not (SystemActivity.None or SystemActivity.AgentIdle) && Current.Interruptible &&
                Current.Kind is BehaviorKind.Idle or BehaviorKind.Wander or BehaviorKind.Curious)
                Interrupt(Create(ActivityBehaviorKind(value)));
        }
    }

    private static BehaviorKind ActivityBehaviorKind(SystemActivity a) => a switch
    {
        SystemActivity.Music => BehaviorKind.Grooving,
        SystemActivity.Movie => BehaviorKind.Watching,
        SystemActivity.Coding => BehaviorKind.Coding,
        SystemActivity.AgentRunning => BehaviorKind.AgentBusy,
        SystemActivity.AgentWaitingForInput => BehaviorKind.AgentWaiting,
        SystemActivity.AgentSucceeded => BehaviorKind.AgentSucceeded,
        SystemActivity.AgentFailed => BehaviorKind.AgentFailed,
        _ => BehaviorKind.Idle,
    };

    /// <summary>How many companions (including this one) are currently visible, set by the host
    /// app each frame. Drives the solo "looking for someone" reaction — it never fires with
    /// company around.</summary>
    public int CompanionsPresent { get; set; } = 1;

    private VideoViewport? _video;
    /// <summary>A detected video region, set by the host app. Changing from "no fullscreen video"
    /// to "fullscreen video" rolls a per-variant interest check and, on success, starts
    /// <see cref="WatchingMovieBehavior"/>; clearing it (or it becoming non-fullscreen) ends that
    /// behavior. Not every companion joins — Blue is most drawn to it, Green least.</summary>
    public VideoViewport? Video
    {
        get => _video;
        set
        {
            if (_video.Equals(value)) return;
            bool wasFullscreen = _video is { Fullscreen: true };
            _video = value;
            if (value is { Fullscreen: true } && !wasFullscreen)
            {
                double interest = Variant switch { CompanionVariant.Blue => 0.8, CompanionVariant.Red => 0.5, _ => 0.2 };
                if (Rng.NextDouble() < interest && Current.Interruptible &&
                    Current.Kind is BehaviorKind.Idle or BehaviorKind.Wander or BehaviorKind.Curious)
                    Interrupt(new WatchingMovieBehavior());
            }
            else if (value is not { Fullscreen: true } && Current.Kind == BehaviorKind.WatchingMovie)
                Interrupt(new IdleBehavior());
        }
    }

    /// <summary>Nudges a candidate movement target outside a non-fullscreen video's window bounds,
    /// so wandering/travel never cross into it. A no-op when there's no windowed video.</summary>
    public Vector2 AvoidVideo(Vector2 p)
    {
        if (Video is not { Fullscreen: false } v || !v.Bounds.Contains(p)) return p;
        float dl = p.X - v.Bounds.Left, dr = v.Bounds.Right - p.X, dt = p.Y - v.Bounds.Top, db = v.Bounds.Bottom - p.Y;
        float min = MathF.Min(MathF.Min(dl, dr), MathF.Min(dt, db));
        Vector2 pushed = min == dl ? new Vector2(v.Bounds.Left - 8, p.Y)
            : min == dr ? new Vector2(v.Bounds.Right + 8, p.Y)
            : min == dt ? new Vector2(p.X, v.Bounds.Top - 8)
            : new Vector2(p.X, v.Bounds.Bottom + 8);
        return Geometry.NearestSafePoint(pushed, Margin);
    }

    /// <summary>Raised when the brain changes a persisted setting itself (e.g. waking leaves sleep mode).</summary>
    public event Action? SettingsMutated;
    public event Action<BehaviorKind>? BehaviorChanged;

    private BehaviorKind? _queued;
    private BehaviorKind _lastChosen = BehaviorKind.Idle;
    private readonly Dictionary<BehaviorKind, double> _lastStarted = new();
    private readonly List<double> _recentClicks = new();

    private static readonly Dictionary<BehaviorKind, float> Cooldowns = new()
    {
        [BehaviorKind.Wander] = 4, [BehaviorKind.Travel] = 25, [BehaviorKind.Happy] = 15,
        [BehaviorKind.Curious] = 6, [BehaviorKind.Excited] = 40, [BehaviorKind.Dance] = 120,
        [BehaviorKind.Follow] = 60, [BehaviorKind.Surprised] = 30,
        [BehaviorKind.Signature] = 28,
        [BehaviorKind.Grooving] = 18, [BehaviorKind.Watching] = 22,
        [BehaviorKind.Coding] = 18, [BehaviorKind.AgentBusy] = 12,
        [BehaviorKind.AgentWaiting] = 15, [BehaviorKind.AgentSucceeded] = 20, [BehaviorKind.AgentFailed] = 20,
        [BehaviorKind.Searching] = 50,
        [BehaviorKind.Shy] = 45, [BehaviorKind.Hovering] = 45, [BehaviorKind.Mischievous] = 45,
        [BehaviorKind.Humming] = 45, [BehaviorKind.Inspecting] = 45, [BehaviorKind.Aloof] = 45,
        [BehaviorKind.Admiring] = 45, [BehaviorKind.Checking] = 45, [BehaviorKind.Challenging] = 45,
    };

    public Brain(Motion motion, Animator animator, ScreenGeometry geometry, CompanionSettings settings, Random rng,
        CompanionVariant variant = CompanionVariant.Red)
    {
        Motion = motion; Animator = animator; Geometry = geometry; Settings = settings; Rng = rng; Variant = variant;
        Current = new IdleBehavior();
        Current.Enter(this);
    }

    // ---------------------------------------------------------------- helpers used by behaviours

    /// <summary>Autonomous movement allowed? (User-initiated moves ignore this.)</summary>
    public bool CanMove => Settings.AutonomousMovement && !Settings.FreezePosition;
    public bool UserBusy => UserIdleSeconds < 3f;
    public float Speed(float logicalPerSecond) =>
        logicalPerSecond * DpiScale * Settings.MovementSpeed * (UserBusy ? 0.8f : 1f) * VideoSlowdown();

    /// <summary>Quieter movement while standing inside a non-fullscreen video's window bounds.</summary>
    private float VideoSlowdown() => Video is { Fullscreen: false } v && v.Bounds.Contains(Motion.Position) ? 0.45f : 1f;
    public float CursorDistanceLogical => Vector2.Distance(Cursor, Motion.Position) / DpiScale;
    public float RandomRange(float a, float b) => a + (float)Rng.NextDouble() * (b - a);
    public void QueueNext(BehaviorKind k) => _queued = k;

    /// <summary>A spot beside the cursor (on the side we're already on) — never under it.</summary>
    public Vector2 PointNearCursor(float logicalOffset)
    {
        float side = Motion.Position.X < Cursor.X ? -1f : 1f;
        var p = Cursor + new Vector2(side * logicalOffset, logicalOffset * 0.35f) * DpiScale;
        return Geometry.NearestSafePoint(p, Margin);
    }

    public bool IsAsleepish => Current.Kind is BehaviorKind.Sleeping or BehaviorKind.Drowsy;

    // ---------------------------------------------------------------- main loop

    public void Tick(float dt)
    {
        Now += dt;
        if (Current.Kind != BehaviorKind.Sleeping)
        {
            float rate = 1f / 600f * (UserIdleSeconds > 120f ? 4f : 1f);
            Sleepiness = Math.Min(1.5f, Sleepiness + dt * rate);
        }

        EvaluateContext();

        Current.Tick(dt);
        if (!Current.Update(this, dt)) Finish();

        if (Current.Kind != BehaviorKind.Dragged) Motion.Update(dt);
        Animator.Update(dt, BuildClipContext());
    }

    private void EvaluateContext()
    {
        var k = Current.Kind;
        // WatchingMovie and Travel deliberately cross monitors themselves (path-validated); the
        // generic off-screen safety net would otherwise hand them to ReturningBehavior mid-transit
        // and undo the move right where two monitors' deflated safe areas touch.
        if (k is BehaviorKind.Dragged or BehaviorKind.Returning or BehaviorKind.Settling
            or BehaviorKind.WatchingMovie or BehaviorKind.Travel) return;

        if (!Geometry.IsComfortablyVisible(Motion.Position, Margin)) { Interrupt(new ReturningBehavior()); return; }
        if (!Current.Interruptible) return;

        if (Settings.SleepMode && k is not (BehaviorKind.Sleeping or BehaviorKind.Drowsy or BehaviorKind.Waking))
        { Interrupt(new DrowsyBehavior()); return; }

        if (k == BehaviorKind.Idle)
        {
            if (Settings.AutoSleep && Sleepiness >= 1f) { Interrupt(new DrowsyBehavior()); return; }
            if (Settings.FollowCursor && CanMove) { Interrupt(new FollowBehavior(indefinite: true)); return; }
        }

        if (k is BehaviorKind.Idle or BehaviorKind.Wander or BehaviorKind.Curious)
        {
            float d = CursorDistanceLogical;
            Vector2 away = Motion.Position - Cursor;
            bool approaching = Vector2.Dot(CursorVelocity, away) > 0;
            if (d < 90 && approaching && CursorVelocity.Length() > 2200 * DpiScale && OffCooldown(BehaviorKind.Surprised))
            { Interrupt(Create(BehaviorKind.Surprised)); return; }

            if (k != BehaviorKind.Curious && d is > 30 and < 120 && CursorVelocity.Length() > 20 * DpiScale
                && OffCooldown(BehaviorKind.Curious))
            { Interrupt(new CuriousBehavior(atCursor: true)); return; }
        }
    }

    private void Finish()
    {
        Current.Exit(this);
        BehaviorKind next = _queued ?? ChooseNext();
        _queued = null;
        Begin(Create(next));
    }

    /// <summary>Pre-empt the current behaviour. Anything it queued on exit is discarded.</summary>
    public void Interrupt(Behavior next)
    {
        Current.Exit(this);
        _queued = null;
        Begin(next);
    }

    private void Begin(Behavior b)
    {
        Current = b;
        _lastStarted[b.Kind] = Now;
        b.Enter(this);
        BehaviorChanged?.Invoke(b.Kind);
    }

    private bool OffCooldown(BehaviorKind k) =>
        !_lastStarted.TryGetValue(k, out var t) || !Cooldowns.TryGetValue(k, out var cd) || Now - t >= cd;

    internal BehaviorKind ChooseNext()
    {
        if (!Geometry.IsComfortablyVisible(Motion.Position, Margin)) return BehaviorKind.Returning;
        // After any activity, return to calm idling.
        if (Current.Kind != BehaviorKind.Idle) return BehaviorKind.Idle;

        float f = Settings.ActivityFrequency;
        bool busy = UserBusy;
        var options = new List<(BehaviorKind k, float w)>
        {
            (BehaviorKind.Idle, 1.5f),
            (BehaviorKind.Wander, CanMove ? 2.6f * f * (busy ? 0.6f : 1f) : 0f),
            (BehaviorKind.Travel, CanMove ? 0.9f * f * (busy ? 0.25f : 1f) : 0f),
            (BehaviorKind.Happy, 0.7f * f * Variant.Playfulness()),
            (BehaviorKind.Curious, 0.8f * f * Variant.Curiosity()),
            (BehaviorKind.Excited, 0.25f * f * Variant.Boldness()),
            (BehaviorKind.Dance, 0.15f * f * Variant.Playfulness() * (busy ? 0.3f : 1f)),
            (BehaviorKind.Signature, 0.22f * f * Variant.Playfulness() * (busy ? 0.4f : 1f)),
            (BehaviorKind.Follow, CanMove && !Settings.FollowCursor ? 0.35f * f : 0f),
            (BehaviorKind.Grooving, SystemActivity == SystemActivity.Music ? 1.0f * f : 0f),
            (BehaviorKind.Watching, SystemActivity == SystemActivity.Movie ? 1.0f * f : 0f),
            (BehaviorKind.Coding, SystemActivity == SystemActivity.Coding ? 0.9f * f : 0f),
            (BehaviorKind.AgentBusy, SystemActivity == SystemActivity.AgentRunning ? 1.3f * f : 0f),
            (BehaviorKind.AgentWaiting, SystemActivity == SystemActivity.AgentWaitingForInput ? 1.3f * f : 0f),
            (BehaviorKind.AgentSucceeded, SystemActivity == SystemActivity.AgentSucceeded ? 1.5f * f : 0f),
            (BehaviorKind.AgentFailed, SystemActivity == SystemActivity.AgentFailed ? 1.5f * f : 0f),
            (BehaviorKind.Searching, CompanionsPresent <= 1 ? 0.6f * f : 0f),
            (BehaviorKind.Shy, Variant == CompanionVariant.Blue ? 0.18f * f : 0f),
            (BehaviorKind.Hovering, Variant == CompanionVariant.Red ? 0.18f * f : 0f),
            (BehaviorKind.Mischievous, Variant == CompanionVariant.Green ? 0.18f * f : 0f),
            (BehaviorKind.Humming, Variant == CompanionVariant.Blue ? 0.18f * f : 0f),
            (BehaviorKind.Inspecting, Variant == CompanionVariant.Red ? 0.18f * f : 0f),
            (BehaviorKind.Aloof, Variant == CompanionVariant.Green ? 0.18f * f : 0f),
            (BehaviorKind.Admiring, Variant == CompanionVariant.Blue ? 0.18f * f : 0f),
            (BehaviorKind.Checking, Variant == CompanionVariant.Red ? 0.18f * f : 0f),
            (BehaviorKind.Challenging, Variant == CompanionVariant.Green ? 0.18f * f : 0f),
        };

        float total = 0;
        for (int i = 0; i < options.Count; i++)
        {
            var (k, w) = options[i];
            if (!OffCooldown(k)) w = 0;
            if (k == _lastChosen && k != BehaviorKind.Idle) w *= 0.35f; // avoid repetition
            options[i] = (k, w);
            total += w;
        }
        float r = RandomRange(0, total);
        foreach (var (k, w) in options)
        {
            if (r < w) { _lastChosen = k; return k; }
            r -= w;
        }
        return BehaviorKind.Idle;
    }

    public Behavior Create(BehaviorKind k) => k switch
    {
        BehaviorKind.Idle => new IdleBehavior(),
        BehaviorKind.Wander => new WanderBehavior(),
        BehaviorKind.Travel => new TravelBehavior(),
        BehaviorKind.Curious => new CuriousBehavior(atCursor: false),
        BehaviorKind.Happy => new ClipBehavior(k, Clips.Happy),
        BehaviorKind.Excited => new ClipBehavior(k, Clips.Excited),
        BehaviorKind.Surprised => new ClipBehavior(k, Clips.Surprised, 0.08f),
        BehaviorKind.Dance => new ClipBehavior(k, Clips.Dance, 0.3f),
        BehaviorKind.Signature => new ClipBehavior(k, Clips.Signature(Variant), 0.3f),
        BehaviorKind.Affectionate => new AffectionateBehavior(),
        BehaviorKind.Follow => new FollowBehavior(indefinite: false),
        BehaviorKind.ComeHere => new ComeHereBehavior(),
        BehaviorKind.Drowsy => new DrowsyBehavior(),
        BehaviorKind.Sleeping => new SleepingBehavior(),
        BehaviorKind.Waking => new ClipBehavior(k, Clips.Waking, 0.4f),
        BehaviorKind.Returning => new ReturningBehavior(),
        BehaviorKind.Settling => new ClipBehavior(k, Clips.Settle, 0.1f),
        BehaviorKind.Dragged => new DraggedBehavior(),
        BehaviorKind.Grooving => new ClipBehavior(k, Clips.Grooving, 0.3f),
        BehaviorKind.Watching => new ClipBehavior(k, Clips.Watching, 0.4f),
        BehaviorKind.Coding => new ClipBehavior(k, Clips.Coding, 0.4f),
        BehaviorKind.AgentBusy => new ClipBehavior(k, Clips.AgentBusy, 0.25f),
        BehaviorKind.AgentWaiting => new AgentWaitingBehavior(),
        BehaviorKind.AgentSucceeded => new ClipBehavior(k, Clips.AgentSucceeded, 0.25f),
        BehaviorKind.AgentFailed => new ClipBehavior(k, Clips.AgentFailed, 0.25f),
        BehaviorKind.Searching => new ClipBehavior(k, Clips.Searching, 0.4f),
        BehaviorKind.Declined => new ClipBehavior(k, Clips.Declined, 0.3f),
        BehaviorKind.Shy => new ClipBehavior(k, Clips.Shy, 0.3f),
        BehaviorKind.Hovering => new ClipBehavior(k, Clips.Hovering, 0.4f),
        BehaviorKind.Mischievous => new ClipBehavior(k, Clips.Mischievous, 0.3f),
        BehaviorKind.Humming => new ClipBehavior(k, Clips.Humming, 0.4f),
        BehaviorKind.Inspecting => new ClipBehavior(k, Clips.Inspecting, 0.4f),
        BehaviorKind.Aloof => new ClipBehavior(k, Clips.Aloof, 0.3f),
        BehaviorKind.Admiring => new ClipBehavior(k, Clips.Admiring, 0.3f),
        BehaviorKind.Checking => new ClipBehavior(k, Clips.Checking, 0.4f),
        BehaviorKind.Challenging => new ClipBehavior(k, Clips.Challenging, 0.25f),
        BehaviorKind.WatchingMovie => new WatchingMovieBehavior(),
        _ => new IdleBehavior(),
    };

    private ClipContext BuildClipContext()
    {
        Vector2 look = Vector2.Zero;
        if (LookOverride is { } o) look = o;
        else
        {
            Vector2 to = Cursor - Motion.Position;
            float len = to.Length();
            if (len > 1f)
            {
                float logical = len / DpiScale;
                float fade = Math.Clamp(1f - (logical - 300f) / 600f, 0f, 1f); // ignore a far-away cursor
                look = to / len * fade;
            }
        }
        Vector2 v = Current is DraggedBehavior d ? d.DragVelocity : Motion.Velocity;
        return new ClipContext { LookDir = look, Velocity = v / DpiScale };
    }

    // ---------------------------------------------------------------- user input

    public void Handle(in InteractionEvent e)
    {
        Cursor = e.Position;
        if (e.Kind is not (InteractionKind.HoverStart or InteractionKind.HoverEnd)) Sleepiness = 0f;

        switch (e.Kind)
        {
            case InteractionKind.DragStart:
                Interrupt(new DraggedBehavior());
                break;

            case InteractionKind.DragMove when Current is DraggedBehavior d:
                Motion.Position = e.Position + d.GrabOffset;
                d.DragVelocity = e.Velocity;
                break;

            case InteractionKind.DragEnd when Current is DraggedBehavior d:
                d.Released = true;
                var v = e.Velocity;
                float max = 1200f * DpiScale;
                if (v.Length() > max) v = Vector2.Normalize(v) * max;
                Motion.Velocity = v * 0.8f; // keep most of the throw's momentum
                Motion.Release(300f * DpiScale); // gentle drag so a flick glides and drifts, not snaps to a stop
                _queued = BehaviorKind.Settling;
                break;

            case InteractionKind.Click:
                if (WakeIfSleeping()) break;
                _recentClicks.Add(Now);
                _recentClicks.RemoveAll(t => Now - t > 2.0);
                if (_recentClicks.Count >= 3) { _recentClicks.Clear(); Interrupt(Create(BehaviorKind.Excited)); }
                else Interrupt(Variant switch
                {
                    CompanionVariant.Red => new CuriousBehavior(atCursor: true),
                    CompanionVariant.Green => Create(BehaviorKind.Excited),
                    _ => Create(BehaviorKind.Happy),
                });
                break;

            case InteractionKind.DoubleClick:
                if (WakeIfSleeping()) break;
                Interrupt(Create(BehaviorKind.Dance));
                break;

            case InteractionKind.Petting:
                if (WakeIfSleeping()) break;
                if (Current is AffectionateBehavior a) a.LastPet = Now;
                else if (Current.Kind != BehaviorKind.Dragged) Interrupt(new AffectionateBehavior());
                break;

            case InteractionKind.HoverStart:
                if (Current.Kind is BehaviorKind.Idle or BehaviorKind.Wander && OffCooldown(BehaviorKind.Curious))
                    Interrupt(new CuriousBehavior(atCursor: true));
                break;
        }
    }

    private bool WakeIfSleeping()
    {
        if (!IsAsleepish) return false;
        if (Settings.SleepMode) { Settings.SleepMode = false; SettingsMutated?.Invoke(); }
        Interrupt(Create(BehaviorKind.Waking));
        return true;
    }

    public void Execute(CompanionCommand c)
    {
        Sleepiness = c == CompanionCommand.Sleep ? Sleepiness : 0f;
        switch (c)
        {
            case CompanionCommand.Dance:
                if (!WakeIfSleeping()) Interrupt(Create(BehaviorKind.Dance));
                break;
            case CompanionCommand.Happy:
                if (!WakeIfSleeping()) Interrupt(Create(BehaviorKind.Happy));
                break;
            case CompanionCommand.Signature:
                if (!WakeIfSleeping()) Interrupt(Create(BehaviorKind.Signature));
                break;
            case CompanionCommand.Sleep:
                if (!IsAsleepish) Interrupt(new DrowsyBehavior());
                break;
            case CompanionCommand.Wake:
                WakeIfSleeping();
                break;
            case CompanionCommand.ComeHere:
                if (Settings.SleepMode) { Settings.SleepMode = false; SettingsMutated?.Invoke(); }
                Interrupt(new ComeHereBehavior());
                break;
            case CompanionCommand.ResetPosition:
                var wa = Geometry.Primary.WorkArea;
                Motion.Teleport(Geometry.NearestSafePoint(new Vector2(wa.Right - 120 * DpiScale, wa.Bottom - 60 * DpiScale), Margin));
                Interrupt(Create(BehaviorKind.Happy));
                break;
        }
    }
}
