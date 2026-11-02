using System.Numerics;

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
    public static IClip PairPlay(int slot) => new PairPlayClip(slot);
    public static IClip TrioPlay(int slot) => new TrioPlayClip(slot);

    internal static float Hop(float t, float freq) => MathF.Abs(MathF.Sin(t * freq));
    internal static float Decay(float t, float rate) => MathF.Exp(-t * rate);

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
