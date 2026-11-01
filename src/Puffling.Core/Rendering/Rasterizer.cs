using System.Numerics;

namespace Puffling.Core.Rendering;

public enum BoxFace { Front = 0, Back = 1, Left = 2, Right = 3, Top = 4, Bottom = 5 }

/// <summary>A rectangular area (in texels) of a texture mapped onto one box face.</summary>
public readonly record struct TexRegion(Texture Texture, int X, int Y, int W, int H)
{
    public static TexRegion Whole(Texture t) => new(t, 0, 0, t.Width, t.Height);
}

/// <summary>An axis-aligned box in its own local space, placed in the model by <see cref="Transform"/>.
/// This is the only primitive: Minecraft-style characters are made entirely of textured cuboids.</summary>
public sealed class BoxPart
{
    public Vector3 Min;
    public Vector3 Max;
    public Matrix4x4 Transform = Matrix4x4.Identity;
    /// <summary>Indexed by <see cref="BoxFace"/>. A null face is not drawn.</summary>
    public readonly TexRegion?[] Faces = new TexRegion?[6];

    public BoxPart(Vector3 min, Vector3 max) { Min = min; Max = max; }

    public BoxPart SetAll(TexRegion r) { for (int i = 0; i < 6; i++) Faces[i] = r; return this; }
    public BoxPart Set(BoxFace f, TexRegion r) { Faces[(int)f] = r; return this; }
}

/// <summary>
/// Tiny orthographic software rasterizer for textured boxes with a depth buffer and flat
/// per-face lighting. At 32-96 px a CPU rasterizer is far cheaper than spinning up a GPU
/// device + swap chain + readback for a layered window, and it is pixel-exact and testable.
/// </summary>
public static class Rasterizer
{
    // Corner index order per face: TL, TR, BR, BL as seen from outside the face.
    // Corner encoding: bit0 = x (0 min / 1 max), bit1 = y, bit2 = z.
    private static readonly int[][] FaceCorners =
    {
        new[] { 0b110, 0b111, 0b101, 0b100 }, // Front  (+z)
        new[] { 0b011, 0b010, 0b000, 0b001 }, // Back   (-z)
        new[] { 0b010, 0b110, 0b100, 0b000 }, // Left   (-x)
        new[] { 0b111, 0b011, 0b001, 0b101 }, // Right  (+x)
        new[] { 0b010, 0b011, 0b111, 0b110 }, // Top    (+y)
        new[] { 0b100, 0b101, 0b001, 0b000 }, // Bottom (-y)
    };

    private static readonly Vector3 LightDir = Vector3.Normalize(new Vector3(-0.45f, 0.8f, 0.55f));

    private struct SV { public float X, Y, Z, U, V; }

    /// <param name="view">Model-to-view transform (rotation + scale, in pixels). +y up, +z towards viewer.</param>
    /// <param name="originX">Canvas x of the model origin.</param>
    /// <param name="originY">Canvas y of the model origin.</param>
    public static void DrawBox(FrameBuffer fb, BoxPart box, in Matrix4x4 view, float originX, float originY)
    {
        Matrix4x4 m = box.Transform * view;
        Span<Vector3> c = stackalloc Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            var local = new Vector3(
                (i & 1) != 0 ? box.Max.X : box.Min.X,
                (i & 2) != 0 ? box.Max.Y : box.Min.Y,
                (i & 4) != 0 ? box.Max.Z : box.Min.Z);
            Vector3 v = Vector3.Transform(local, m);
            // To canvas space: y flips (screen y grows downward).
            c[i] = new Vector3(originX + v.X, originY - v.Y, v.Z);
            if (c[i].X < 0 || c[i].Y < 0 || c[i].X > fb.Width || c[i].Y > fb.Height) fb.ClippedVertices++;
        }

        for (int f = 0; f < 6; f++)
        {
            TexRegion? region = box.Faces[f];
            if (region is null) continue;
            int[] idx = FaceCorners[f];
            Vector3 tl = c[idx[0]], tr = c[idx[1]], br = c[idx[2]], bl = c[idx[3]];

            // Normal in view space (undo the screen y flip for the lighting/culling maths).
            var a = new Vector3(bl.X - tl.X, -(bl.Y - tl.Y), bl.Z - tl.Z);
            var b = new Vector3(tr.X - tl.X, -(tr.Y - tl.Y), tr.Z - tl.Z);
            Vector3 n = Vector3.Cross(a, b);
            float len = n.Length();
            if (len < 1e-6f) continue;
            n /= len;
            if (n.Z <= 0.02f) continue; // back-face (orthographic camera looks down -z)

            float shade = 0.60f + 0.40f * MathF.Max(0f, Vector3.Dot(n, LightDir));
            shade = MathF.Round(shade * 24f) / 24f; // quantised: steadier, more "blocky" lighting

            TexRegion r = region.Value;
            SV vTL = Make(tl, 0, 0), vTR = Make(tr, 1, 0), vBR = Make(br, 1, 1), vBL = Make(bl, 0, 1);
            FillTriangle(fb, vTL, vTR, vBR, r, shade);
            FillTriangle(fb, vTL, vBR, vBL, r, shade);
        }
    }

    private static SV Make(Vector3 p, float u, float v) => new() { X = p.X, Y = p.Y, Z = p.Z, U = u, V = v };

    private static float Edge(in SV a, in SV b, float px, float py) => (b.X - a.X) * (py - a.Y) - (b.Y - a.Y) * (px - a.X);

    private static void FillTriangle(FrameBuffer fb, SV v0, SV v1, SV v2, TexRegion r, float shade)
    {
        float area = Edge(v0, v1, v2.X, v2.Y);
        if (MathF.Abs(area) < 1e-5f) return;
        if (area < 0) { (v1, v2) = (v2, v1); area = -area; }

        int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(v0.X, MathF.Min(v1.X, v2.X))));
        int maxX = Math.Min(fb.Width - 1, (int)MathF.Ceiling(MathF.Max(v0.X, MathF.Max(v1.X, v2.X))));
        int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(v0.Y, MathF.Min(v1.Y, v2.Y))));
        int maxY = Math.Min(fb.Height - 1, (int)MathF.Ceiling(MathF.Max(v0.Y, MathF.Max(v1.Y, v2.Y))));
        if (minX > maxX || minY > maxY) return;

        Texture tex = r.Texture;
        float inv = 1f / area;
        const float eps = -1e-4f;

        for (int y = minY; y <= maxY; y++)
        {
            float py = y + 0.5f;
            int row = y * fb.Width;
            for (int x = minX; x <= maxX; x++)
            {
                float px = x + 0.5f;
                float w0 = Edge(v1, v2, px, py);
                float w1 = Edge(v2, v0, px, py);
                float w2 = Edge(v0, v1, px, py);
                if (w0 < eps || w1 < eps || w2 < eps) continue;
                w0 *= inv; w1 *= inv; w2 *= inv;

                float z = w0 * v0.Z + w1 * v1.Z + w2 * v2.Z;
                int i = row + x;
                if (z <= fb.Depth[i]) continue;

                float s = Math.Clamp(w0 * v0.U + w1 * v1.U + w2 * v2.U, 0.0005f, 0.9995f);
                float t = Math.Clamp(w0 * v0.V + w1 * v1.V + w2 * v2.V, 0.0005f, 0.9995f);
                int tx = r.X + (int)(s * r.W);
                int ty = r.Y + (int)(t * r.H);
                uint texel = tex.Pixels[ty * tex.Width + tx];
                if ((texel >> 24) == 0) continue; // cut-out

                fb.Depth[i] = z;
                fb.Color[i] = Shade(texel, shade);
            }
        }
    }

    private static uint Shade(uint argb, float k)
    {
        uint rr = (uint)Math.Min(255f, ((argb >> 16) & 0xFF) * k);
        uint gg = (uint)Math.Min(255f, ((argb >> 8) & 0xFF) * k);
        uint bb = (uint)Math.Min(255f, (argb & 0xFF) * k);
        return 0xFF000000u | (rr << 16) | (gg << 8) | bb; // opaque, so premultiplied == straight
    }
}
