using System.Numerics;
using Puffling.Core.Character;

namespace Puffling.Core.Animation;

/// <summary>Live inputs a clip may react to. All vectors are in screen space (y down),
/// velocities in logical px/sec.</summary>
public struct ClipContext
{
    /// <summary>Unit vector from the character towards the cursor (zero if unknown).</summary>
    public Vector2 LookDir;
    public Vector2 Velocity;
    /// <summary>Per-play random value in [0,1) so repeated plays are not identical.</summary>
    public float Variation;
}

/// <summary>
/// A clip is a pure function (time, context) -> target pose. Clips hold no mutable state,
/// which makes them trivially blendable, restartable and testable.
/// </summary>
public interface IClip
{
    string Name { get; }
    /// <summary>Natural length in seconds, or null for looping clips.</summary>
    float? Duration { get; }
    Pose Evaluate(float t, in ClipContext c);
}

public enum GroupGameKind { Chase, Spar, Pong, Stare }

public static class Clips
{
    public static readonly IClip Idle = new IdleClip();
    public static readonly IClip Happy = new HappyClip();
    public static readonly IClip Excited = new ExcitedClip();
    public static readonly IClip Surprised = new SurprisedClip();
    public static readonly IClip Curious = new CuriousClip();
    public static readonly IClip Drowsy = new DrowsyClip();
    public static readonly IClip Sleeping = new SleepingClip();
    public static readonly IClip Waking = new WakingClip();
    public static readonly IClip Dance = new DanceClip();
    public static readonly IClip Affection = new AffectionClip();
    public static readonly IClip Travel = new TravelClip(happy: false);
    public static readonly IClip Follow = new TravelClip(happy: true);
    public static readonly IClip Dragged = new DraggedClip();
    public static readonly IClip Settle = new SettleClip();
    public static readonly IClip Grooving = new GroovingClip();
    public static readonly IClip Watching = new WatchingClip();
    public static readonly IClip Coding = new CodingClip();
    public static readonly IClip AgentBusy = new AgentBusyClip();
    public static readonly IClip AgentWaiting = new AgentWaitingClip();
    public static readonly IClip AgentSucceeded = new AgentSucceededClip();
    public static readonly IClip AgentFailed = new AgentFailedClip();
    public static readonly IClip Searching = new SearchingClip();
    public static readonly IClip Declined = new DeclinedClip();
    public static readonly IClip Shy = new ShyClip();
    public static readonly IClip Hovering = new HoveringClip();
    public static readonly IClip Mischievous = new MischievousClip();
    private static readonly IClip RedMove = new SignatureClip(CompanionVariant.Red);
    private static readonly IClip BlueMove = new SignatureClip(CompanionVariant.Blue);
    private static readonly IClip GreenMove = new SignatureClip(CompanionVariant.Green);
    public static IClip Signature(CompanionVariant variant) => variant switch
    {
        CompanionVariant.Blue => BlueMove,
        CompanionVariant.Green => GreenMove,
        _ => RedMove,
    };
    public static IClip PairPlay(int slot) => new PairPlayClip(slot);
    public static IClip TrioPlay(int slot) => new TrioPlayClip(slot);
    public static IClip GroupGame(GroupGameKind kind, int slot) => new GroupGameClip(kind, slot);

    internal static float Hop(float t, float freq) => MathF.Abs(MathF.Sin(t * freq));
    internal static float Decay(float t, float rate) => MathF.Exp(-t * rate);

    private sealed class SignatureClip(CompanionVariant variant) : IClip
    {
        public string Name => variant switch
        {
            CompanionVariant.Red => "Red rally",
            CompanionVariant.Blue => "Blue twirl",
            _ => "Green dash",
        };
        public float? Duration => 2.6f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            switch (variant)
            {
                case CompanionVariant.Red:
                    p.Expression = Expression.Curious;
                    p.Yaw = 0.22f * MathF.Sin(t * 2.5f);
                    p.Pitch = 0.12f * MathF.Sin(t * 4.5f);
                    p.LookX = t < 1.3f ? -1f : 1f;
                    p.OffsetY = -2f * Hop(t, 4f);
                    p.Particles = ParticleKind.Sparkles;
                    break;
                case CompanionVariant.Blue:
                    p.Expression = Expression.Happy;
                    p.Yaw = SmoothStep(t / 2.6f) * MathF.Tau;
                    p.OffsetY = -4.5f * Hop(t, 7f);
                    p.Roll = 0.12f * MathF.Sin(t * 7f);
                    p.TentacleSpread = 0.3f;
                    p.Particles = ParticleKind.Hearts;
                    break;
                case CompanionVariant.Green:
                    p.Expression = Expression.Excited;
                    p.OffsetX = 3.5f * MathF.Sin(t * 10f);
                    p.OffsetY = -3f * Hop(t, 10f);
                    p.Roll = -0.2f * MathF.Sin(t * 10f);
                    p.TentacleSway = -0.35f * MathF.Sin(t * 10f);
                    p.Particles = ParticleKind.Sparkles;
                    break;
            }
            return p;
        }
    }

    private sealed class GroupGameClip(GroupGameKind kind, int slot) : IClip
    {
        public string Name => kind.ToString();
        public float? Duration => null;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            if (kind == GroupGameKind.Stare)
            {
                // A silly staring contest: both hold still and blink in sync, then "lose"
                // together and burst into giggles near the end.
                bool blinking = MathF.Sin(t * 0.9f + slot * 1.3f) > 0.88f;
                p.Expression = t > 6.5f ? Expression.Happy : blinking ? Expression.Blink : Expression.Neutral;
                p.LookX = c.LookDir.X;
                p.LookY = -c.LookDir.Y;
                p.TentacleAmplitude = 0.05f; p.TentacleSpeed = 1f;
                p.Particles = t > 6.5f ? ParticleKind.Sparkles : ParticleKind.None;
                return p;
            }
            p.Expression = kind == GroupGameKind.Pong ? Expression.Curious : Expression.Excited;
            p.LookX = c.LookDir.X;
            p.LookY = -c.LookDir.Y;
            p.TentacleAmplitude = kind == GroupGameKind.Chase ? 0.36f : 0.48f;
            p.TentacleSpeed = kind == GroupGameKind.Chase ? 7f : 10f;
            p.Roll = 0.14f * MathF.Sin(t * (kind == GroupGameKind.Spar ? 9f : 5f) + slot * MathF.PI);
            p.OffsetY = -3.5f * Hop(t + slot * 0.4f, kind == GroupGameKind.Spar ? 8f : 5f);
            p.Particles = kind == GroupGameKind.Pong ? ParticleKind.Notes : ParticleKind.Sparkles;
            return p;
        }
    }

    private sealed class PairPlayClip(int slot) : IClip
    {
        public string Name => "Pair play";
        public float? Duration => null;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            float beat = t * 7f + slot * MathF.PI;
            p.Expression = Expression.Love;
            p.OffsetY = -4f * MathF.Max(0, MathF.Sin(beat));
            p.Roll = 0.14f * MathF.Sin(t * 3.5f + slot * MathF.PI);
            p.Squash = 1f + 0.07f * MathF.Sin(beat);
            p.TentacleAmplitude = 0.38f;
            p.TentacleSpeed = 7f;
            p.TentacleSpread = 0.22f;
            p.LookX = c.LookDir.X;
            p.Particles = ParticleKind.Hearts;
            return p;
        }
    }

    private sealed class TrioPlayClip(int slot) : IClip
    {
        public string Name => "Trio carousel";
        public float? Duration => null;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            float beat = t * 9f + slot * MathF.Tau / 3f;
            p.Expression = Expression.Excited;
            p.OffsetY = -5f * MathF.Max(0, MathF.Sin(beat));
            p.Roll = 0.20f * MathF.Sin(t * 4f + slot * MathF.Tau / 3f);
            p.Yaw = 0.22f * MathF.Sin(t * 2.2f + slot * MathF.Tau / 3f);
            p.Squash = 1f + 0.08f * MathF.Sin(beat);
            p.TentacleAmplitude = 0.52f;
            p.TentacleSpeed = 11f;
            p.TentacleSpread = 0.36f;
            p.GoggleWiggle = 0.35f * MathF.Sin(beat);
            p.Particles = ParticleKind.Notes;
            return p;
        }
    }

    private sealed class IdleClip : IClip
    {
        public string Name => "Idle";
        public float? Duration => null;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            float v = c.Variation * 10f;
            p.Yaw = 0.18f * MathF.Sin(t * 0.37f + v);
            p.Roll = 0.05f * MathF.Sin(t * 0.83f + v);
            p.Pitch = 0.04f * MathF.Sin(t * 0.61f);
            // Occasional glances: the sum of two slow sines crosses +-0.5 only now and then.
            p.LookX = 0.9f * MathF.Sin(t * 0.29f + v) * MathF.Sin(t * 0.17f + 1f);
            p.GoggleWiggle = 0.14f * MathF.Sin(t * 1.7f);
            return p;
        }
    }

    private sealed class HappyClip : IClip
    {
        public string Name => "Happy";
        public float? Duration => 1.6f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            float env = Decay(t, 1.2f);
            p.Expression = Expression.Happy;
            p.OffsetY = -4.5f * Hop(t, 7.5f) * env;
            p.Squash = 1f + 0.10f * MathF.Sin(t * 15f) * env;
            p.Roll = 0.10f * MathF.Sin(t * 7.5f) * env;
            p.TentacleAmplitude = 0.45f; p.TentacleSpeed = 8f; p.TentacleSpread = 0.25f;
            p.GoggleWiggle = 0.4f * MathF.Sin(t * 12f) * env;
            p.Particles = ParticleKind.Sparkles;
            return p;
        }
    }

    private sealed class ExcitedClip : IClip
    {
        public string Name => "Excited";
        public float? Duration => 2.0f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            p.Expression = Expression.Excited;
            p.OffsetY = -5.5f * Hop(t, 11f);
            p.Roll = 0.16f * MathF.Sin(t * 11f);
            p.Squash = 1f + 0.08f * MathF.Cos(t * 22f);
            p.TentacleAmplitude = 0.55f; p.TentacleSpeed = 12f; p.TentacleSpread = 0.35f;
            p.GoggleWiggle = 0.5f * MathF.Sin(t * 16f);
            p.LookX = c.LookDir.X; p.LookY = -c.LookDir.Y;
            p.Particles = ParticleKind.Sparkles;
            return p;
        }
    }

    private sealed class SurprisedClip : IClip
    {
        public string Name => "Surprised";
        public float? Duration => 1.1f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            p.Expression = Expression.Surprised;
            // Quick squash, then a startled stretch upward that settles.
            p.Squash = t < 0.08f ? 0.88f : 1f + 0.13f * Decay(t - 0.08f, 4f) * MathF.Cos((t - 0.08f) * 10f);
            p.OffsetY = -4f * Decay(t, 3f) * MathF.Min(1f, t * 12f);
            p.TentacleSpread = 0.42f; p.TentacleAmplitude = 0.12f; p.TentacleSpeed = 14f;
            p.GoggleWiggle = 0.5f * Decay(t, 3f) * MathF.Sin(t * 30f);
            p.LookX = c.LookDir.X; p.LookY = -c.LookDir.Y;
            return p;
        }
    }

    private sealed class CuriousClip : IClip
    {
        public string Name => "Curious";
        public float? Duration => null;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            p.Expression = Expression.Curious;
            p.LookX = c.LookDir.X * 1.2f;
            p.LookY = -c.LookDir.Y * 1.2f;
            p.Yaw = c.LookDir.X * 0.5f;
            p.Pitch = c.LookDir.Y * 0.18f;
            p.Roll = (c.LookDir.X >= 0 ? -1f : 1f) * 0.16f * MathF.Min(1f, t * 3f); // head tilt
            p.TentacleAmplitude = 0.16f; p.TentacleSpeed = 2f;
            p.GoggleWiggle = 0.2f * MathF.Sin(t * 2.4f);
            return p;
        }
    }

    private sealed class DrowsyClip : IClip
    {
        public string Name => "Drowsy";
        public float? Duration => 4.5f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            p.Expression = MathF.Sin(t * 2.1f) > 0.85f ? Expression.Asleep : Expression.Sleepy; // nodding off
            p.Pitch = 0.08f + 0.10f * MathF.Max(0, MathF.Sin(t * 1.3f));
            p.BobAmplitude = 1.2f; p.BobSpeed = 1.3f;
            p.TentacleAmplitude = 0.10f; p.TentacleSpeed = 1.2f; p.TentacleSpread = 0.02f;
            // Yawn-stretch in the middle.
            float yawn = MathF.Max(0, MathF.Sin((t - 1.5f) * 1.6f)) * (t is > 1.5f and < 3.5f ? 1 : 0);
            p.Squash = 1f + 0.08f * yawn;
            return p;
        }
    }

    private sealed class SleepingClip : IClip
    {
        public string Name => "Sleeping";
        public float? Duration => null;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            p.Expression = Expression.Asleep;
            p.Pitch = 0.14f;
            p.Roll = 0.04f * MathF.Sin(t * 0.5f);
            p.Squash = 1f - 0.03f * (0.5f + 0.5f * MathF.Sin(t * 1.4f)); // slow breathing
            p.BobAmplitude = 1.0f; p.BobSpeed = 0.9f;
            p.TentacleAmplitude = 0.07f; p.TentacleSpeed = 0.8f; p.TentacleSpread = -0.05f;
            p.GoggleWiggle = 0.25f; // goggles tilt to one side
            p.Particles = ParticleKind.Zzz;
            return p;
        }
    }

    private sealed class WakingClip : IClip
    {
        public string Name => "Waking";
        public float? Duration => 2.4f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            p.Expression = t < 0.7f ? Expression.Sleepy : t < 0.9f ? Expression.Blink : t < 1.1f ? Expression.Sleepy
                         : t < 1.25f ? Expression.Blink : Expression.Neutral;
            float stretch = MathF.Sin(MathF.Min(1f, t / 1.6f) * MathF.PI);
            p.Squash = 1f + 0.13f * stretch;
            p.TentacleSpread = 0.42f * stretch;
            p.TentacleAmplitude = 0.08f + 0.15f * MathF.Min(1, t / 2.4f);
            p.Pitch = 0.14f * (1f - MathF.Min(1f, t / 1.2f)) - 0.08f * stretch;
            p.Roll = 0.10f * MathF.Sin(t * 5f) * stretch;
            p.GoggleWiggle = 0.25f * (1f - MathF.Min(1f, t)) + 0.3f * stretch * MathF.Sin(t * 10f);
            return p;
        }
    }

    private sealed class DanceClip : IClip
    {
        private const float Beat = 2.2f; // beats per second (~132 bpm)
        public string Name => "Dance";
        public float? Duration => 7.2f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            float w = MathF.PI * Beat;
            int beat = (int)(t * Beat);
            p.Expression = beat % 4 < 2 ? Expression.Happy : Expression.Excited;
            p.OffsetY = -4f * Hop(t, w);
            p.OffsetX = 3.5f * MathF.Sin(t * w * 0.5f);
            p.Roll = 0.24f * MathF.Sin(t * w * 0.5f);
            p.Squash = 1f + 0.09f * MathF.Cos(t * w * 2f);
            // Every 8 beats: a full spin over 2 beats (smooth-stepped).
            float cycle = t * Beat % 8f;
            float spin = cycle < 6f ? 0f : SmoothStep((cycle - 6f) / 2f) * MathF.Tau;
            p.Yaw = 0.35f * MathF.Sin(t * w * 0.25f) + spin;
            p.TentacleAmplitude = 0.5f; p.TentacleSpeed = 9f; p.TentacleSpread = 0.3f;
            p.TentacleSway = -0.4f * MathF.Sin(t * w * 0.5f);
            p.GoggleWiggle = 0.5f * MathF.Sin(t * w);
            p.Particles = ParticleKind.Notes;
            return p;
        }
    }

    private sealed class AffectionClip : IClip
    {
        public string Name => "Affection";
        public float? Duration => null;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            p.Expression = Expression.Love;
            p.Roll = 0.14f * MathF.Sin(t * 3f);
            p.Yaw = c.LookDir.X * 0.3f;
            p.Squash = 1f + 0.05f * MathF.Sin(t * 6f);
            p.BobAmplitude = 2f; p.BobSpeed = 3f;
            p.TentacleAmplitude = 0.35f; p.TentacleSpeed = 5f; p.TentacleSpread = 0.3f;
            p.GoggleWiggle = 0.3f * MathF.Sin(t * 6f);
            p.Particles = ParticleKind.Hearts;
            return p;
        }
    }

    private sealed class TravelClip : IClip
    {
        private readonly bool _happy;
        public TravelClip(bool happy) => _happy = happy;
        public string Name => _happy ? "Follow" : "Travel";
        public float? Duration => null;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            const float refSpeed = 160f;
            float vx = Math.Clamp(c.Velocity.X / refSpeed, -1.5f, 1.5f);
            float vy = Math.Clamp(c.Velocity.Y / refSpeed, -1.5f, 1.5f);
            float speed = MathF.Min(1.5f, c.Velocity.Length() / refSpeed);
            p.Expression = _happy ? Expression.Happy : Expression.Neutral;
            // The renderer's resting camera already favors the left side. Turn farther
            // to the right than to the left so both directions read at icon size.
            // Reach the full turn even at the slower wander speed.
            float turn = Math.Clamp(c.Velocity.X / 25f, -1f, 1f);
            p.Yaw = turn * (0.75f + 0.25f * turn);
            p.Roll = -vx * 0.16f;                 // lean into the direction of travel
            p.Pitch = vy * 0.10f;
            p.TentacleSway = -vx * 0.45f;         // tentacles trail behind
            p.TentacleTrail = -speed * 0.15f;
            p.TentacleAmplitude = 0.20f + 0.15f * speed;
            p.TentacleSpeed = 3f + 4f * speed;    // "swimming" strokes get faster with speed
            p.BobAmplitude = 1.2f; p.BobSpeed = 3.5f;
            p.LookX = MathF.Abs(vx) > 0.15f ? MathF.Sign(vx) : 0f;
            p.LookY = MathF.Abs(vy) > 0.3f ? -MathF.Sign(vy) * 0.6f : 0f;
            if (_happy) { p.LookX = c.LookDir.X; p.LookY = -c.LookDir.Y; }
            p.GoggleWiggle = -vx * 0.3f;
            return p;
        }
    }

    private sealed class DraggedClip : IClip
    {
        public string Name => "Dragged";
        public float? Duration => null;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            const float refSpeed = 600f;
            float vx = Math.Clamp(c.Velocity.X / refSpeed, -1f, 1f);
            float vy = Math.Clamp(c.Velocity.Y / refSpeed, -1f, 1f);
            p.Expression = t < 0.35f ? Expression.Surprised : Expression.Excited;
            p.Squash = 1.08f - vy * 0.04f;  // stretched like being held up
            p.Roll = -vx * 0.25f;
            p.TentacleSway = -vx * 0.6f;    // dangle behind the motion
            p.TentacleTrail = vy * 0.4f;
            p.TentacleAmplitude = 0.35f; p.TentacleSpeed = 11f; p.TentacleSpread = 0.2f;
            p.BobAmplitude = 0f;            // glued to the cursor
            p.GoggleWiggle = -vx * 0.5f + 0.2f * MathF.Sin(t * 14f);
            return p;
        }
    }

    /// <summary>Ambient reaction while music is detected playing: a little head-bob sway.</summary>
    private sealed class GroovingClip : IClip
    {
        public string Name => "Grooving";
        public float? Duration => 3.2f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            float beat = t * 3.4f;
            p.Expression = Expression.Happy;
            p.OffsetY = -2.5f * Hop(beat, 1f);
            p.Roll = 0.14f * MathF.Sin(beat * MathF.PI);
            p.Yaw = 0.10f * MathF.Sin(beat * MathF.PI * 0.5f);
            p.TentacleSway = 0.3f * MathF.Sin(beat * MathF.PI);
            p.TentacleAmplitude = 0.3f; p.TentacleSpeed = 5f;
            p.GoggleWiggle = 0.25f * MathF.Sin(beat * MathF.PI * 2f);
            p.Particles = ParticleKind.Notes;
            return p;
        }
    }

    /// <summary>Ambient reaction while a video/movie is detected: settled, eyes fixed ahead.</summary>
    private sealed class WatchingClip : IClip
    {
        public string Name => "Watching";
        public float? Duration => 4.0f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            p.Expression = Expression.Curious;
            p.Pitch = 0.05f;
            p.LookY = 0.1f;
            p.TentacleAmplitude = 0.08f; p.TentacleSpeed = 1.2f; p.TentacleSpread = -0.05f;
            p.BobAmplitude = 0.6f; p.BobSpeed = 1f;
            p.GoggleWiggle = 0.1f * MathF.Sin(t * 0.8f);
            return p;
        }
    }

    /// <summary>Ambient reaction while a code editor/IDE is detected: a quiet, focused pose with a
    /// faint typing twitch.</summary>
    private sealed class CodingClip : IClip
    {
        public string Name => "Coding";
        public float? Duration => 4.0f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            p.Expression = Expression.Curious;
            p.Pitch = 0.12f;
            p.LookY = 0.25f;
            p.OffsetX = 0.6f * MathF.Sin(t * 14f);
            p.TentacleAmplitude = 0.10f; p.TentacleSpeed = 2f; p.TentacleSpread = 0.02f;
            p.GoggleWiggle = 0.08f * MathF.Sin(t * 3f);
            return p;
        }
    }

    /// <summary>Ambient reaction while a coding agent process is detected running: alert and
    /// sparkly, watching the screen do the work.</summary>
    private sealed class AgentBusyClip : IClip
    {
        public string Name => "Agent at work";
        public float? Duration => 3.0f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            p.Expression = Expression.Excited;
            p.OffsetY = -3f * Hop(t, 9f);
            p.Roll = 0.1f * MathF.Sin(t * 9f);
            p.Pitch = -0.06f;
            p.LookY = -0.3f;
            p.TentacleAmplitude = 0.4f; p.TentacleSpeed = 9f; p.TentacleSpread = 0.3f;
            p.GoggleWiggle = 0.35f * MathF.Sin(t * 10f);
            p.Particles = ParticleKind.Sparkles;
            return p;
        }
    }

    /// <summary>Ambient reaction while an agent lifecycle event reports it's waiting on the user:
    /// restless, glancing around rather than at the screen.</summary>
    private sealed class AgentWaitingClip : IClip
    {
        public string Name => "Impatient";
        public float? Duration => 4.0f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            p.Expression = Expression.Curious;
            float sweep = MathF.Sin(t * 2.2f);
            p.LookX = sweep * 0.8f;
            p.LookY = 0.1f;
            p.Yaw = 0.12f * sweep;
            p.Pitch = 0.08f;
            // A little foot-tap substitute: quicker, tighter tentacle taps than the usual sway.
            p.TentacleAmplitude = 0.18f + 0.1f * Hop(t, 6f);
            p.TentacleSpeed = 5f; p.TentacleSpread = 0.05f;
            p.GoggleWiggle = -0.15f * MathF.Sin(t * 3.5f);
            return p;
        }
    }

    /// <summary>One-off celebration when an agent lifecycle event reports success: a bigger,
    /// bouncier triumph than the everyday Happy clip.</summary>
    private sealed class AgentSucceededClip : IClip
    {
        public string Name => "Celebrating";
        public float? Duration => 2.0f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            float env = Decay(t, 1.0f);
            p.Expression = Expression.Excited;
            p.OffsetY = -6f * Hop(t, 8f) * env;
            p.OffsetX = 2f * MathF.Sin(t * 8f) * env;
            p.Squash = 1f + 0.14f * MathF.Sin(t * 16f) * env;
            p.Roll = 0.18f * MathF.Sin(t * 8f) * env;
            p.TentacleAmplitude = 0.6f; p.TentacleSpeed = 11f; p.TentacleSpread = 0.4f;
            p.GoggleWiggle = 0.5f * MathF.Sin(t * 14f) * env;
            p.Particles = ParticleKind.Sparkles;
            return p;
        }
    }

    /// <summary>One-off reaction when an agent lifecycle event reports failure: a puzzled
    /// head-tilt and a small deflating squash — not real distress.</summary>
    private sealed class AgentFailedClip : IClip
    {
        public string Name => "Puzzled";
        public float? Duration => 2.2f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            float env = Decay(t, 1.2f);
            p.Expression = Expression.Curious;
            p.Roll = 0.2f * MathF.Sin(t * 2.5f) * env - 0.05f;
            p.Pitch = 0.1f * MathF.Min(1f, t * 3f);
            p.Squash = 1f - 0.05f * env;
            p.LookX = 0.4f * MathF.Sin(t * 1.8f);
            p.TentacleAmplitude = 0.1f; p.TentacleSpeed = 1.5f; p.TentacleSpread = -0.05f;
            p.GoggleWiggle = -0.1f * MathF.Sin(t * 2f);
            return p;
        }
    }

    /// <summary>Solo reaction for when no other Puffling is around: a little search for company.</summary>
    private sealed class SearchingClip : IClip
    {
        public string Name => "Searching";
        public float? Duration => 5.0f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            p.Expression = t < 3.6f ? Expression.Curious : Expression.Sleepy;
            float sweep = MathF.Sin(t * 1.6f);
            p.Yaw = 0.3f * sweep;
            p.LookX = sweep;
            p.LookY = -0.15f;
            p.Pitch = 0.05f * MathF.Sin(t * 0.9f);
            p.TentacleAmplitude = 0.14f; p.TentacleSpeed = 2.4f;
            p.GoggleWiggle = 0.2f * MathF.Sin(t * 2f);
            return p;
        }
    }

    /// <summary>A brief, good-natured deflation for the one whose invitation to play was turned
    /// down — a small shrug, not real distress.</summary>
    private sealed class DeclinedClip : IClip
    {
        public string Name => "Declined";
        public float? Duration => 1.6f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            float env = Decay(t, 2.5f);
            p.Expression = Expression.Sleepy;
            p.Pitch = 0.12f * MathF.Min(1f, t * 3f);
            p.Squash = 1f - 0.05f * env;
            p.OffsetY = 1.5f * MathF.Min(1f, t * 2f);
            p.TentacleAmplitude = 0.08f; p.TentacleSpeed = 1.2f; p.TentacleSpread = -0.08f;
            p.GoggleWiggle = -0.15f * env;
            return p;
        }
    }

    /// <summary>Blue idle flourish: a brief shy retreat — tucking down and looking away rather
    /// than at anything in particular — before resuming as if nothing happened.</summary>
    private sealed class ShyClip : IClip
    {
        public string Name => "Shy";
        public float? Duration => 2.6f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            float tuck = MathF.Sin(MathF.Min(1f, t / 1.1f) * MathF.PI); // down and back up
            p.Expression = Expression.Sleepy;
            p.Pitch = 0.16f * tuck;
            p.Squash = 1f - 0.08f * tuck;
            p.OffsetY = 2f * tuck;
            p.Yaw = -0.18f * tuck;
            p.LookX = -0.5f * tuck; p.LookY = 0.2f * tuck;
            p.TentacleAmplitude = 0.08f; p.TentacleSpeed = 1.4f; p.TentacleSpread = -0.1f * tuck;
            return p;
        }
    }

    /// <summary>Red idle flourish: a steady, chin-up hover with almost no wobble — the composed
    /// leader surveying things rather than drifting idly.</summary>
    private sealed class HoveringClip : IClip
    {
        public string Name => "Hovering";
        public float? Duration => 3.2f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            p.Expression = Expression.Happy;
            p.BobAmplitude = 0.6f; p.BobSpeed = 1.1f;
            p.Pitch = -0.08f;
            p.OffsetY = -1.5f;
            p.LookX = c.LookDir.X * 0.3f; p.LookY = -c.LookDir.Y * 0.3f;
            p.TentacleAmplitude = 0.1f; p.TentacleSpeed = 1f; p.TentacleSpread = 0.04f;
            return p;
        }
    }

    /// <summary>Green idle flourish: a sly, lingering side-eye stare with a crooked smirk-tilt.</summary>
    private sealed class MischievousClip : IClip
    {
        public string Name => "Mischievous";
        public float? Duration => 2.8f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            p.Expression = Expression.Curious;
            float sweep = MathF.Sin(t * 1.1f);
            p.LookX = 0.7f * sweep; p.LookY = 0.1f;
            p.Roll = 0.14f;
            p.Pitch = 0.05f;
            p.TentacleAmplitude = 0.12f; p.TentacleSpeed = 1.6f; p.TentacleSpread = -0.06f;
            p.GoggleWiggle = 0.25f * MathF.Sin(t * 2.3f);
            return p;
        }
    }

    private sealed class SettleClip : IClip
    {
        public string Name => "Settle";
        public float? Duration => 1.2f;
        public Pose Evaluate(float t, in ClipContext c)
        {
            var p = Pose.Rest;
            p.Expression = Expression.Happy;
            p.Squash = 1f - 0.11f * Decay(t, 4f) * MathF.Cos(t * 14f); // damped wobble on release
            p.TentacleAmplitude = 0.3f * Decay(t, 2f) + 0.2f; p.TentacleSpeed = 6f;
            p.GoggleWiggle = 0.45f * Decay(t, 3f) * MathF.Sin(t * 16f);
            return p;
        }
    }

    internal static float SmoothStep(float x)
    {
        x = Math.Clamp(x, 0f, 1f);
        return x * x * (3f - 2f * x);
    }
}
