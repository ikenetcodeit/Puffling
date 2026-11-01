namespace Puffling.Core.Rendering;

/// <summary>
/// Square-ish render target. Colour is stored PREMULTIPLIED BGRA in a uint (0xAARRGGBB with RGB
/// already multiplied by A) because that is exactly what UpdateLayeredWindow expects.
/// </summary>
public sealed class FrameBuffer
{
    public int Width { get; }
    public int Height { get; }
    public uint[] Color { get; }
    public float[] Depth { get; }

    /// <summary>Number of projected vertices that fell outside the canvas during the last frame.
    /// Used by tests to prove the character always fits inside its icon-sized box.</summary>
    public int ClippedVertices { get; internal set; }

    public FrameBuffer(int width, int height)
    {
        Width = width;
        Height = height;
        Color = new uint[width * height];
        Depth = new float[width * height];
    }

    public void Clear()
    {
        Array.Clear(Color);
        Array.Fill(Depth, float.NegativeInfinity);
        ClippedVertices = 0;
    }

    /// <summary>Cheap content hash so the presenter can skip redundant window updates.</summary>
    public ulong ComputeHash()
    {
        ulong h = 14695981039346656037UL;
        foreach (uint c in Color)
        {
            h ^= c;
            h *= 1099511628211UL;
        }
        return h;
    }

    public static uint Premultiply(uint argb)
    {
        uint a = argb >> 24;
        if (a == 255) return argb;
        if (a == 0) return 0;
        uint r = ((argb >> 16) & 0xFF) * a / 255;
        uint g = ((argb >> 8) & 0xFF) * a / 255;
        uint b = (argb & 0xFF) * a / 255;
        return (a << 24) | (r << 16) | (g << 8) | b;
    }
}
