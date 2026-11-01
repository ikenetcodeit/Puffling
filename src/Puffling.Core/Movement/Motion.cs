using System.Numerics;

namespace Puffling.Core.Movement;

/// <summary>
/// Kinematic "arrive" steering for a floating creature.
///
/// Each frame the desired velocity points at the target with a speed that ramps down as
/// sqrt(2·decel·distance) — the fastest speed from which we can still stop in time — and the
/// actual velocity moves toward it by at most accel·dt. Result: smooth ease-in/ease-out with
/// no overshoot, at any frame rate. A small perpendicular wobble keeps paths from looking
/// ruler-straight.
/// </summary>
public sealed class Motion
{
    public Vector2 Position;
    public Vector2 Velocity;
    public Vector2? Target { get; private set; }
    public float MaxSpeed { get; private set; } = 60f;
    public float Accel { get; private set; } = 120f;
    /// <summary>Perpendicular wobble as a fraction of current speed.</summary>
    public float Wobble { get; set; } = 0.18f;

    private float _time;

    public bool Arrived => Target is null
        ? Velocity.LengthSquared() < 1f
        : Vector2.Distance(Position, Target.Value) < 1.5f && Velocity.Length() < 8f;

    public void MoveTo(Vector2 target, float maxSpeed, float accel)
    {
        Target = target;
        MaxSpeed = Math.Max(1f, maxSpeed);
        Accel = Math.Max(1f, accel);
    }

    /// <summary>Stop steering; glide to a halt using <paramref name="brake"/> deceleration.</summary>
    public void Release(float brake)
    {
        Target = null;
        Accel = Math.Max(1f, brake);
    }

    public void Teleport(Vector2 p)
    {
        Position = p;
        Velocity = Vector2.Zero;
        Target = null;
    }

    public void Update(float dt)
    {
        if (dt <= 0) return;
        _time += dt;
        Vector2 desired = Vector2.Zero;
        if (Target is { } t)
        {
            Vector2 to = t - Position;
            float dist = to.Length();
            if (dist > 0.01f)
            {
                float stoppingSpeed = MathF.Sqrt(2f * Accel * 0.8f * dist);
                desired = to / dist * MathF.Min(MaxSpeed, stoppingSpeed);
            }
        }

        Vector2 dv = desired - Velocity;
        float maxDv = Accel * dt;
        float len = dv.Length();
        if (len > maxDv) dv *= maxDv / len;
        Velocity += dv;

        Vector2 step = Velocity * dt;
        float speed = Velocity.Length();
        if (speed > 1f && Wobble > 0f)
        {
            var perp = new Vector2(-Velocity.Y, Velocity.X) / speed;
            float w = MathF.Sin(_time * 1.9f) * 0.6f + MathF.Sin(_time * 3.7f + 1.3f) * 0.4f;
            step += perp * (w * Wobble * speed * dt);
        }
        Position += step;

        if (Target is { } tt && Vector2.Distance(Position, tt) < 0.75f && speed < 6f)
        {
            Position = tt;
            Velocity = Vector2.Zero;
        }
    }
}
