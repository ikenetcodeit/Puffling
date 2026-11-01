using Puffling.Core.Animation;
using Puffling.Core.Character;

namespace Puffling.Core.Rendering;

/// <summary>
/// 2D pixel-art particles (Zzz, hearts, notes, sparkles) drawn on top of the 3D render.
/// They live inside the same canvas (clamped), so they never enlarge the window either.
/// </summary>
public static class Particles
{
    private static readonly string[] Z = { "###", "..#", ".#.", "###" };
    private static readonly string[] HeartS = { "#.#", "###", ".#." };
    private static readonly string[] Note = { ".##", ".#.", "##.", "##." };
    private static readonly string[] Spark = { ".#.", "###", ".#." };

    public static void Draw(FrameBuffer fb, ParticleKind kind, float time)
    {
        int s = fb.Width;
        int k = Math.Max(1, (int)MathF.Round(s / 44f)); // particle "pixel" size

        for (int i = 0; i < 3; i++)
        {
            float t = Frac(time * 0.33f + i / 3f);
            switch (kind)
            {
                case ParticleKind.Zzz:
                {
                    int scale = t > 0.5f ? k + (s >= 64 ? 1 : 0) : k;
                    Blit(fb, Z, s * (0.70f + 0.16f * t), s * (0.34f - 0.30f * t), scale, Palette.ZzzColor, t < 0.92f);
                    break;
                }
                case ParticleKind.Hearts:
                {
                    float x = i % 2 == 0 ? s * (0.10f + 0.05f * MathF.Sin(t * 9f)) : s * (0.80f + 0.05f * MathF.Sin(t * 9f));
                    Blit(fb, HeartS, x, s * (0.40f - 0.36f * t), k, Palette.Heart, t < 0.9f);
                    break;
                }
                case ParticleKind.Notes:
                {
                    float x = i % 2 == 0 ? s * 0.06f : s * 0.84f;
                    Blit(fb, Note, x + MathF.Sin(t * 12f) * k, s * (0.45f - 0.40f * t), k, Palette.NoteColor, t < 0.9f);
                    break;
                }
                case ParticleKind.Sparkles:
                {
                    // Pseudo-random but deterministic spots that twinkle.
                    int slot = (int)(time * 0.33f + i / 3f);
                    float rx = Frac(MathF.Sin(slot * 12.9898f + i * 78.233f) * 43758.55f);
                    float x = rx < 0.5f ? s * (0.04f + rx * 0.2f) : s * (0.76f + (rx - 0.5f) * 0.3f);
                    float y = s * (0.08f + Frac(rx * 7.3f) * 0.35f);
                    Blit(fb, Spark, x, y, k, Palette.SparkleColor, t is > 0.1f and < 0.6f);
                    break;
                }
            }
        }
    }

    private static void Blit(FrameBuffer fb, string[] sprite, float fx, float fy, int k, uint color, bool visible)
    {
        if (!visible) return;
        int h = sprite.Length, w = sprite[0].Length;
        int x0 = Math.Clamp((int)fx, 0, fb.Width - w * k);
        int y0 = Math.Clamp((int)fy, 0, fb.Height - h * k);
        for (int sy = 0; sy < h; sy++)
            for (int sx = 0; sx < w; sx++)
            {
                if (sprite[sy][sx] != '#') continue;
                for (int py = 0; py < k; py++)
                    for (int px = 0; px < k; px++)
                    {
                        int x = x0 + sx * k + px, y = y0 + sy * k + py;
                        if ((uint)x < (uint)fb.Width && (uint)y < (uint)fb.Height)
                            fb.Color[y * fb.Width + x] = color;
                    }
            }
    }

    private static float Frac(float v) => v - MathF.Floor(v);
}
