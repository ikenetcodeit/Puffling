namespace Puffling.Core.Rendering;

/// <summary>
/// A small pixel-art texture. Pixels are straight (non-premultiplied) ARGB, 0xAARRGGBB.
/// Alpha is treated as a cut-out mask: 0 = hole, anything else = opaque.
/// Sampling is always nearest-neighbour to keep the blocky look crisp.
/// </summary>
public sealed class Texture
{
    public int Width { get; }
    public int Height { get; }
    public uint[] Pixels { get; }

    public Texture(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        Width = width;
        Height = height;
        Pixels = new uint[width * height];
    }

    public uint this[int x, int y]
    {
        get => Pixels[y * Width + x];
        set => Pixels[y * Width + x] = value;
    }

    public void Fill(uint argb) => Array.Fill(Pixels, argb);

    public void Set(int x, int y, uint argb)
    {
        if ((uint)x < (uint)Width && (uint)y < (uint)Height) Pixels[y * Width + x] = argb;
    }

    /// <summary>Nearest-neighbour sample. u,v in [0,1], v=0 is the top row.</summary>
    public uint Sample(float u, float v)
    {
        int x = (int)(u * Width);
        int y = (int)(v * Height);
        if (x < 0) x = 0; else if (x >= Width) x = Width - 1;
        if (y < 0) y = 0; else if (y >= Height) y = Height - 1;
        return Pixels[y * Width + x];
    }
}
