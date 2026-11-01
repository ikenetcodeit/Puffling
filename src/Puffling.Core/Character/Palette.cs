namespace Puffling.Core.Character;

/// <summary>Colours for the procedural Happy Ghast fan skin.</summary>
public static class Palette
{
    public const uint Body = 0xFFF6F0DC;
    public const uint BodyLight = 0xFFFFF8E3;
    public const uint BodyShadow = 0xFFDAD4C6;
    public const uint Harness = 0xFFA5542B;
    public const uint HarnessDark = 0xFF613B27;
    public const uint HarnessBlue = 0xFF345C99;
    public const uint GoggleGlass = 0xFF80CEE4;
    public const uint GoggleLight = 0xFFB9F2F6;

    public const uint Eye = 0xFF41352F;
    public const uint EyeShine = 0xFFFFFFFF;
    public const uint Blush = 0xFFE8D7B7;
    public const uint Mouth = 0xFF4A392D;
    public const uint MouthInside = 0xFF805D49;
    public const uint Heart = 0xFFFF5C8A;

    public const uint Tentacle0 = 0xFFF7F1DD;
    public const uint Tentacle1 = 0xFFEFE9D7;
    public const uint Tentacle2 = 0xFFE5DECF;
    public const uint Tentacle2Dark = 0xFFD2CBBD;

    // Particles
    public const uint ZzzColor = 0xFFE9F2FF;
    public const uint ZzzOutline = 0xFF6F7FA8;
    public const uint NoteColor = 0xFF6D5BD0;
    public const uint SparkleColor = 0xFFFFE27A;

    /// <summary>Deterministic per-texel brightness jitter: gives the Minecraft-like "noisy" texture
    /// look while keeping textures identical between runs.</summary>
    public static uint Jitter(uint argb, int x, int y, int seed, int strength = 10)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        int d = (int)(h % (uint)(strength * 2 + 1)) - strength;
        int r = Math.Clamp((int)((argb >> 16) & 0xFF) + d, 0, 255);
        int g = Math.Clamp((int)((argb >> 8) & 0xFF) + d, 0, 255);
        int b = Math.Clamp((int)(argb & 0xFF) + d, 0, 255);
        return (argb & 0xFF000000u) | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
    }
}
