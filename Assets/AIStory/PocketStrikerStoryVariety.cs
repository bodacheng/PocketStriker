using System;
using System.Runtime.CompilerServices;

/// <summary>Stable selection seed per battle attempt; story content lives in remote resources.</summary>
public static class PocketStrikerStoryVariety
{
    // The existing queue worker and portrait presentation use this transport ratio.
    public const string ImageAspectRatio = "9:16";
    static readonly ConditionalWeakTable<FightInfo, Variant> Attempts = new ConditionalWeakTable<FightInfo, Variant>();
    static Variant preview;

    public sealed class Variant
    {
        public readonly string Seed;
        internal Variant(string seed) { Seed = seed; }
    }

    // Only the real Preparing entry starts a new attempt. Reads share its request and selection.
    public static void BeginBattleAttempt(FightInfo fight)
    {
        if (fight == null) { preview = null; return; }
        Attempts.Remove(fight);
    }

    public static Variant ForFight(FightInfo fight)
    {
        if (fight == null) return preview ??= Next();
        return Attempts.GetValue(fight, _ => Next());
    }

    public static Variant ForSeed(string seed)
    {
        if (string.IsNullOrWhiteSpace(seed)) throw new ArgumentException("Missing story variation seed.");
        return new Variant(seed);
    }

    static Variant Next() => new Variant(Guid.NewGuid().ToString("N"));
}
