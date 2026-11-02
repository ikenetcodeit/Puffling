using Puffling.Core.Animation;
using Puffling.Core.Character;
using Puffling.Core.Rendering;
using Puffling.Preview;

// Usage: dotnet run --project tools/Puffling.Preview -- <outDir> [mode]
string outDir = args.Length > 0 ? args[0] : "preview";
string mode = args.Length > 1 ? args[1] : "expressions";
Directory.CreateDirectory(outDir);

var renderer = new CharacterRenderer();
if (mode == "expressions")
{
    foreach (int size in new[] { 32, 48, 96 })
        foreach (Expression e in Enum.GetValues<Expression>())
        {
            var pose = Pose.Rest;
            pose.Expression = e;
            pose.Particles = e switch
            {
                Expression.Asleep => ParticleKind.Zzz,
                Expression.Love => ParticleKind.Hearts,
                Expression.Excited => ParticleKind.Sparkles,
                _ => ParticleKind.None,
            };
            var fb = renderer.Render(pose, 0.7f, 0.4f, size);
            PngWriter.Write(Path.Combine(outDir, $"{size}_{e}.png"), fb.Width, fb.Height, fb.Color);
        }
}
else if (mode == "icon")
{
    // Source images for puffling.ico (one exact render per icon size; no blurry resampling).
    foreach (int size in new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 })
    {
        var pose = Pose.Rest;
        pose.Expression = Expression.Happy;
        var fb = renderer.Render(pose, 0.6f, 0f, size, particles: false);
        PngWriter.Write(Path.Combine(outDir, $"icon_{size}.png"), fb.Width, fb.Height, fb.Color);
    }
}
else if (mode == "travel-facing")
{
    foreach (int size in new[] { 32, 48, 96 })
        foreach (int direction in new[] { -1, 0, 1 })
        {
            var pose = direction == 0 ? Pose.Rest : Clips.Travel.Evaluate(0f,
                new ClipContext { Velocity = new System.Numerics.Vector2(direction * 42f, 0) });
            var fb = renderer.Render(pose, 0.6f, 0f, size, particles: false);
            string label = direction < 0 ? "left" : direction > 0 ? "right" : "rest";
            PngWriter.Write(Path.Combine(outDir, $"{size}_{label}.png"), fb.Width, fb.Height, fb.Color);
        }
}
else if (mode == "variants")
{
    foreach (int size in new[] { 32, 48, 96 })
        foreach (CompanionVariant variant in Enum.GetValues<CompanionVariant>())
        {
            var pose = Pose.Rest;
            pose.Expression = Expression.Happy;
            var styled = new CharacterRenderer(variant);
            var fb = styled.Render(pose, 0.6f, 0f, size, particles: false);
            PngWriter.Write(Path.Combine(outDir, $"{size}_{variant}.png"), fb.Width, fb.Height, fb.Color);
        }
}
else
{
    SimulationPreview.Run(outDir, mode);
}
Console.WriteLine($"Wrote previews to {Path.GetFullPath(outDir)}");
