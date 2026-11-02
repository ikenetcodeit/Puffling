namespace Puffling.Core.Character;

/// <summary>Stable visual and behavioral identity for each roster slot.</summary>
public enum CompanionVariant { Classic, Teal, Lavender }

public static class CompanionVariants
{
    public static CompanionVariant ForSlot(int slot) => slot switch
    {
        0 => CompanionVariant.Classic,
        1 => CompanionVariant.Teal,
        2 => CompanionVariant.Lavender,
        _ => throw new ArgumentOutOfRangeException(nameof(slot)),
    };

    public static float IdleDuration(this CompanionVariant variant) => variant switch
    {
        CompanionVariant.Teal => 0.82f,
        CompanionVariant.Lavender => 1.20f,
        _ => 1f,
    };

    public static float WanderSpeed(this CompanionVariant variant) => variant switch
    {
        CompanionVariant.Teal => 1.16f,
        CompanionVariant.Lavender => 0.88f,
        _ => 1f,
    };

    public static float Playfulness(this CompanionVariant variant) => variant switch
    {
        CompanionVariant.Teal => 1.30f,
        CompanionVariant.Lavender => 0.78f,
        _ => 1f,
    };
}
