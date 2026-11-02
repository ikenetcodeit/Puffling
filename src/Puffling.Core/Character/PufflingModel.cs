using System.Numerics;
using Puffling.Core.Animation;
using Puffling.Core.Rendering;

namespace Puffling.Core.Character;

/// <summary>
/// A procedural, Happy Ghast-inspired fan skin: pale cube, broad smile, short tentacles,
/// leather harness and raised blue goggles. Built purely from textured boxes.
///
/// Model units: 1 unit = 1 texel. Body spans x -6..6, y -5..5, z -5..5. Front face is +z.
/// Parts are pre-allocated once; <see cref="Pose"/> updates only rewrite their transforms,
/// so steady-state rendering allocates nothing.
/// </summary>
public sealed class PufflingModel
{
    public const int TentacleCount = 6;
    public const int SegmentsPerTentacle = 3;
    public const float SegmentLength = 1.5f;
    /// <summary>Per-segment bend limit. The fit calculation relies on it.</summary>
    public const float MaxSegmentAngle = 0.42f;

    private static readonly Vector3[] TentacleRoots =
    {
        new(-4f, -5f, 3f), new(0f, -5f, 3f), new(4f, -5f, 3f),
        new(-4f, -5f, -3f), new(0f, -5f, -3f), new(4f, -5f, -3f),
    };

    private readonly FaceTextures _faces = new();
    private readonly BoxPart _body, _harnessTop, _harnessLeft, _harnessRight, _harnessFront,
        _blueLeft, _blueRight, _goggleLeft, _goggleRight, _lensLeft, _lensRight;
    private readonly BoxPart[,] _segments = new BoxPart[TentacleCount, SegmentsPerTentacle];
    private readonly List<BoxPart> _all = new();

    public IReadOnlyList<BoxPart> Parts => _all;

    public PufflingModel(CompanionVariant variant = CompanionVariant.Classic)
    {
        var side = Tex(10, 10, (x, y) => y == 0 ? Palette.BodyLight : y >= 8 ? Palette.BodyShadow : Palette.Body, 3);
        var back = Tex(12, 10, (x, y) => y == 0 ? Palette.BodyLight : y >= 8 ? Palette.BodyShadow : Palette.Body, 5);
        var top = Tex(12, 10, (x, y) => Palette.BodyLight, 7);
        var bottom = Tex(12, 10, (x, y) => Palette.BodyShadow, 9);
        var leather = Tex(4, 2, (x, y) => y == 1 ? Palette.HarnessDark : Palette.Harness, 13);
        var leatherDark = Tex(2, 2, (x, y) => Palette.HarnessDark, 15);
        var blue = Tex(2, 2, (x, y) => Palette.Accent(variant), 17);
        var frontTrim = variant == CompanionVariant.Classic ? leather :
            Tex(12, 2, (x, y) => x is > 1 and < 10 ? Palette.Accent(variant) : Palette.Harness, 13);
        var glass = Tex(2, 2, (x, y) => x == 0 ? Palette.GoggleLight : Palette.Lens(variant), 19);
        var seg = new[]
        {
            Tex(2, 2, (x, y) => Palette.Tentacle0, 21),
            Tex(2, 2, (x, y) => Palette.Tentacle1, 22),
            Tex(2, 2, (x, y) => y == 1 ? Palette.Tentacle2Dark : Palette.Tentacle2, 23),
        };

        _body = new BoxPart(new(-6, -5, -5), new(6, 5, 5))
            .Set(BoxFace.Front, TexRegion.Whole(_faces.Get(Expression.Neutral, 0, 0)))
            .Set(BoxFace.Back, TexRegion.Whole(back))
            .Set(BoxFace.Left, TexRegion.Whole(side)).Set(BoxFace.Right, TexRegion.Whole(side))
            .Set(BoxFace.Top, TexRegion.Whole(top)).Set(BoxFace.Bottom, TexRegion.Whole(bottom));
        _harnessTop = new BoxPart(new(-6.1f, 4.45f, -5.1f), new(6.1f, 5.4f, 5.1f)).SetAll(TexRegion.Whole(leather));
        _harnessLeft = new BoxPart(new(-6.35f, -0.4f, -3.8f), new(-5.65f, 4.6f, 3.8f)).SetAll(TexRegion.Whole(leather));
        _harnessRight = new BoxPart(new(5.65f, -0.4f, -3.8f), new(6.35f, 4.6f, 3.8f)).SetAll(TexRegion.Whole(leather));
        _harnessFront = new BoxPart(new(-6f, 3.8f, 5f), new(6f, 4.6f, 5.38f)).SetAll(TexRegion.Whole(frontTrim));
        _blueLeft = new BoxPart(new(-6.45f, 2.4f, -2.9f), new(-6.3f, 4.25f, 2.9f)).SetAll(TexRegion.Whole(blue));
        _blueRight = new BoxPart(new(6.3f, 2.4f, -2.9f), new(6.45f, 4.25f, 2.9f)).SetAll(TexRegion.Whole(blue));
        _goggleLeft = new BoxPart(new(-5.1f, 4.45f, 5.3f), new(-0.4f, 6.5f, 5.85f)).SetAll(TexRegion.Whole(leatherDark));
        _goggleRight = new BoxPart(new(0.4f, 4.45f, 5.3f), new(5.1f, 6.5f, 5.85f)).SetAll(TexRegion.Whole(leatherDark));
        _lensLeft = new BoxPart(new(-4.45f, 4.8f, 5.85f), new(-1.05f, 6.15f, 5.98f)).SetAll(TexRegion.Whole(glass));
        _lensRight = new BoxPart(new(1.05f, 4.8f, 5.85f), new(4.45f, 6.15f, 5.98f)).SetAll(TexRegion.Whole(glass));

        _all.AddRange(new[] { _body, _harnessTop, _harnessLeft, _harnessRight, _harnessFront,
            _blueLeft, _blueRight, _goggleLeft, _goggleRight, _lensLeft, _lensRight });
        float[] widths = { 1.65f, 1.45f, 1.2f };
        for (int t = 0; t < TentacleCount; t++)
            for (int s = 0; s < SegmentsPerTentacle; s++)
            {
                float hw = widths[s] / 2f;
                // Segments hang downward from their pivot (y from -length to 0), overlapping slightly to avoid gaps.
                _segments[t, s] = new BoxPart(new(-hw, -SegmentLength - 0.15f, -hw), new(hw, 0.1f, hw)).SetAll(TexRegion.Whole(seg[s]));
                _all.Add(_segments[t, s]);
            }
    }

    /// <summary>Positions every part for this pose.</summary>
    /// <param name="tentaclePhase">Integrated wave phase from the animator (radians).</param>
    /// <param name="segmentOverride">Fit calculation only: force all segment angles (x, z).</param>
    public void Apply(in Pose pose, float tentaclePhase, Vector2? segmentOverride = null)
    {
        float sq = pose.Squash;
        float xz = 1f / MathF.Sqrt(sq);
        Matrix4x4 body =
            Matrix4x4.CreateScale(xz, sq, xz) *
            Matrix4x4.CreateRotationY(pose.Yaw) *
            Matrix4x4.CreateRotationX(pose.Pitch) *
            Matrix4x4.CreateRotationZ(pose.Roll);

        _body.Transform = body;
        _body.Faces[(int)BoxFace.Front] = TexRegion.Whole(_faces.Get(pose.Expression, pose.LookX, pose.LookY));
        _harnessTop.Transform = body;
        _harnessLeft.Transform = body;
        _harnessRight.Transform = body;
        _harnessFront.Transform = body;
        _blueLeft.Transform = body;
        _blueRight.Transform = body;
        Matrix4x4 goggles = Matrix4x4.CreateTranslation(0, -5.25f, -5.6f) *
            Matrix4x4.CreateRotationZ(pose.GoggleWiggle * 0.25f) *
            Matrix4x4.CreateTranslation(0, 5.25f, 5.6f) * body;
        _goggleLeft.Transform = goggles;
        _goggleRight.Transform = goggles;
        _lensLeft.Transform = goggles;
        _lensRight.Transform = goggles;

        for (int t = 0; t < TentacleCount; t++)
        {
            Vector3 root = TentacleRoots[t];
            float side = MathF.Sign(root.X);
            float phaseOffset = t * 1.7f;
            Matrix4x4 parent = Matrix4x4.CreateTranslation(root) * body;
            for (int s = 0; s < SegmentsPerTentacle; s++)
            {
                float ax, az;
                if (segmentOverride is { } o) { ax = o.X; az = o.Y * side; }
                else
                {
                    float w = MathF.Sin(tentaclePhase + phaseOffset - s * 0.9f);
                    float gain = 0.55f + 0.45f * s; // tips move more than roots: whip-like
                    ax = pose.TentacleTrail / SegmentsPerTentacle + pose.TentacleAmplitude * gain * w;
                    az = side * (pose.TentacleSpread / SegmentsPerTentacle * (s == 0 ? 2f : 0.5f)
                                 + pose.TentacleAmplitude * 0.35f * gain * MathF.Cos(tentaclePhase * 0.8f + phaseOffset - s))
                         + pose.TentacleSway / SegmentsPerTentacle;
                    ax = Math.Clamp(ax, -MaxSegmentAngle, MaxSegmentAngle);
                    az = Math.Clamp(az, -MaxSegmentAngle, MaxSegmentAngle);
                }
                // Segment pivot: rotate, then hang from parent's pivot.
                Matrix4x4 local = Matrix4x4.CreateRotationX(ax) * Matrix4x4.CreateRotationZ(az);
                Matrix4x4 world = local * parent;
                _segments[t, s].Transform = world;
                parent = Matrix4x4.CreateTranslation(0, -SegmentLength, 0) * world;
            }
        }
    }

    private static Texture Tex(int w, int h, Func<int, int, uint> color, int seed)
    {
        var t = new Texture(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                t[x, y] = Palette.Jitter(color(x, y), x, y, seed, 7);
        return t;
    }
}
