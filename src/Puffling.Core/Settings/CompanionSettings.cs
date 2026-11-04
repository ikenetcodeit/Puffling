using System.Text.Json;
using System.Text.Json.Serialization;

namespace Puffling.Core.Settings;

public readonly record struct SavedCompanionPosition(float X, float Y);

/// <summary>
/// User-facing settings. Plain mutable POCO for easy JSON round-tripping; every value is
/// range-checked by <see cref="Normalize"/> after loading so a hand-edited or corrupted file can
/// never put the app in an invalid state.
/// </summary>
public sealed class CompanionSettings
{
    public const int MinScale = 32, MaxScale = 96, DefaultScale = 48;
    public const int MaxCompanions = 3;

    /// <summary>Character size in logical pixels (DPI-scaled at runtime).</summary>
    public int Scale { get; set; } = DefaultScale;
    public float MovementSpeed { get; set; } = 1f;      // 0.25 .. 2
    public float AnimationIntensity { get; set; } = 1f; // 0.3 .. 1.5
    public float ActivityFrequency { get; set; } = 1f;  // 0.2 .. 2
    public bool AutonomousMovement { get; set; } = true;
    public bool FollowCursor { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool Paused { get; set; }
    /// <summary>Stay asleep until turned off (or until clicked).</summary>
    public bool SleepMode { get; set; }
    public bool AutoSleep { get; set; } = true;
    public int FpsLimit { get; set; } = 30;             // 10 .. 60
    public bool PerformanceMode { get; set; }
    public bool HideInFullscreen { get; set; } = true;
    public bool FreezePosition { get; set; }
    public bool MicrosoftReminders { get; set; }
    public string MicrosoftClientId { get; set; } = "";
    public string DailyReminderTime { get; set; } = "09:00";
    public int EventReminderMinutes { get; set; } = 15;

    /// <summary>Last centre position in physical px (validated against monitors at startup).</summary>
    public float? LastX { get; set; }
    public float? LastY { get; set; }
    public int CompanionCount { get; set; } = 1;
    public List<SavedCompanionPosition> AdditionalPositions { get; set; } = new();

    /// <summary>Remembered resting spot in physical px, set the first time this companion falls
    /// asleep and preferred thereafter (validated against monitors at startup, like LastX/LastY).
    /// Slot 0's spot; slots 1+ are <see cref="AdditionalFavoriteSpots"/>, index-aligned with
    /// <see cref="AdditionalPositions"/> (a missing/short entry just means "no favorite spot yet").</summary>
    public float? FavoriteX { get; set; }
    public float? FavoriteY { get; set; }
    public List<SavedCompanionPosition?> AdditionalFavoriteSpots { get; set; } = new();

    public int SettingsVersion { get; set; } = 1;

    public CompanionSettings Normalize()
    {
        Scale = Math.Clamp(Scale, MinScale, MaxScale);
        MovementSpeed = ClampF(MovementSpeed, 0.25f, 2f, 1f);
        AnimationIntensity = ClampF(AnimationIntensity, 0.3f, 1.5f, 1f);
        ActivityFrequency = ClampF(ActivityFrequency, 0.2f, 2f, 1f);
        FpsLimit = Math.Clamp(FpsLimit, 10, 60);
        MicrosoftClientId = Guid.TryParse(MicrosoftClientId, out var id) ? id.ToString() : "";
        if (!TimeOnly.TryParseExact(DailyReminderTime, "HH:mm", out _)) DailyReminderTime = "09:00";
        EventReminderMinutes = Math.Clamp(EventReminderMinutes, 1, 120);
        if (LastX is { } x && !float.IsFinite(x)) LastX = null;
        if (LastY is { } y && !float.IsFinite(y)) LastY = null;
        CompanionCount = Math.Clamp(CompanionCount, 1, MaxCompanions);
        AdditionalPositions ??= new();
        AdditionalPositions = AdditionalPositions
            .Where(p => float.IsFinite(p.X) && float.IsFinite(p.Y))
            .Take(MaxCompanions - 1).ToList();
        if (FavoriteX is { } fx && !float.IsFinite(fx)) FavoriteX = null;
        if (FavoriteY is { } fy && !float.IsFinite(fy)) FavoriteY = null;
        AdditionalFavoriteSpots ??= new();
        AdditionalFavoriteSpots = AdditionalFavoriteSpots
            .Select(p => p is { } v && float.IsFinite(v.X) && float.IsFinite(v.Y) ? p : null)
            .Take(MaxCompanions - 1).ToList();
        return this;
    }

    /// <summary>Effective frame rate after performance mode.</summary>
    public int EffectiveFps => PerformanceMode ? Math.Min(FpsLimit, 15) : FpsLimit;

    public CompanionSettings Clone()
    {
        var clone = (CompanionSettings)MemberwiseClone();
        clone.AdditionalPositions = new List<SavedCompanionPosition>(AdditionalPositions ?? new());
        clone.AdditionalFavoriteSpots = new List<SavedCompanionPosition?>(AdditionalFavoriteSpots ?? new());
        return clone;
    }

    private static float ClampF(float v, float lo, float hi, float fallback) =>
        float.IsFinite(v) ? Math.Clamp(v, lo, hi) : fallback;
}

/// <summary>Loads/saves settings as JSON. Writes are atomic (temp file + replace) so a crash or
/// power loss mid-write can't leave a truncated file.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public string FilePath { get; }

    public SettingsStore(string filePath) => FilePath = filePath;

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Puffling", "settings.json");

    /// <summary>Never throws: a missing or corrupt file yields defaults (corrupt file is kept as .bak).</summary>
    public CompanionSettings Load(Action<string>? log = null)
    {
        try
        {
            if (!File.Exists(FilePath)) return new CompanionSettings();
            var s = JsonSerializer.Deserialize<CompanionSettings>(File.ReadAllText(FilePath), Json);
            return (s ?? new CompanionSettings()).Normalize();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            log?.Invoke($"Settings unreadable, using defaults: {ex.Message}");
            try { File.Copy(FilePath, FilePath + ".bak", overwrite: true); } catch { /* best effort */ }
            return new CompanionSettings();
        }
    }

    public void Save(CompanionSettings settings)
    {
        string dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Json));
        File.Move(tmp, FilePath, overwrite: true);
    }
}
