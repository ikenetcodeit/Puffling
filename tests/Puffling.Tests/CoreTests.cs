using System.Numerics;
using Puffling.Core;
using Puffling.Core.Animation;
using Puffling.Core.Behavior;
using Puffling.Core.Character;
using Puffling.Core.Interaction;
using Puffling.Core.Movement;
using Puffling.Core.Rendering;
using Puffling.Core.Settings;

namespace Puffling.Tests;

public static class RenderingTests
{
    private static readonly IClip[] AllClips =
    {
        Clips.Idle, Clips.Happy, Clips.Excited, Clips.Surprised, Clips.Curious, Clips.Drowsy, Clips.Sleeping,
        Clips.Waking, Clips.Dance, Clips.Affection, Clips.Travel, Clips.Follow, Clips.Dragged, Clips.Settle,
        Clips.Signature(Puffling.Core.Character.CompanionVariant.Red),
        Clips.Signature(Puffling.Core.Character.CompanionVariant.Blue),
        Clips.Signature(Puffling.Core.Character.CompanionVariant.Green),
        Clips.GroupGame(GroupGameKind.Chase, 0), Clips.GroupGame(GroupGameKind.Spar, 1),
        Clips.GroupGame(GroupGameKind.Pong, 2),
        Clips.Grooving, Clips.Watching, Clips.Coding, Clips.AgentBusy, Clips.Searching, Clips.Declined,
        Clips.AgentWaiting, Clips.AgentSucceeded, Clips.AgentFailed,
    };

    [Test]
    public static void RestPose_RendersOpaqueCharacter_WithTransparentSurroundings()
    {
        var r = new CharacterRenderer();
        var fb = r.Render(Pose.Rest, 0, 0, 48, particles: false);
        int opaque = fb.Color.Count(c => c >> 24 == 255);
        int partial = fb.Color.Count(c => c >> 24 is > 0 and < 255);
        Assert.True(opaque > 48 * 48 / 5, $"character too small/empty: {opaque} opaque px");
        Assert.True(partial == 0, "pixel art must be hard-edged (no semi-transparent pixels => precise hit-testing)");
        Assert.True(fb.Color[0] == 0 && fb.Color[^1] == 0, "corners must be transparent (click-through)");
        Assert.Equal(0, fb.ClippedVertices, "clipped vertices");
    }

    /// <summary>Spec §2/§15: the character must stay inside its icon-sized box in EVERY animation,
    /// at every supported size and intensity, including dancing, travelling and being dragged.</summary>
    [Test]
    public static void EveryClip_AtEverySizeAndIntensity_StaysInsideCanvas()
    {
        var r = new CharacterRenderer();
        var rng = new Random(1);
        int frames = 0;
        foreach (int size in new[] { 32, 48, 64, 96 })
            foreach (float intensity in new[] { 0.3f, 1f, 1.5f })
                foreach (var clip in AllClips)
                {
                    var anim = new Animator(new Random(2)) { Intensity = intensity };
                    anim.Play(clip, 0f);
                    for (float t = 0; t < 9f; t += 1f / 20f)
                    {
                        var ctx = new ClipContext
                        {
                            LookDir = new Vector2((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1),
                            Velocity = new Vector2((float)rng.NextDouble() * 2400 - 1200, (float)rng.NextDouble() * 2400 - 1200),
                        };
                        anim.Update(1f / 20f, ctx);
                        var fb = r.Render(anim.Current, anim.TentaclePhase, anim.ParticleTime, size, particles: false);
                        frames++;
                        if (fb.ClippedVertices != 0)
                            throw new AssertException($"{clip.Name} @ {size}px intensity {intensity} t={t:F2}: {fb.ClippedVertices} vertices outside canvas");
                        for (int i = 0; i < size; i++)
                        {
                            bool edge = fb.Color[i] != 0 || fb.Color[(size - 1) * size + i] != 0 ||
                                        fb.Color[i * size] != 0 || fb.Color[i * size + size - 1] != 0;
                            if (edge) throw new AssertException($"{clip.Name} @ {size}px touches the canvas border");
                        }
                    }
                }
        Console.WriteLine($"        checked {frames} frames");
    }

    [Test]
    public static void Character_FillsAReasonableShareOfTheIconBox()
    {
        // Guard against the fit becoming so conservative that the pet turns into a speck.
        var fb = new CharacterRenderer().Render(Pose.Rest, 0, 0, 48, particles: false);
        int minX = 48, maxX = 0, minY = 48, maxY = 0;
        for (int y = 0; y < 48; y++)
            for (int x = 0; x < 48; x++)
                if (fb.Color[y * 48 + x] != 0) { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
        int h = maxY - minY + 1, w = maxX - minX + 1;
        Console.WriteLine($"        rest-pose footprint at 48px: {w}x{h}");
        Assert.True(h >= 34, $"too short: {h}px tall");
        Assert.True(w >= 22, $"too narrow: {w}px wide");
    }
}

public static class TravelFacingTests
{
    [Test]
    public static void TravelAndFollow_FaceTheirHorizontalDirection_AtWanderSpeed()
    {
        foreach (var clip in new[] { Clips.Travel, Clips.Follow })
        {
            var left = clip.Evaluate(0f, new ClipContext { Velocity = new Vector2(-42, 0) });
            var right = clip.Evaluate(0f, new ClipContext { Velocity = new Vector2(42, 0) });
            Assert.True(left.Yaw + CharacterRenderer.CameraYaw < -0.6f, $"{clip.Name} should face left");
            Assert.True(right.Yaw + CharacterRenderer.CameraYaw > 0.5f, $"{clip.Name} should face right");
        }
    }

    [Test]
    public static void ReversingTravel_TurnsSmoothlyAndSettles()
    {
        var anim = new Animator(new Random(1));
        anim.Play(Clips.Travel, 0f);
        var right = new ClipContext { Velocity = new Vector2(42, 0) };
        var left = new ClipContext { Velocity = new Vector2(-42, 0) };
        for (int i = 0; i < 90; i++) anim.Update(1f / 60f, right);
        Assert.True(anim.Current.Yaw > 0.9f, "right turn should settle");

        float before = anim.Current.Yaw;
        anim.Update(1f / 60f, left);
        Assert.True(anim.Current.Yaw < before && anim.Current.Yaw > 0.7f, "reversal should begin without snapping");
        for (int i = 0; i < 90; i++) anim.Update(1f / 60f, left);
        Assert.True(anim.Current.Yaw < -0.45f, "left turn should settle");
    }
}

public static class PointerTests
{
    private static List<InteractionEvent> Drain(PointerTracker p)
    {
        var l = new List<InteractionEvent>();
        while (p.TryDequeue(out var e)) l.Add(e);
        return l;
    }

    [Test]
    public static void PressRelease_InPlace_IsClick()
    {
        var p = new PointerTracker();
        p.PointerDown(new(10, 10), 0); p.PointerMove(new(11, 10), 0.05); p.PointerUp(new(11, 10), 0.1);
        var ev = Drain(p);
        Assert.True(ev.Count(e => e.Kind == InteractionKind.Click) == 1, "expected one click");
        Assert.True(ev.All(e => e.Kind != InteractionKind.DragStart), "jitter below threshold must not drag");
    }

    [Test]
    public static void PressMoveRelease_IsDrag_WithThrowVelocity()
    {
        var p = new PointerTracker();
        p.PointerDown(new(0, 0), 0);
        for (int i = 1; i <= 10; i++) p.PointerMove(new(i * 10, 0), i * 0.016);
        p.PointerUp(new(100, 0), 0.17);
        var ev = Drain(p);
        Assert.True(ev[0].Kind == InteractionKind.DragStart, "drag start first");
        var end = ev.Single(e => e.Kind == InteractionKind.DragEnd);
        Assert.True(end.Velocity.X > 200, $"throw velocity expected, got {end.Velocity}");
        Assert.True(ev.All(e => e.Kind != InteractionKind.Click), "a drag is not a click");
    }

    [Test]
    public static void DragThatStopsBeforeRelease_HasNoThrow()
    {
        var p = new PointerTracker();
        p.PointerDown(new(0, 0), 0);
        for (int i = 1; i <= 5; i++) p.PointerMove(new(i * 10, 0), i * 0.016);
        p.PointerUp(new(50, 0), 0.5);
        Assert.True(Drain(p).Single(e => e.Kind == InteractionKind.DragEnd).Velocity == Vector2.Zero, "no throw");
    }

    [Test]
    public static void DoubleClick_DoesNotAlsoProduceSecondClick()
    {
        var p = new PointerTracker();
        p.PointerDown(new(5, 5), 0); p.PointerUp(new(5, 5), 0.05);
        p.DoubleClick(new(5, 5), 0.2); p.PointerUp(new(5, 5), 0.25);
        var ev = Drain(p);
        Assert.Equal(1, ev.Count(e => e.Kind == InteractionKind.Click), "clicks");
        Assert.Equal(1, ev.Count(e => e.Kind == InteractionKind.DoubleClick), "double clicks");
    }

    [Test]
    public static void BackAndForthStrokes_ArePetting_ButJitterIsNot()
    {
        var p = new PointerTracker();
        double t = 0; float x = 0;
        for (int stroke = 0; stroke < 6; stroke++)
            for (int i = 0; i < 4; i++) { x += stroke % 2 == 0 ? 3 : -3; t += 0.03; p.PointerMove(new(x, 0), t); }
        Assert.True(Drain(p).Any(e => e.Kind == InteractionKind.Petting), "strokes should pet");

        var q = new PointerTracker();
        t = 0;
        for (int i = 0; i < 60; i++) { t += 0.02; q.PointerMove(new(i % 2, 0), t); } // 1px tremor
        Assert.True(Drain(q).All(e => e.Kind != InteractionKind.Petting), "tremor must not count as petting");
    }
}

public static class MovementTests
{
    [Test]
    public static void Motion_ArrivesSmoothly_WithoutOvershootOrSpeeding()
    {
        var m = new Motion { Wobble = 0 };
        m.Teleport(new(0, 0));
        m.MoveTo(new(500, 0), 150, 120);
        float maxSpeed = 0, maxX = 0, prevSpeed = 0, maxJerk = 0;
        for (int i = 0; i < 60 * 20 && !m.Arrived; i++)
        {
            m.Update(1 / 60f);
            float s = m.Velocity.Length();
            maxSpeed = MathF.Max(maxSpeed, s);
            maxX = MathF.Max(maxX, m.Position.X);
            maxJerk = MathF.Max(maxJerk, MathF.Abs(s - prevSpeed));
            prevSpeed = s;
        }
        Assert.True(m.Arrived, "should arrive within 20 s");
        Assert.True(maxSpeed <= 150.01f, $"speed limit exceeded: {maxSpeed}");
        Assert.True(maxX <= 500.5f, $"overshoot: {maxX}");
        Assert.True(maxJerk <= 120f / 60f + 0.01f, "acceleration limit exceeded (would look like snapping)");
    }

    [Test]
    public static void Geometry_PathAcrossMonitorGap_IsNotVisible()
    {
        // Left monitor 1920x1080, right monitor taller and offset up: corner gap below-left.
        var g = new ScreenGeometry(new[]
        {
            new MonitorInfo("a", new RectI(0, 0, 1920, 1080), new RectI(0, 0, 1920, 1040), 1f, true),
            new MonitorInfo("b", new RectI(1920, -400, 3360, 2160), new RectI(1920, -400, 3360, 2120), 1.5f, false),
        });
        Assert.True(g.PathIsVisible(new(100, 500), new(2500, 500)), "straight path across shared edge");
        Assert.True(!g.PathIsVisible(new(100, 500), new(2500, -300)), "path through the void above monitor A");
        var safe = g.NearestSafePoint(new(-500, 5000), 30);
        Assert.True(g.IsComfortablyVisible(safe, 30), "nearest safe point must be visible");
    }
}

public static class CompanionCollisionTests
{
    private static Companion MakePet(ScreenGeometry g, Vector2 pos, int seed) =>
        new(new CompanionSettings { LastX = pos.X, LastY = pos.Y, AutonomousMovement = false }, g, seed);

    [Test]
    public static void Overlapping_Companions_ArePushedApart()
    {
        var g = ScreenGeometry.Single(1920, 1080);
        var a = MakePet(g, new(900, 500), 1);
        var b = MakePet(g, new(915, 500), 2); // well inside each other's icon radius
        float minDist = (a.CanvasSize + b.CanvasSize) * 0.32f;

        for (int i = 0; i < 10; i++) CompanionCollision.Resolve(new[] { a, b });

        Assert.True(Vector2.Distance(a.Position, b.Position) >= minDist - 0.5f,
            $"should no longer overlap: {Vector2.Distance(a.Position, b.Position)} < {minDist}");
    }

    [Test]
    public static void FlickedCompanion_BouncesOffAStationaryOne_LikeBilliards()
    {
        var g = ScreenGeometry.Single(1920, 1080);
        var moving = MakePet(g, new(700, 500), 3);
        var still = MakePet(g, new(760, 500), 4);
        moving.Motion.Velocity = new Vector2(400, 0); // flicked straight at `still`

        bool bounced = false;
        for (int i = 0; i < 120 && !bounced; i++)
        {
            moving.Motion.Update(1 / 60f);
            CompanionCollision.Resolve(new[] { moving, still });
            if (still.Motion.Velocity.X > 10f) bounced = true;
        }
        Assert.True(bounced, "a stationary companion picks up velocity when struck");
        Assert.True(moving.Motion.Velocity.X < 400f, "the mover loses speed to the one it struck");
    }

    [Test]
    public static void DraggedCompanion_IsNeverMoved_ButStillShovesWhatItHits()
    {
        var g = ScreenGeometry.Single(1920, 1080);
        var dragged = MakePet(g, new(900, 500), 5);
        var other = MakePet(g, new(915, 500), 6);
        dragged.Pointer.PointerDown(dragged.Position, 0);
        dragged.Pointer.PointerMove(dragged.Position + new Vector2(30, 0), 0.05);
        dragged.Tick(1 / 60f, new FrameInputs(dragged.Position + new Vector2(30, 0), 0));
        Assert.Equal(BehaviorKind.Dragged, dragged.CurrentBehavior, "set up as dragged");
        var pinnedPos = dragged.Position;

        CompanionCollision.Resolve(new[] { dragged, other });

        Assert.Equal(pinnedPos, dragged.Position, "a dragged pet is glued to the cursor, never repositioned");
        float minDist = (dragged.CanvasSize + other.CanvasSize) * 0.32f;
        Assert.True(Vector2.Distance(dragged.Position, other.Position) >= minDist - 0.5f,
            "the other pet gets pushed clear instead");
    }

    [Test]
    public static void GroupPlay_IsNeverDisturbedByCollision()
    {
        var g = ScreenGeometry.Single(1920, 1080);
        var a = MakePet(g, new(900, 500), 7);
        var b = MakePet(g, new(905, 500), 8); // deliberately overlapping
        a.Brain.Interrupt(new GroupPlayBehavior(new(900, 500), 0, 2, 52f));
        b.Brain.Interrupt(new GroupPlayBehavior(new(900, 500), 1, 2, 52f));
        var posA = a.Position;
        var posB = b.Position;

        CompanionCollision.Resolve(new[] { a, b });

        Assert.Equal(posA, a.Position, "choreographed routines are left alone");
        Assert.Equal(posB, b.Position, "choreographed routines are left alone");
    }
}

public static class SettingsTests
{
    [Test]
    public static void Normalize_ClampsGarbage()
    {
        var s = new CompanionSettings { Scale = 5000, MovementSpeed = float.NaN, FpsLimit = 1, AnimationIntensity = -3, LastX = float.PositiveInfinity }.Normalize();
        Assert.Equal(96, s.Scale, "scale");
        Assert.Equal(1f, s.MovementSpeed, "speed");
        Assert.Equal(10, s.FpsLimit, "fps");
        Assert.Equal(0.3f, s.AnimationIntensity, "intensity");
        Assert.True(s.LastX is null, "non-finite position discarded");
    }

    [Test]
    public static void Store_RoundTrips_AndSurvivesCorruption()
    {
        string dir = Path.Combine(Path.GetTempPath(), "puffling-test-" + Guid.NewGuid());
        var store = new SettingsStore(Path.Combine(dir, "settings.json"));
        try
        {
            store.Save(new CompanionSettings
            {
                Scale = 64, FollowCursor = true, LastX = 12.5f, CompanionCount = 3,
                AdditionalPositions = [new(300, 400), new(600, 700)]
            });
            var back = store.Load();
            Assert.Equal(64, back.Scale, "scale"); Assert.True(back.FollowCursor, "follow"); Assert.True(back.LastX == 12.5f, "x");
            Assert.Equal(3, back.CompanionCount, "saved companion count");
            Assert.Equal(new SavedCompanionPosition(600, 700), back.AdditionalPositions[1], "saved extra position");

            File.WriteAllText(store.FilePath, "{ this is not json");
            var d = store.Load();
            Assert.Equal(CompanionSettings.DefaultScale, d.Scale, "defaults after corruption");
            Assert.True(File.Exists(store.FilePath + ".bak"), "corrupt file preserved as .bak");
        }
        finally { Directory.Delete(dir, true); }
    }
}

public static class AgentLifecycleTests
{
    private static string Json(string state, DateTimeOffset updatedAt) =>
        $$"""{"state":"{{state}}","updatedAt":"{{updatedAt:O}}"}""";

    [Test]
    public static void TryParse_MapsEveryRecognizedState()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(SystemActivity.AgentIdle, AgentLifecycle.TryParse(Json("idle", now), now), "idle");
        Assert.Equal(SystemActivity.AgentRunning, AgentLifecycle.TryParse(Json("running", now), now), "running");
        Assert.Equal(SystemActivity.AgentWaitingForInput, AgentLifecycle.TryParse(Json("waitingForInput", now), now), "waitingForInput");
        Assert.Equal(SystemActivity.AgentSucceeded, AgentLifecycle.TryParse(Json("succeeded", now), now), "succeeded");
        Assert.Equal(SystemActivity.AgentFailed, AgentLifecycle.TryParse(Json("failed", now), now), "failed");
    }

    [Test]
    public static void TryParse_RejectsMalformedOrUnrecognizedInput()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.True(AgentLifecycle.TryParse("{ not json", now) is null, "malformed JSON");
        Assert.True(AgentLifecycle.TryParse("{}", now) is null, "missing state");
        Assert.True(AgentLifecycle.TryParse(Json("whatever", now), now) is null, "unrecognized state");
        Assert.True(AgentLifecycle.TryParse("""{"state":"running"}""", now) is null, "missing updatedAt");
        Assert.True(AgentLifecycle.TryParse("""{"state":"running","updatedAt":"not a date"}""", now) is null, "unparsable updatedAt");
    }

    [Test]
    public static void TryParse_RejectsAStaleEvent_SoACrashedScriptCannotStickForever()
    {
        var now = DateTimeOffset.UtcNow;
        var fresh = Json("succeeded", now - AgentLifecycle.MaxAge + TimeSpan.FromSeconds(1));
        var stale = Json("succeeded", now - AgentLifecycle.MaxAge - TimeSpan.FromSeconds(1));
        Assert.Equal(SystemActivity.AgentSucceeded, AgentLifecycle.TryParse(fresh, now), "fresh");
        Assert.True(AgentLifecycle.TryParse(stale, now) is null, "older than MaxAge falls back to the coarse heuristic");
    }
}

public static class BehaviorTests
{
    internal static ScreenGeometry ThreeMonitors() => new(new[]
    {
        new MonitorInfo("left", new RectI(-1280, 200, 0, 1224), new RectI(-1280, 200, 0, 1224), 1f, false),
        new MonitorInfo("main", new RectI(0, 0, 1920, 1080), new RectI(0, 0, 1920, 1032), 1.25f, true),
        new MonitorInfo("right", new RectI(1920, -300, 4480, 1140), new RectI(1920, -300, 4480, 1100), 2f, false),
    });

    private static void Run(Companion c, float seconds, Vector2 cursor, float idle, float fps = 30)
    {
        for (float t = 0; t < seconds; t += 1 / fps) c.Tick(1 / fps, new FrameInputs(cursor, idle));
    }

    [Test]
    public static void Inactive_User_PetFallsAsleep_AndClickWakesIt()
    {
        var c = new Companion(new CompanionSettings(), ScreenGeometry.Single(1920, 1080), seed: 3);
        var far = new Vector2(10, 10);
        float idle = 500;
        bool slept = false;
        for (int i = 0; i < 30 * 600 && !slept; i++) { c.Tick(1 / 30f, new(far, idle)); slept = c.IsSleeping; }
        Assert.True(slept, "should fall asleep within 10 min of user inactivity");

        var p = c.Position;
        c.Pointer.PointerDown(p, 1000); c.Pointer.PointerUp(p, 1000.05);
        c.Tick(1 / 30f, new(far, 0));
        Assert.Equal(BehaviorKind.Waking, c.CurrentBehavior, "click wakes");
        Run(c, 3f, far, 0);
        Assert.True(c.CurrentBehavior != BehaviorKind.Sleeping && c.CurrentBehavior != BehaviorKind.Waking, "awake after waking clip");
    }

    [Test]
    public static void SleepMode_HoldsSleep_UntilClicked_ThenClearsSetting()
    {
        var s = new CompanionSettings { SleepMode = true };
        var c = new Companion(s, ScreenGeometry.Single(1920, 1080), seed: 4);
        bool mutated = false;
        c.Brain.SettingsMutated += () => mutated = true;
        Run(c, 400f, new(5, 5), 0);
        Assert.Equal(BehaviorKind.Sleeping, c.CurrentBehavior, "sleep mode keeps it asleep beyond natural duration");
        c.Pointer.PointerDown(c.Position, 1); c.Pointer.PointerUp(c.Position, 1.05);
        c.Tick(1 / 30f, new(new(5, 5), 0));
        Assert.True(!c.Settings.SleepMode && mutated, "waking leaves sleep mode and notifies the shell");
    }

    [Test]
    public static void Drag_MovesPet_ThenSettles_AndReturnsOnScreen()
    {
        var c = new Companion(new CompanionSettings(), ScreenGeometry.Single(1920, 1080), seed: 5);
        var start = c.Position;
        c.Pointer.PointerDown(start, 0);
        Vector2 cur = start;
        for (int i = 1; i <= 40; i++)
        {
            cur = start + new Vector2(i * 60, 0); // drag far off the right edge
            c.Pointer.PointerMove(cur, i * 0.016);
            c.Tick(1 / 60f, new(cur, 0));
        }
        Assert.Equal(BehaviorKind.Dragged, c.CurrentBehavior, "dragging");
        Assert.True(Vector2.Distance(c.Position, cur) < 1, "pet follows the cursor exactly while dragged");
        c.Pointer.PointerUp(cur, 0.65);
        c.Tick(1 / 60f, new(cur, 0));
        c.Tick(1 / 60f, new(cur, 0));
        Assert.Equal(BehaviorKind.Settling, c.CurrentBehavior, "settles after release");
        Run(c, 15f, new(5, 5), 0);
        Assert.True(c.Geometry.IsComfortablyVisible(c.Position, c.Brain.Margin), $"must return on-screen, at {c.Position}");
    }

    [Test]
    public static void UnpluggedMonitor_PetIsRestoredToARemainingMonitor()
    {
        var g = ThreeMonitors();
        var c = new Companion(new CompanionSettings { LastX = 3000, LastY = 400 }, g, seed: 6);
        Assert.True(g.Monitors[2].Bounds.Contains(c.Position), "starts on right monitor");
        c.UpdateGeometry(ScreenGeometry.Single(1920, 1080));
        Assert.True(c.Geometry.IsComfortablyVisible(c.Position, c.Brain.Margin), "restored onto the remaining monitor");
        Assert.Equal(48, c.CanvasSize, "canvas re-derived from the new monitor's DPI");
    }

    [Test]
    public static void Travel_OccasionallyCrossesToAnotherMonitor_WithoutDriftingThroughTheGap()
    {
        var g = ThreeMonitors();
        var c = new Companion(new CompanionSettings { LastX = 900, LastY = 500, ActivityFrequency = 2f }, g, seed: 11);
        var startMonitor = g.MonitorAt(c.Position).Id;
        bool crossed = false;
        for (int i = 0; i < 30 * 600 && !crossed; i++)
        {
            c.Tick(1 / 30f, new FrameInputs(new Vector2(-9000, -9000), 20));
            // While deliberately travelling, the pet must never be caught in a real gap between
            // monitors (ReturningBehavior/ScreenEdgeCollision must not fight a validated crossing).
            if (c.CurrentBehavior == BehaviorKind.Travel) Assert.True(g.IsOnAnyMonitor(c.Position), $"left all monitors mid-travel at {c.Position}");
            if (g.MonitorAt(c.Position).Id != startMonitor) crossed = true;
        }
        Assert.True(crossed, "never travelled to another monitor within 10 simulated minutes");
    }

    [Test]
    public static void SystemActivity_ReactsImmediatelyWhenIdle_AndClearsBackToIdle()
    {
        var c = new Companion(new CompanionSettings(), ScreenGeometry.Single(1920, 1080), seed: 7);
        Run(c, 9f, new(5, 5), 0); // settle into Idle
        Assert.Equal(BehaviorKind.Idle, c.CurrentBehavior, "idle before reacting");

        c.Brain.SystemActivity = SystemActivity.AgentRunning;
        Assert.Equal(BehaviorKind.AgentBusy, c.CurrentBehavior, "reacts right away while free to be interrupted");

        c.Brain.SystemActivity = SystemActivity.None;
        Run(c, 10f, new(5, 5), 0);
        Assert.True(c.CurrentBehavior != BehaviorKind.AgentBusy, "stops once the activity clears");
    }

    [Test]
    public static void SystemActivity_AgentLifecycle_PlaysMatchingReaction_ForEachState()
    {
        var g = ScreenGeometry.Single(1920, 1080);
        foreach (var (activity, kind) in new[]
        {
            (SystemActivity.AgentWaitingForInput, BehaviorKind.AgentWaiting),
            (SystemActivity.AgentSucceeded, BehaviorKind.AgentSucceeded),
            (SystemActivity.AgentFailed, BehaviorKind.AgentFailed),
        })
        {
            var c = new Companion(new CompanionSettings(), g, seed: 7, CompanionVariant.Red);
            Run(c, 9f, new(5, 5), 0); // settle into Idle
            c.Brain.SystemActivity = activity;
            Assert.Equal(kind, c.CurrentBehavior, $"{activity} reacts right away while free to be interrupted");

            c.Brain.SystemActivity = SystemActivity.None;
            Run(c, 10f, new(5, 5), 0);
            Assert.True(c.CurrentBehavior != kind, $"{activity} reaction stops once the activity clears");
        }
    }

    [Test]
    public static void SystemActivity_AgentIdle_DoesNotInterruptOrChurnTheCurrentIdle()
    {
        var c = new Companion(new CompanionSettings(), ScreenGeometry.Single(1920, 1080), seed: 7);
        Run(c, 1f, new(5, 5), 0);
        Assert.Equal(BehaviorKind.Idle, c.CurrentBehavior, "idle before reacting");

        c.Brain.SystemActivity = SystemActivity.AgentIdle;
        Assert.Equal(BehaviorKind.Idle, c.CurrentBehavior, "agent-idle is not noteworthy, unlike running/waiting/succeeded/failed");
    }

    [Test]
    public static void SystemActivity_AgentWaitingForInput_GreenSometimesWandersOff_OthersHoldStill()
    {
        var g = ScreenGeometry.Single(1920, 1080);
        int greenWandered = 0, redWandered = 0;
        const int trials = 24;
        for (int i = 0; i < trials; i++)
        {
            var green = new Companion(new CompanionSettings { LastX = 960, LastY = 540 }, g, seed: 2000 + i, CompanionVariant.Green);
            green.Brain.SystemActivity = SystemActivity.AgentWaitingForInput;
            Run(green, 2f, new(-9000, -9000), 20);
            if (Vector2.Distance(green.Position, new Vector2(960, 540)) > 15f) greenWandered++;

            var red = new Companion(new CompanionSettings { LastX = 960, LastY = 540 }, g, seed: 2000 + i, CompanionVariant.Red);
            red.Brain.SystemActivity = SystemActivity.AgentWaitingForInput;
            Run(red, 2f, new(-9000, -9000), 20);
            if (Vector2.Distance(red.Position, new Vector2(960, 540)) > 15f) redWandered++;
        }
        Assert.True(greenWandered > 0, "green sometimes wanders off while waiting");
        Assert.True(greenWandered < trials, "green doesn't always wander off while waiting");
        Assert.Equal(0, redWandered, "red just holds the impatient pose rather than wandering off");
    }

    [Test]
    public static void SystemActivity_DoesNotInterruptUserDrivenBehavior()
    {
        var c = new Companion(new CompanionSettings(), ScreenGeometry.Single(1920, 1080), seed: 8);
        var start = c.Position;
        c.Pointer.PointerDown(start, 0);
        var moved = start + new Vector2(40, 0);
        c.Pointer.PointerMove(moved, 0.05);
        c.Tick(1 / 30f, new(moved, 0));
        Assert.Equal(BehaviorKind.Dragged, c.CurrentBehavior, "dragging before setting activity");

        c.Brain.SystemActivity = SystemActivity.Coding;
        Assert.Equal(BehaviorKind.Dragged, c.CurrentBehavior, "an uninterruptible/user behavior keeps going");
    }

    [Test]
    public static void CanvasSize_FollowsMonitorDpi()
    {
        var c = new Companion(new CompanionSettings { LastX = 3000, LastY = 400 }, ThreeMonitors(), seed: 7);
        Assert.Equal(96, c.CanvasSize, "48 logical px at 200% = 96 physical px");
    }

    [Test]
    public static void Paused_FreezesAnimation()
    {
        var c = new Companion(new CompanionSettings(), ScreenGeometry.Single(1920, 1080), seed: 8);
        Run(c, 1f, new(5, 5), 0);
        c.ApplySettings(new CompanionSettings { Paused = true });
        var before = c.Animator.Current; var pos = c.Position; float ph = c.Animator.TentaclePhase;
        Run(c, 2f, new(5, 5), 0);
        Assert.True(c.Animator.TentaclePhase == ph && c.Position == pos && c.Animator.Current.Equals(before), "nothing moves while paused");
    }

    [Test]
    public static void FreezePosition_PreventsAutonomousMovement()
    {
        var c = new Companion(new CompanionSettings { FreezePosition = true, ActivityFrequency = 2 }, ScreenGeometry.Single(1920, 1080), seed: 9);
        var p0 = c.Position;
        Run(c, 300f, new(5, 5), 30);
        Assert.True(Vector2.Distance(p0, c.Position) < 2f, $"moved {Vector2.Distance(p0, c.Position)} px while frozen");
    }

    /// <summary>Soak test: two simulated hours on three mixed-DPI monitors with a wandering cursor
    /// and random clicks, pets, double-clicks and drags. Checks invariants continuously.</summary>
    [Test]
    public static void Soak_TwoHours_ThreeMonitors_InvariantsHold()
    {
        var g = ThreeMonitors();
        var c = new Companion(new CompanionSettings(), g, seed: 42);
        var rng = new Random(42);
        var seen = new HashSet<BehaviorKind>();
        var autonomousDanceStarts = new List<double>();
        bool userDance = false;
        c.Brain.BehaviorChanged += k =>
        {
            seen.Add(k);
            if (k == BehaviorKind.Dance) { if (!userDance) autonomousDanceStarts.Add(c.Brain.Now); userDance = false; }
        };

        Vector2 cursor = new(900, 500), cursorVel = Vector2.Zero;
        const float dt = 1 / 30f;
        double t = 0, offscreenSince = -1;
        float idle = 0;
        int frames = 0;
        for (; t < 7200; t += dt, frames++)
        {
            // Cursor: random walk with bursts, occasionally parked (user reading / away).
            bool away = (int)(t / 600) % 3 == 2;
            idle = away ? idle + dt : (rng.NextDouble() < 0.02 ? 0 : idle + dt);
            if (!away)
            {
                cursorVel = Vector2.Lerp(cursorVel, new Vector2(rng.Next(-900, 900), rng.Next(-600, 600)), 0.05f);
                cursor = g.NearestSafePoint(cursor + cursorVel * dt, 0);
            }

            // Occasional direct interactions at the pet.
            double roll = rng.NextDouble();
            var p = c.Position;
            if (!away && roll < 0.0006) { c.Pointer.PointerDown(p, t); c.Pointer.PointerUp(p, t + 0.05); }
            else if (!away && roll < 0.0008) { userDance = true; c.Pointer.PointerDown(p, t); c.Pointer.PointerUp(p, t + 0.05); c.Pointer.DoubleClick(p, t + 0.1); c.Pointer.PointerUp(p, t + 0.15); }
            else if (!away && roll < 0.0010)
            {
                for (int i = 0; i < 16; i++) c.Pointer.PointerMove(p + new Vector2(i % 2 == 0 ? 5 : -5, 0), t + i * 0.04);
                c.Pointer.PointerLeave(p);
            }
            else if (!away && roll < 0.0011)
            {
                c.Pointer.PointerDown(p, t);
                var target = new Vector2(rng.Next(-1500, 4600), rng.Next(-500, 1400)); // may drop it off-screen
                for (int i = 1; i <= 20; i++) { c.Pointer.PointerMove(Vector2.Lerp(p, target, i / 20f), t + i * 0.016); c.Tick(dt, new(cursor, 0)); }
                c.Pointer.PointerUp(target, t + 0.34);
            }

            c.Tick(dt, new(cursor, idle));

            // Invariant: never lost off-screen for long (Returning must bring it back).
            bool visible = g.IsComfortablyVisible(c.Position, c.Brain.Margin);
            if (!visible && c.CurrentBehavior != BehaviorKind.Dragged)
            {
                if (offscreenSince < 0) offscreenSince = t;
                if (t - offscreenSince > 12) throw new AssertException($"off-screen for >12 s at {c.Position} (behavior {c.CurrentBehavior})");
            }
            else offscreenSince = -1;

            if (frames % 15 == 0)
            {
                var r = c.Render();
                Assert.Equal(0, r.Frame.ClippedVertices, $"clipping during {c.CurrentBehavior}");
                Assert.True(r.Size == r.Frame.Width && r.Frame.Width == r.Frame.Height, "window = canvas = square");
            }
            Assert.True(float.IsFinite(c.Position.X) && float.IsFinite(c.Position.Y), "position finite");
        }

        Console.WriteLine($"        behaviours seen: {string.Join(", ", seen.OrderBy(k => k))}");
        foreach (var k in new[] { BehaviorKind.Idle, BehaviorKind.Wander, BehaviorKind.Travel, BehaviorKind.Curious, BehaviorKind.Happy,
                                  BehaviorKind.Dance, BehaviorKind.Affectionate, BehaviorKind.Drowsy, BehaviorKind.Sleeping,
                                  BehaviorKind.Waking, BehaviorKind.Dragged, BehaviorKind.Settling, BehaviorKind.Follow })
            Assert.True(seen.Contains(k), $"never exhibited {k} in two hours");
        for (int i = 1; i < autonomousDanceStarts.Count; i++)
            Assert.True(autonomousDanceStarts[i] - autonomousDanceStarts[i - 1] >= 119.9, "autonomous dance cooldown violated");
    }
}

public static class VideoWatchingTests
{
    private static Companion MakePet(ScreenGeometry g, CompanionVariant variant, int seed, float? x = null, float? y = null) =>
        new(new CompanionSettings { LastX = x, LastY = y }, g, seed, variant);

    [Test]
    public static void InterestedCompanion_LeavesTheVideoMonitor_AndFacesBackTowardIt()
    {
        var g = BehaviorTests.ThreeMonitors(); // left/main/right
        var blue = MakePet(g, CompanionVariant.Blue, seed: 30, x: 900, y: 500); // starts on "main"
        var mainMonitor = g.MonitorAt(blue.Position);

        blue.Brain.Video = new VideoViewport(mainMonitor.Bounds, Fullscreen: true);
        Assert.Equal(BehaviorKind.WatchingMovie, blue.CurrentBehavior, "a highly-interested companion joins right away");

        for (int i = 0; i < 30 * 90; i++) blue.Tick(1 / 30f, new FrameInputs(new Vector2(-9000, -9000), 20));
        Assert.True(g.MonitorAt(blue.Position).Id != mainMonitor.Id, "relocated off the video's monitor");
        Assert.Equal(BehaviorKind.WatchingMovie, blue.CurrentBehavior, "still watching");

        blue.Brain.Video = null;
        blue.Tick(1 / 30f, new FrameInputs(new Vector2(-9000, -9000), 20));
        Assert.True(blue.CurrentBehavior != BehaviorKind.WatchingMovie, "stops watching once the video ends");
    }

    [Test]
    public static void Green_IsLeastInterested_AndDoesNotAlwaysJoin()
    {
        var g = ScreenGeometry.Single(1920, 1080);
        int joined = 0;
        const int trials = 30;
        for (int i = 0; i < trials; i++)
        {
            var green = MakePet(g, CompanionVariant.Green, seed: 1000 + i);
            green.Brain.Video = new VideoViewport(g.Primary.Bounds, Fullscreen: true);
            if (green.CurrentBehavior == BehaviorKind.WatchingMovie) joined++;
        }
        Assert.True(joined > 0, "joining is still possible sometimes");
        Assert.True(joined < trials, "green (low interest) doesn't always join, unlike blue");
    }

    [Test]
    public static void WindowedVideo_IsNeverCrossedByWandering_AndSlowsNearbyMovement()
    {
        var g = ScreenGeometry.Single(1920, 1080);
        var c = MakePet(g, CompanionVariant.Red, seed: 32, x: 200, y: 500);
        var videoWindow = new RectI(700, 300, 1300, 800); // squarely in the middle of the desktop
        c.Brain.Video = new VideoViewport(videoWindow, Fullscreen: false);

        for (int i = 0; i < 30 * 60; i++)
        {
            c.Tick(1 / 30f, new FrameInputs(new Vector2(-9000, -9000), 20));
            Assert.True(!videoWindow.Contains(c.Position), $"must never enter the video window, at {c.Position}");
        }

        c.Motion.Teleport(new Vector2(1000, 550)); // force it inside, as if the window just appeared there
        float slow = c.Brain.Speed(100);
        c.Brain.Video = null;
        float normal = c.Brain.Speed(100);
        Assert.True(slow < normal, "movement is quieter while standing inside a windowed video's bounds");
    }
}

public static class PerformanceTests
{
    /// <summary>Budget check for the CPU side of a frame (simulate + render). The Windows-side
    /// cost (UpdateLayeredWindow) is separate and must be profiled on Windows.</summary>
    [Test]
    public static void FrameCost_IsWellUnderBudget()
    {
        foreach (int size in new[] { 48, 96 })
        {
            var c = new Companion(new CompanionSettings { Scale = size }, ScreenGeometry.Single(1920, 1080), seed: 1);
            c.Execute(CompanionCommand.Dance); // busiest clip: rotation + particles
            for (int i = 0; i < 60; i++) { c.Tick(1 / 30f, new(new(5, 5), 0)); c.Render(); } // warm-up / JIT
            var sw = System.Diagnostics.Stopwatch.StartNew();
            const int n = 600;
            for (int i = 0; i < n; i++) { c.Tick(1 / 30f, new(new(5, 5), 0)); c.Render().Frame.ComputeHash(); }
            double us = sw.Elapsed.TotalMilliseconds * 1000 / n;
            long alloc0 = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < n; i++) { c.Tick(1 / 30f, new(new(5, 5), 0)); c.Render(); }
            double bytesPerFrame = (GC.GetAllocatedBytesForCurrentThread() - alloc0) / (double)n;
            Console.WriteLine($"        {size}px: {us:F0} µs/frame, {bytesPerFrame:F0} B allocated/frame (at 30 fps = {us * 30 / 10000:F2}% of one core)");
            Assert.True(us < 2000, $"frame too expensive at {size}px: {us:F0} µs");
        }
    }
}

public static class ContextTests
{
    [Test]
    public static void CursorRushingAtPet_StartlesIt_AndComeHereFliesToCursor()
    {
        var c = new Companion(new CompanionSettings(), ScreenGeometry.Single(1920, 1080), seed: 11);
        for (int i = 0; i < 30; i++) c.Tick(1 / 30f, new(new(5, 5), 0)); // settle into idle, cursor far away
        var p = c.Position;
        bool startled = false;
        // Cursor sweeps toward the pet at ~3000 px/s, ending ~60 px away.
        for (int i = 0; i < 6 && !startled; i++)
        {
            var cur = p + new Vector2(-60 - 100 * (5 - i), 0);
            c.Tick(1 / 30f, new(cur, 0));
            startled = c.CurrentBehavior == BehaviorKind.Surprised;
        }
        Assert.True(startled, "a cursor rushing at the pet should startle it");

        var target = new Vector2(400, 300);
        c.Execute(CompanionCommand.ComeHere);
        for (int i = 0; i < 30 * 8; i++) c.Tick(1 / 30f, new(target, 0));
        float d = Vector2.Distance(c.Position, target);
        Assert.True(d < 90 && d > 30, $"should end up beside (not under) the cursor; distance {d:F0}px");
    }
}
