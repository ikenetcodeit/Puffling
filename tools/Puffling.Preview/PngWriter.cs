using System.IO.Compression;
using System.Text;

namespace Puffling.Preview;

/// <summary>Minimal RGBA PNG encoder (no external packages).</summary>
public static class PngWriter
{
    public static void Write(string path, int w, int h, uint[] argbPremultiplied)
    {
        using var fs = File.Create(path);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13];
        BE(ihdr, 0, (uint)w); BE(ihdr, 4, (uint)h);
        ihdr[8] = 8; ihdr[9] = 6; // 8-bit RGBA
        Chunk(fs, "IHDR", ihdr);

        var raw = new MemoryStream();
        for (int y = 0; y < h; y++)
        {
            raw.WriteByte(0);
            for (int x = 0; x < w; x++)
            {
                uint c = argbPremultiplied[y * w + x];
                uint a = c >> 24;
                byte r = (byte)((c >> 16) & 0xFF), g = (byte)((c >> 8) & 0xFF), b = (byte)(c & 0xFF);
                if (a is > 0 and < 255) { r = (byte)(r * 255 / a); g = (byte)(g * 255 / a); b = (byte)(b * 255 / a); }
                raw.WriteByte(r); raw.WriteByte(g); raw.WriteByte(b); raw.WriteByte((byte)a);
            }
        }
        var z = new MemoryStream();
        using (var zs = new ZLibStream(z, CompressionLevel.Optimal, leaveOpen: true)) raw.WriteTo(zs);
        Chunk(fs, "IDAT", z.ToArray());
        Chunk(fs, "IEND", Array.Empty<byte>());
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; BE(len, 0, (uint)data.Length); s.Write(len);
        var td = new byte[4 + data.Length];
        Encoding.ASCII.GetBytes(type).CopyTo(td, 0); data.CopyTo(td, 4);
        s.Write(td);
        var crc = new byte[4]; BE(crc, 0, Crc(td)); s.Write(crc);
    }

    private static void BE(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

    private static uint Crc(byte[] d)
    {
        uint c = 0xFFFFFFFF;
        foreach (byte x in d)
        {
            c ^= x;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        }
        return c ^ 0xFFFFFFFF;
    }
}
