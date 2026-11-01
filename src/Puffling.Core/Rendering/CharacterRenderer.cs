using System.Numerics;
using Puffling.Core.Animation;
using Puffling.Core.Character;

namespace Puffling.Core.Rendering;

/// <summary>
/// Renders the character into a square canvas of exactly <c>sizePx</c> x <c>sizePx</c>.
///
/// Size guarantee (spec section 2 / 15): the model-to-pixel scale is derived ONCE from the
/// worst-case extents of every pose the animation system is allowed to produce (any yaw, max
/// tilt, max squash, max tentacle bend). Animations therefore never need a bigger window —
/// dancing, travelling and dragging all happen inside the same icon-sized box. Bobbing and hops
/// move the *window*, not pixels inside it.
/// </summary>
public sealed class CharacterRenderer
{
    public const float CameraPitch = 0.24f;  // look slightly down so the top face reads as 3D
    public const float CameraYaw = -0.30f;   // gentle 3/4 view

    private readonly PufflingModel _model = new();
    private FrameBuffer? _fb;
    private static readonly Lazy<(float maxAbsX, float minY, float maxY)> Extents = new(ComputeExtents);

    public FrameBuffer Render(in Pose pose, float tentaclePhase, float particleTime, int sizePx, bool particles = true)
    {
        sizePx = Math.Clamp(sizePx, 16, 512);
        if (_fb is null || _fb.Width != sizePx) _fb = new FrameBuffer(sizePx, sizePx);
        _fb.Clear();

        var (ppu, ox, oy) = Layout(sizePx);
        Matrix4x4 view = Matrix4x4.CreateRotationY(CameraYaw) * Matrix4x4.CreateRotationX(CameraPitch) * Matrix4x4.CreateScale(ppu);

        Pose p = pose;
        p.Clamp();
        _model.Apply(p, tentaclePhase);
        var parts = _model.Parts;
        for (int i = 0; i < parts.Count; i++) Rasterizer.DrawBox(_fb, parts[i], view, ox, oy); // indexed: no enumerator boxing

        if (particles && p.Particles != ParticleKind.None)
            Particles.Draw(_fb, p.Particles, particleTime);
        return _fb;
    }

    /// <summary>Pixels per model unit and canvas position of the model origin for a canvas size.</summary>
    public static (float ppu, float originX, float originY) Layout(int sizePx)
    {
        var (maxAbsX, minY, maxY) = Extents.Value;
        float usable = sizePx - 2f; // 1 px safety border on each side
        float ppu = MathF.Min(usable / (2f * maxAbsX), usable / (maxY - minY));
        float originX = sizePx / 2f;
        float usedH = (maxY - minY) * ppu;
        float originY = 1f + (usable - usedH) / 2f + maxY * ppu;
        return (ppu, originX, originY);
    }

    /// <summary>Brute-force worst case over the allowed pose envelope (runs once, ~ms).</summary>
    private static (float, float, float) ComputeExtents()
    {
        var model = new PufflingModel();
        Matrix4x4 cam = Matrix4x4.CreateRotationY(CameraYaw) * Matrix4x4.CreateRotationX(CameraPitch);
        float maxAbsX = 0, minY = float.MaxValue, maxY = float.MinValue;
        float a = PufflingModel.MaxSegmentAngle;
        var segs = new Vector2[] { new(0, 0), new(a, a), new(-a, a), new(a, -a), new(-a, -a), new(0, a), new(a, 0), new(-a, 0) };
        float[] tilts = { -Pose.MaxTilt, 0, Pose.MaxTilt };
        float[] squashes = { Pose.MinSquash, 1f, Pose.MaxSquash };
        float[] wiggles = { -0.6f, 0.6f };
        for (int yi = 0; yi < 24; yi++)
            foreach (float pitch in tilts)
                foreach (float roll in tilts)
                    foreach (float sq in squashes)
                        foreach (var seg in segs)
                            foreach (float wig in wiggles)
                            {
                                var pose = Pose.Rest;
                                pose.Yaw = yi * MathF.Tau / 24f; pose.Pitch = pitch; pose.Roll = roll; pose.Squash = sq;
                                pose.SproutWiggle = wig;
                                model.Apply(pose, 0, seg);
                                foreach (var part in model.Parts)
                                {
                                    Matrix4x4 m = part.Transform * cam;
                                    for (int i = 0; i < 8; i++)
                                    {
                                        var v = Vector3.Transform(new Vector3(
                                            (i & 1) != 0 ? part.Max.X : part.Min.X,
                                            (i & 2) != 0 ? part.Max.Y : part.Min.Y,
                                            (i & 4) != 0 ? part.Max.Z : part.Min.Z), m);
                                        maxAbsX = MathF.Max(maxAbsX, MathF.Abs(v.X));
                                        minY = MathF.Min(minY, v.Y);
                                        maxY = MathF.Max(maxY, v.Y);
                                    }
                                }
                            }
        // The yaw sweep is sampled; add a small margin for angles between samples.
        return (maxAbsX * 1.01f, minY * 1.01f, maxY * 1.01f);
    }
}
