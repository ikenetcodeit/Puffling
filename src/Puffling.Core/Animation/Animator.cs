using System.Numerics;

namespace Puffling.Core.Animation;

/// <summary>
/// Plays one clip at a time and cross-fades between clips so changes are never abrupt.
///
/// Layers, applied in order each frame:
///   1. Base clip (with cross-fade from a frozen snapshot of the previous output).
///   2. Intensity scaling (user setting) — scales deviation from rest, then hard clamps.
///   3. Blink layer — momentary eyelid closes on open-eyed expressions.
/// Phase integration (bobbing, tentacle wave, particles) happens here, from blended speeds.
/// </summary>
public sealed class Animator
{
    private readonly Random _rng;
    private Pose _from = Pose.Rest;
    private float _blend = 1f, _blendDuration = 0.3f;
    private float _blinkTimer, _blinkRemaining;
    private float _variation;

    public IClip Clip { get; private set; } = Clips.Idle;
    public float ClipTime { get; private set; }
    public Pose Current { get; private set; } = Pose.Rest;
    public float BobPhase { get; private set; }
    public float TentaclePhase { get; private set; }
    public float ParticleTime { get; private set; }
    /// <summary>0.3 .. 1.5, from settings.</summary>
    public float Intensity { get; set; } = 1f;

    public Animator(Random rng)
    {
        _rng = rng;
        _blinkTimer = NextBlinkDelay();
    }

    /// <summary>True when the current clip has a duration and has played past it.</summary>
    public bool ClipFinished => Clip.Duration is { } d && ClipTime >= d;

    public void Play(IClip clip, float blendSeconds = 0.3f, bool restartIfSame = false)
    {
        if (ReferenceEquals(clip, Clip) && !restartIfSame) return;
        _from = Current;              // freeze what is on screen now; blend from it
        Clip = clip;
        ClipTime = 0f;
        _blend = blendSeconds <= 0 ? 1f : 0f;
        _blendDuration = Math.Max(0.01f, blendSeconds);
        _variation = (float)_rng.NextDouble();
        if (clip.Particles() != Current.Particles) ParticleTime = 0f;
    }

    public void Update(float dt, in ClipContext ctx)
    {
        dt = Math.Clamp(dt, 0f, 0.1f);
        ClipTime += dt;
        _blend = Math.Min(1f, _blend + dt / _blendDuration);

        var c = ctx;
        c.Variation = _variation;
        Pose target = Clip.Evaluate(ClipTime, c);
        Pose p = _blend >= 1f ? target : Pose.Lerp(_from, target, Clips.SmoothStep(_blend));

        p = ApplyIntensity(p, Intensity);
        p.Clamp();

        // Integrate phases from blended speeds (no phase discontinuities when clips change).
        BobPhase = Wrap(BobPhase + p.BobSpeed * dt);
        TentaclePhase = Wrap(TentaclePhase + p.TentacleSpeed * dt);
        ParticleTime += dt;

        p.Expression = ApplyBlink(p.Expression, dt);
        Current = p;
    }

    /// <summary>Window offset in logical px (at 48 px scale): bobbing + clip offsets.</summary>
    public Vector2 WindowOffset => new(Current.OffsetX, Current.OffsetY + MathF.Sin(BobPhase) * Current.BobAmplitude);

    private static Pose ApplyIntensity(Pose p, float k)
    {
        if (MathF.Abs(k - 1f) < 1e-3f) return p;
        p.Pitch *= k; p.Roll *= k;
        p.Squash = 1f + (p.Squash - 1f) * k;
        p.BobAmplitude *= k;
        p.OffsetX *= k; p.OffsetY *= k;
        p.TentacleAmplitude *= k;
        p.SproutWiggle *= k;
        return p;
    }

    private Expression ApplyBlink(Expression e, float dt)
    {
        bool canBlink = e is Expression.Neutral or Expression.Curious or Expression.Excited;
        if (_blinkRemaining > 0f)
        {
            _blinkRemaining -= dt;
            return canBlink ? Expression.Blink : e;
        }
        _blinkTimer -= dt;
        if (_blinkTimer <= 0f)
        {
            _blinkTimer = NextBlinkDelay();
            if (canBlink) { _blinkRemaining = 0.13f; return Expression.Blink; }
        }
        return e;
    }

    private float NextBlinkDelay() => 2.2f + (float)_rng.NextDouble() * 4.5f;
    private static float Wrap(float a) => a > 1000f ? a - 100f * MathF.Tau : a;
}

internal static class ClipExtensions
{
    /// <summary>Particles a clip shows at its start (used to restart particle timing cleanly).</summary>
    public static ParticleKind Particles(this IClip clip) => clip.Evaluate(0f, default).Particles;
}
