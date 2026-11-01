namespace Puffling.Core.Animation;

public enum Expression
{
    Neutral,
    Happy,       // ^ ^ eyes, open smile
    Excited,     // big sparkly eyes, wide smile
    Surprised,   // round eyes, "o" mouth
    Curious,     // pupils follow look direction, small mouth
    Sleepy,      // half-closed eyes
    Asleep,      // closed eyes
    Love,        // heart eyes
    Blink,       // momentary closed eyes (layered by the blink controller)
}

public enum ParticleKind { None, Zzz, Hearts, Notes, Sparkles }

/// <summary>
/// The complete, blendable description of how the character looks at one instant.
/// Clips produce target poses; the <see cref="Animator"/> blends between them.
///
/// Phase-like quantities (bob phase, tentacle phase) are NOT in the pose: blending two
/// phases produces jumps. Instead poses carry *speeds*, which blend safely, and the
/// animator integrates them into phases.
/// </summary>
public struct Pose
{
    // Body orientation in radians (yaw = turn left/right, pitch = nod, roll = tilt).
    public float Yaw, Pitch, Roll;
    /// <summary>1 = rest. &gt;1 stretches vertically, &lt;1 squashes (volume roughly preserved).</summary>
    public float Squash;

    // Floating motion, in LOGICAL pixels at 48 px size. Applied to the window position,
    // never inside the canvas, so the character's footprint never grows.
    public float BobAmplitude, BobSpeed;   // speed in radians/sec
    public float OffsetX, OffsetY;          // extra positional offset (hops, sways)

    // Tentacles.
    public float TentacleAmplitude;         // radians of swing
    public float TentacleSpeed;             // radians/sec of the wave
    public float TentacleSpread;            // radians splayed outward
    public float TentacleTrail;             // radians swept backward (front/back)
    public float TentacleSway;              // radians swept sideways, same for all (travel / drag)

    // Face.
    public Expression Expression;
    public float LookX, LookY;              // -1..1, where the pupils point
    public float GoggleWiggle;              // subtle tilt of the raised goggles

    public ParticleKind Particles;

    public static Pose Rest => new()
    {
        Squash = 1f,
        BobAmplitude = 1.5f, BobSpeed = 2.2f,
        TentacleAmplitude = 0.22f, TentacleSpeed = 2.4f, TentacleSpread = 0.08f,
        Expression = Expression.Neutral,
    };

    public static Pose Lerp(in Pose a, in Pose b, float t)
    {
        static float L(float x, float y, float t) => x + (y - x) * t;
        return new Pose
        {
            Yaw = L(a.Yaw, b.Yaw, t), Pitch = L(a.Pitch, b.Pitch, t), Roll = L(a.Roll, b.Roll, t),
            Squash = L(a.Squash, b.Squash, t),
            BobAmplitude = L(a.BobAmplitude, b.BobAmplitude, t), BobSpeed = L(a.BobSpeed, b.BobSpeed, t),
            OffsetX = L(a.OffsetX, b.OffsetX, t), OffsetY = L(a.OffsetY, b.OffsetY, t),
            TentacleAmplitude = L(a.TentacleAmplitude, b.TentacleAmplitude, t),
            TentacleSpeed = L(a.TentacleSpeed, b.TentacleSpeed, t),
            TentacleSpread = L(a.TentacleSpread, b.TentacleSpread, t),
            TentacleTrail = L(a.TentacleTrail, b.TentacleTrail, t),
            TentacleSway = L(a.TentacleSway, b.TentacleSway, t),
            LookX = L(a.LookX, b.LookX, t), LookY = L(a.LookY, b.LookY, t),
            GoggleWiggle = L(a.GoggleWiggle, b.GoggleWiggle, t),
            // Discrete channels switch at the midpoint of a blend.
            Expression = t < 0.5f ? a.Expression : b.Expression,
            Particles = t < 0.5f ? a.Particles : b.Particles,
        };
    }

    /// <summary>Hard limits that the size-fitting relies on. Intensity scaling can never push
    /// the model outside these, which is what guarantees the icon-sized footprint.</summary>
    public const float MaxTilt = 0.30f;        // pitch / roll
    public const float MinSquash = 0.86f, MaxSquash = 1.14f;
    public const float MaxTentacleAngle = 0.75f;

    public void Clamp()
    {
        Pitch = Math.Clamp(Pitch, -MaxTilt, MaxTilt);
        Roll = Math.Clamp(Roll, -MaxTilt, MaxTilt);
        Squash = Math.Clamp(Squash, MinSquash, MaxSquash);
        TentacleAmplitude = Math.Clamp(TentacleAmplitude, 0f, 0.6f);
        TentacleSpread = Math.Clamp(TentacleSpread, -0.2f, 0.45f);
        TentacleTrail = Math.Clamp(TentacleTrail, -0.6f, 0.6f);
        TentacleSway = Math.Clamp(TentacleSway, -0.6f, 0.6f);
        LookX = Math.Clamp(LookX, -1f, 1f);
        LookY = Math.Clamp(LookY, -1f, 1f);
        GoggleWiggle = Math.Clamp(GoggleWiggle, -0.6f, 0.6f);
    }
}
