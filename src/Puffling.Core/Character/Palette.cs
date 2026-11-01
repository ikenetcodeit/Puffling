namespace Puffling.Core.Character;

/// <summary>Original colour palette for "Puffling", a mint cloud-jelly with a sprout on its head.</summary>
public static class Palette
{
    public const uint Body = 0xFF9BE3C7;
    public const uint BodyLight = 0xFFB8EFD9;
    public const uint BodyShadow = 0xFF7ACBAE;
    public const uint Cloud = 0xFFCDF6E6;
    public const uint CloudShadow = 0xFFAEE9D2;

    public const uint Eye = 0xFF2B2540;
    public const uint EyeShine = 0xFFFFFFFF;
    public const uint Blush = 0xFFF59AB0;
    public const uint Mouth = 0xFF5A2E3E;
    public const uint MouthInside = 0xFFD45D7A;
    public const uint Heart = 0xFFFF5C8A;

    public const uint Stem = 0xFF5DAA4E;
    public const uint Leaf = 0xFF86D86A;
    public const uint LeafDark = 0xFF62B54E;

    public const uint Tentacle0 = 0xFF86D6BA;
    public const uint Tentacle1 = 0xFFA9E6CF;
    public const uint Tentacle2 = 0xFFFFB7A1;
    public const uint Tentacle2Dark = 0xFFF29C86;

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
