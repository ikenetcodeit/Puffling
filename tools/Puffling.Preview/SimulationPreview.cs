using System.Numerics;
using Puffling.Core.Animation;
using Puffling.Core;
using Puffling.Core.Behavior;
using Puffling.Core.Movement;
using Puffling.Core.Rendering;
using Puffling.Core.Settings;

namespace Puffling.Preview;

/// <summary>Renders each clip as a numbered PNG sequence (canvas + window offset applied inside a
/// slightly larger frame, so you can see the bob/hop motion the window would perform).</summary>
public static class SimulationPreview
{
    public static void Run(string outDir, string mode)
    {
        if (mode is "pair" or "trio") { RunGroup(outDir, mode); return; }
        var clips = new Dictionary<string, IClip>
        {
            ["idle"] = Clips.Idle, ["happy"] = Clips.Happy, ["excited"] = Clips.Excited, ["surprised"] = Clips.Surprised,
            ["curious"] = Clips.Curious, ["drowsy"] = Clips.Drowsy, ["sleeping"] = Clips.Sleeping, ["waking"] = Clips.Waking,
            ["dance"] = Clips.Dance, ["affection"] = Clips.Affection, ["travel"] = Clips.Travel, ["dragged"] = Clips.Dragged,
            ["settle"] = Clips.Settle,
        };
        const int size = 48, pad = 8, frame = size + pad * 2, fps = 20;
        var renderer = new CharacterRenderer();
        foreach (var (name, clip) in clips)
        {
            if (mode != "all" && mode != name) continue;
            var dir = Path.Combine(outDir, name);
            Directory.CreateDirectory(dir);
            var anim = new Animator(new Random(1));
            anim.Play(clip, 0f);
            float len = clip.Duration ?? 4f;
            int n = (int)(len * fps);
            for (int i = 0; i < n; i++)
            {
                var ctx = new ClipContext
                {
                    LookDir = Vector2.Normalize(new Vector2(MathF.Cos(i * 0.05f), -0.4f)),
                    Velocity = name is "travel" or "dragged" ? new Vector2(160 * MathF.Sin(i * 0.06f), 0) : Vector2.Zero,
                };
                anim.Update(1f / fps, ctx);
                var fb = renderer.Render(anim.Current, anim.TentaclePhase, anim.ParticleTime, size);
                var off = anim.WindowOffset;
                var outBuf = new uint[frame * frame];
                int ox = pad + (int)MathF.Round(off.X), oy = pad + (int)MathF.Round(off.Y);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        int tx = x + ox, ty = y + oy;
                        if ((uint)tx < frame && (uint)ty < frame && fb.Color[y * size + x] != 0)
                            outBuf[ty * frame + tx] = fb.Color[y * size + x];
                    }
                PngWriter.Write(Path.Combine(dir, $"{i:D3}.png"), frame, frame, outBuf);
            }
        }
    }

    private static void RunGroup(string outDir, string mode)
    {
        int count = mode == "pair" ? 2 : 3;
        const int width = 200, height = 130, fps = 12;
        var geometry = ScreenGeometry.Single(400, 320, taskbar: 0);
        var pets = new Companion[count];
        for (int i = 0; i < count; i++)
            pets[i] = new Companion(new CompanionSettings
            {
                LastX = 200 + (i - (count - 1) / 2f) * 105,
                LastY = 150,
            }, geometry, i + 1);
        var group = new GroupInteractionCoordinator();
        group.Tick(0, pets);
        group.Tick(8, pets);
        string dir = Path.Combine(outDir, mode);
        Directory.CreateDirectory(dir);
        int frames = (int)MathF.Ceiling((count == 2 ? 7.5f : 9.5f) * fps);
        for (int i = 0; i < frames; i++)
        {
            var pixels = new uint[width * height];
            foreach (var pet in pets)
            {
                pet.Tick(1f / fps, new FrameInputs(new Vector2(1000, 1000), 20));
                var render = pet.Render();
                int ox = render.Left - 100, oy = render.Top - 85;
                for (int y = 0; y < render.Size; y++)
                    for (int x = 0; x < render.Size; x++)
                    {
                        int px = ox + x, py = oy + y;
                        uint color = render.Frame.Color[y * render.Size + x];
                        if ((uint)px < width && (uint)py < height && color != 0)
                            pixels[py * width + px] = color;
                    }
            }
            PngWriter.Write(Path.Combine(dir, $"{i:D3}.png"), width, height, pixels);
        }
    }
}
