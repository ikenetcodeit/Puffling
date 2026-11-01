using Puffling.Core.Animation;
using Puffling.Core.Rendering;

namespace Puffling.Core.Character;

/// <summary>
/// Paints the 12x10 pixel-art Happy Ghast face for every expression. Faces are generated in code
/// and cached, so switching expressions costs nothing per frame.
/// Coordinates: x 0..11 left->right, y 0..9 top->bottom.
/// </summary>
public sealed class FaceTextures
{
    public const int W = 12, H = 10;
    private readonly Dictionary<(Expression, int, int), Texture> _cache = new();
    private readonly Texture _base;

    public FaceTextures()
    {
        _base = new Texture(W, H);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                uint c = y == 0 ? Palette.BodyLight : y == H - 1 ? Palette.BodyShadow : Palette.Body;
                _base[x, y] = Palette.Jitter(c, x, y, 11, 6);
            }
    }

    /// <summary>Look direction is quantised to whole texels: sub-texel pupil motion would blur
    /// the pixel art, and at 2 px/texel one-texel steps read clearly as "glancing".</summary>
    public Texture Get(Expression e, float lookX, float lookY)
    {
        int lx = (int)MathF.Round(Math.Clamp(lookX, -1f, 1f));
        int ly = (int)MathF.Round(Math.Clamp(lookY, -1f, 1f));
        if (!UsesLook(e)) { lx = 0; ly = 0; }
        var key = (e, lx, ly);
        if (!_cache.TryGetValue(key, out var tex))
        {
            tex = Paint(e, lx, ly);
            _cache[key] = tex;
        }
        return tex;
    }

    private static bool UsesLook(Expression e) =>
        e is Expression.Neutral or Expression.Curious or Expression.Excited or Expression.Surprised;

    private Texture Paint(Expression e, int lx, int ly)
    {
        var t = new Texture(W, H);
        Array.Copy(_base.Pixels, t.Pixels, _base.Pixels.Length);
        int ey = -ly; // +lookY means "look up" -> smaller row index

        void Cheeks()
        {
            t.Set(1, 6, Palette.Blush); t.Set(2, 6, Palette.Blush);
            t.Set(9, 6, Palette.Blush); t.Set(10, 6, Palette.Blush);
        }
        void OpenEyes(int h, bool doubleShine)
        {
            int top = 3 + ey - (h - 3);
            foreach (int cx in new[] { 2, 8 })
            {
                for (int y = top; y < top + h; y++)
                    for (int x = cx + lx; x < cx + lx + 2; x++) t.Set(x, y, Palette.Eye);
                t.Set(cx + lx, top, Palette.EyeShine);
                if (doubleShine) t.Set(cx + lx + 1, top + h - 1, Palette.EyeShine);
            }
        }
        void SmallSmile()
        {
            t.Set(3, 6, Palette.Mouth); t.Set(8, 6, Palette.Mouth);
            for (int x = 4; x <= 7; x++) t.Set(x, 7, Palette.Mouth);
        }
        void OpenSmile(bool wide)
        {
            int x0 = wide ? 3 : 4, x1 = wide ? 8 : 7;
            for (int x = x0; x <= x1; x++) t.Set(x, 6, Palette.Mouth);
            for (int x = x0 + 1; x <= x1 - 1; x++) t.Set(x, 7, Palette.MouthInside);
            if (wide) { t.Set(5, 8, Palette.Mouth); t.Set(6, 8, Palette.Mouth); }
        }
        void ClosedArcEyes(bool happy)
        {
            // happy: "^ ^"   asleep/blink: flat lines lower on the face
            foreach (int cx in new[] { 1, 7 })
            {
                if (happy)
                {
                    t.Set(cx, 4, Palette.Eye); t.Set(cx + 1, 3, Palette.Eye);
                    t.Set(cx + 2, 3, Palette.Eye); t.Set(cx + 3, 4, Palette.Eye);
                }
                else
                {
                    for (int x = cx; x < cx + 4; x++) t.Set(x, 5, Palette.Eye);
                    t.Set(cx == 1 ? cx : cx + 3, 4, Palette.Eye); // outer corner lifts: sleepy lashes
                }
            }
        }

        switch (e)
        {
            case Expression.Neutral:
                OpenEyes(3, false); Cheeks(); SmallSmile(); break;
            case Expression.Curious:
                OpenEyes(3, false); Cheeks();
                t.Set(5 + Math.Max(0, lx), 7, Palette.Mouth); t.Set(6 + Math.Max(0, lx), 7, Palette.Mouth);
                break;
            case Expression.Happy:
                ClosedArcEyes(true); Cheeks(); OpenSmile(false); break;
            case Expression.Excited:
                OpenEyes(3, true); Cheeks(); OpenSmile(true); break;
            case Expression.Surprised:
                OpenEyes(4, false);
                t.Set(5, 7, Palette.Mouth); t.Set(6, 7, Palette.Mouth);
                t.Set(5, 8, Palette.Mouth); t.Set(6, 8, Palette.Mouth);
                break;
            case Expression.Sleepy:
                foreach (int cx in new[] { 2, 8 })
                {
                    t.Set(cx, 4, Palette.BodyShadow); t.Set(cx + 1, 4, Palette.BodyShadow);
                    t.Set(cx, 5, Palette.Eye); t.Set(cx + 1, 5, Palette.Eye);
                }
                Cheeks(); t.Set(5, 7, Palette.Mouth); t.Set(6, 7, Palette.Mouth);
                break;
            case Expression.Asleep:
                ClosedArcEyes(false); Cheeks();
                t.Set(5, 7, Palette.Mouth); t.Set(6, 7, Palette.Mouth);
                break;
            case Expression.Blink:
                foreach (int cx in new[] { 2, 8 }) { t.Set(cx, 5, Palette.Eye); t.Set(cx + 1, 5, Palette.Eye); }
                Cheeks(); SmallSmile();
                break;
            case Expression.Love:
                foreach (int cx in new[] { 1, 8 })
                {
                    t.Set(cx, 3, Palette.Heart); t.Set(cx + 2, 3, Palette.Heart);
                    t.Set(cx, 4, Palette.Heart); t.Set(cx + 1, 4, Palette.Heart); t.Set(cx + 2, 4, Palette.Heart);
                    t.Set(cx + 1, 5, Palette.Heart);
                }
                Cheeks(); OpenSmile(false);
                break;
        }
        return t;
    }
}
