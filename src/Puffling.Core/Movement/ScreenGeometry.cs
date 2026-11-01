using System.Numerics;

namespace Puffling.Core.Movement;

/// <summary>Integer rectangle in physical (virtual-desktop) pixels. Right/Bottom are exclusive.</summary>
public readonly record struct RectI(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public Vector2 Center => new((Left + Right) / 2f, (Top + Bottom) / 2f);
    public bool Contains(Vector2 p) => p.X >= Left && p.X < Right && p.Y >= Top && p.Y < Bottom;

    /// <summary>Shrinks by <paramref name="m"/> on every side; collapses to the centre if too small.</summary>
    public RectI Deflate(int m)
    {
        int l = Left + m, t = Top + m, r = Right - m, b = Bottom - m;
        if (l >= r) { l = r = (Left + Right) / 2; r++; }
        if (t >= b) { t = b = (Top + Bottom) / 2; b++; }
        return new RectI(l, t, r, b);
    }

    public Vector2 Clamp(Vector2 p) => new(Math.Clamp(p.X, Left, Right - 1), Math.Clamp(p.Y, Top, Bottom - 1));

    public float DistanceSquared(Vector2 p) => Vector2.DistanceSquared(p, Clamp(p));
}

/// <param name="Scale">DPI scale factor (1.0 = 96 DPI, 1.5 = 144 DPI, ...).</param>
public sealed record MonitorInfo(string Id, RectI Bounds, RectI WorkArea, float Scale, bool IsPrimary);

/// <summary>
/// Immutable snapshot of the monitor layout. Replaced wholesale when Windows reports a display
/// change, so code holding an old snapshot never sees a half-updated layout.
/// Positions refer to the character's CENTRE in physical pixels.
/// </summary>
public sealed class ScreenGeometry
{
    // Stored as an array and iterated with foreach-over-array: these queries run every frame,
    // and enumerating an IReadOnlyList<T> boxes its enumerator (a small per-frame allocation).
    private readonly MonitorInfo[] _monitors;
    public IReadOnlyList<MonitorInfo> Monitors => _monitors;

    public ScreenGeometry(IReadOnlyList<MonitorInfo> monitors)
    {
        if (monitors.Count == 0) throw new ArgumentException("At least one monitor is required.", nameof(monitors));
        _monitors = monitors.ToArray();
    }

    public static ScreenGeometry Single(int w, int h, float scale = 1f, int taskbar = 48) =>
        new(new[] { new MonitorInfo("primary", new RectI(0, 0, w, h), new RectI(0, 0, w, h - taskbar), scale, true) });

    public MonitorInfo Primary => Array.Find(_monitors, m => m.IsPrimary) ?? _monitors[0];

    /// <summary>The monitor containing the point, or the nearest one.</summary>
    public MonitorInfo MonitorAt(Vector2 p)
    {
        foreach (var m in _monitors) if (m.Bounds.Contains(p)) return m;
        return _monitors.MinBy(m => m.Bounds.DistanceSquared(p))!;
    }

    /// <summary>True if the point lies on some monitor's work area (inset by <paramref name="margin"/>,
    /// with a little tolerance so a character hugging an edge does not count as lost).</summary>
    public bool IsComfortablyVisible(Vector2 p, int margin)
    {
        foreach (var m in _monitors)
            if (m.WorkArea.Deflate(Math.Max(0, margin - 4)).Contains(p)) return true;
        return false;
    }

    public bool IsOnAnyMonitor(Vector2 p)
    {
        foreach (var m in _monitors) if (m.Bounds.Contains(p)) return true;
        return false;
    }

    /// <summary>Closest point that keeps the whole character inside some monitor's work area.</summary>
    public Vector2 NearestSafePoint(Vector2 p, int margin)
    {
        Vector2 best = Primary.WorkArea.Deflate(margin).Center;
        float bestD = float.MaxValue;
        foreach (var m in _monitors)
        {
            var c = m.WorkArea.Deflate(margin).Clamp(p);
            float d = Vector2.DistanceSquared(c, p);
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    public Vector2 RandomPointNear(Vector2 from, float minDist, float maxDist, int margin, Random rng)
    {
        var area = MonitorAt(from).WorkArea.Deflate(margin);
        for (int i = 0; i < 12; i++)
        {
            float ang = (float)(rng.NextDouble() * Math.Tau);
            // Prefer mostly-horizontal hops: floating creatures drift sideways more than up/down.
            var dir = new Vector2(MathF.Cos(ang), MathF.Sin(ang) * 0.6f);
            float dist = minDist + (float)rng.NextDouble() * (maxDist - minDist);
            var p = from + Vector2.Normalize(dir) * dist;
            if (area.Contains(p)) return p;
        }
        return area.Clamp(from + new Vector2((float)(rng.NextDouble() - 0.5) * maxDist, 0));
    }

    /// <summary>Picks a far destination; may be on another monitor if the straight path stays on-screen.</summary>
    public Vector2 RandomFarPoint(Vector2 from, float minDist, int margin, Random rng)
    {
        for (int i = 0; i < 16; i++)
        {
            var m = Monitors[rng.Next(Monitors.Count)];
            var a = m.WorkArea.Deflate(margin);
            var p = new Vector2(a.Left + (float)rng.NextDouble() * a.Width, a.Top + (float)rng.NextDouble() * a.Height);
            if (Vector2.Distance(p, from) >= minDist && PathIsVisible(from, p)) return p;
        }
        return RandomPointNear(from, minDist * 0.5f, minDist * 1.5f, margin, rng);
    }

    /// <summary>A path is visible if every sample lies on some monitor (no flying through the gap
    /// between monitors of different heights).</summary>
    public bool PathIsVisible(Vector2 a, Vector2 b)
    {
        int n = Math.Max(2, (int)(Vector2.Distance(a, b) / 40f));
        for (int i = 0; i <= n; i++)
            if (!IsOnAnyMonitor(Vector2.Lerp(a, b, i / (float)n))) return false;
        return true;
    }
}
