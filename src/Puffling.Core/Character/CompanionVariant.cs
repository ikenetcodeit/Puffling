namespace Puffling.Core.Character;

/// <summary>Stable visual and behavioral identity for each roster slot.</summary>
public enum CompanionVariant { Red, Blue, Green }

public static class CompanionVariants
{
    public static CompanionVariant ForSlot(int slot) => slot switch
    {
        0 => CompanionVariant.Red,
        1 => CompanionVariant.Blue,
        2 => CompanionVariant.Green,
        _ => throw new ArgumentOutOfRangeException(nameof(slot)),
    };

    public static float IdleDuration(this CompanionVariant variant) => variant switch
    {
        CompanionVariant.Red => 1.12f,
        CompanionVariant.Blue => 0.84f,
        CompanionVariant.Green => 0.78f,
        _ => 1f,
    };

    public static float WanderSpeed(this CompanionVariant variant) => variant switch
    {
        CompanionVariant.Blue => 0.90f,
        CompanionVariant.Green => 1.22f,
        _ => 1.04f,
    };

    public static float Playfulness(this CompanionVariant variant) => variant switch
    {
        CompanionVariant.Blue => 1.42f,
        CompanionVariant.Green => 1.12f,
        _ => 0.92f,
    };

    public static float Curiosity(this CompanionVariant variant) => variant switch
    {
        CompanionVariant.Red => 1.35f,
        CompanionVariant.Blue => 1.08f,
        _ => 0.76f,
    };

    public static float Boldness(this CompanionVariant variant) => variant switch
    {
        CompanionVariant.Green => 1.65f,
        CompanionVariant.Blue => 0.78f,
        _ => 1f,
    };
}
