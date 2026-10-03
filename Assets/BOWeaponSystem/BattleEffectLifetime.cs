using System.Collections.Generic;

/// <summary>Invalidates rented battle effects and their pending spawns at round boundaries.</summary>
public static class BattleEffectLifetime
{
    static readonly HashSet<Decomposition> Active = new HashSet<Decomposition>();
    public static int Generation { get; private set; }
    public static bool Suspended { get; private set; }
    public static int ActiveCount => Active.Count;
    public static bool IsCurrent(int generation) => generation == Generation && !Suspended;

    internal static void Register(Decomposition effect) => Active.Add(effect);
    internal static void Unregister(Decomposition effect) => Active.Remove(effect);

    public static void InvalidateAll(bool suspend = false)
    {
        Generation++;
        Suspended = suspend;
        // Closing markers unregisters their owners and may occur inside a death
        // callback while the processor is applying this frame's damage.
        var effects = new List<Decomposition>(Active);
        foreach (var effect in effects)
            if (effect != null) effect.InvalidateBattleEffect();
    }

    public static void Resume()
    {
        Generation++;
        Suspended = false;
    }
}
