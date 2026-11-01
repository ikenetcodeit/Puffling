using System.Numerics;
using Puffling.Core.Animation;
using Puffling.Core.Rendering;

namespace Puffling.Core.Character;

/// <summary>
/// The original voxel character: a mint "cloud-jelly" with a cloud puff and leaf sprout on top,
/// a pixel-art face, and four three-segment tentacles. Built purely from textured boxes.
///
/// Model units: 1 unit = 1 texel. Body spans x -6..6, y -5..5, z -5..5. Front face is +z.
/// Parts are pre-allocated once; <see cref="Pose"/> updates only rewrite their transforms,
/// so steady-state rendering allocates nothing.
/// </summary>
public sealed class PufflingModel
{
    public const int TentacleCount = 4;
    public const int SegmentsPerTentacle = 3;
    public const float SegmentLength = 2f;
    /// <summary>Per-segment bend limit. The fit calculation relies on it.</summary>
    public const float MaxSegmentAngle = 0.42f;

    private static readonly Vector3[] TentacleRoots =
    {
        new(-3.4f, -5f, 2.2f), new(3.4f, -5f, 2.2f),
        new(-3.4f, -5f, -2.2f), new(3.4f, -5f, -2.2f),
    };

    private readonly FaceTextures _faces = new();
    private readonly BoxPart _body, _cloud, _cloudSmall, _stem, _leafL, _leafR;
    private readonly BoxPart[,] _segments = new BoxPart[TentacleCount, SegmentsPerTentacle];
    private readonly List<BoxPart> _all = new();

    public IReadOnlyList<BoxPart> Parts => _all;

    public PufflingModel()
    {
        var side = Tex(10, 10, (x, y) => y == 0 ? Palette.BodyLight : y == 9 ? Palette.BodyShadow : Palette.Body, 3);
        var back = Tex(12, 10, (x, y) => y == 0 ? Palette.BodyLight : y == 9 ? Palette.BodyShadow : Palette.Body, 5);
        var top = Tex(12, 10, (x, y) => Palette.BodyLight, 7);
        var bottom = Tex(12, 10, (x, y) => Palette.BodyShadow, 9);
        var cloud = Tex(8, 6, (x, y) => y == 5 ? Palette.CloudShadow : Palette.Cloud, 13);
        var stem = Tex(1, 2, (x, y) => Palette.Stem, 1);
        var leaf = Tex(3, 1, (x, y) => x == 0 ? Palette.LeafDark : Palette.Leaf, 2);
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
        _cloud = new BoxPart(new(-4.5f, 5f, -3.5f), new(2.5f, 7f, 2.5f)).SetAll(TexRegion.Whole(cloud));
        _cloudSmall = new BoxPart(new(2.5f, 5f, -2.5f), new(5f, 6.2f, 1.5f)).SetAll(TexRegion.Whole(cloud));
        _stem = new BoxPart(new(-0.5f, 0f, -0.5f), new(0.5f, 2f, 0.5f)).SetAll(TexRegion.Whole(stem));
        _leafL = new BoxPart(new(-2.8f, 0f, -0.6f), new(0f, 0.9f, 0.6f)).SetAll(TexRegion.Whole(leaf));
        _leafR = new BoxPart(new(0f, 0f, -0.6f), new(2.8f, 0.9f, 0.6f)).SetAll(TexRegion.Whole(leaf));

        _all.AddRange(new[] { _body, _cloud, _cloudSmall, _stem, _leafL, _leafR });
        float[] widths = { 1.9f, 1.6f, 1.3f };
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
        _cloud.Transform = body;
        _cloudSmall.Transform = body;

        Matrix4x4 sprout = Matrix4x4.CreateRotationZ(pose.SproutWiggle) * Matrix4x4.CreateTranslation(-1f, 7f, -0.5f) * body;
        _stem.Transform = sprout;
        Matrix4x4 leafBase = Matrix4x4.CreateTranslation(0, 1.8f, 0) * sprout;
        _leafL.Transform = Matrix4x4.CreateRotationZ(-0.35f - pose.SproutWiggle * 0.5f) * leafBase;
        _leafR.Transform = Matrix4x4.CreateRotationZ(0.35f - pose.SproutWiggle * 0.5f) * leafBase;

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
