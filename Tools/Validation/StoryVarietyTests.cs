using System;
using System.Collections.Generic;

// Compile the production attempt selector directly: no Unity/player/account state.
public sealed class FightInfo { }

public static class StoryVarietyTests
{
    public static int Main()
    {
        try
        {
            var seeds = new HashSet<string>(StringComparer.Ordinal);
            for (int attempt = 0; attempt < 256; attempt++)
            {
                var fight = new FightInfo();
                var first = PocketStrikerStoryVariety.ForFight(fight);
                Require(!string.IsNullOrWhiteSpace(first.Seed) && seeds.Add(first.Seed), "Different battle attempts reused a seed.");
                Require(ReferenceEquals(first, PocketStrikerStoryVariety.ForFight(fight)), "An ordinary read started a new attempt.");
                PocketStrikerStoryVariety.BeginBattleAttempt(fight);
                var retry = PocketStrikerStoryVariety.ForFight(fight);
                Require(!ReferenceEquals(first, retry) && seeds.Add(retry.Seed), "A real retry retained the previous seed.");
                Require(ReferenceEquals(retry, PocketStrikerStoryVariety.ForFight(fight)), "Retry seed was not stable.");
            }
            var preview = PocketStrikerStoryVariety.ForFight(null);
            Require(ReferenceEquals(preview, PocketStrikerStoryVariety.ForFight(null)), "Preview reads changed the attempt.");
            var active = new FightInfo();
            var battle = PocketStrikerStoryVariety.ForFight(active);
            PocketStrikerStoryVariety.BeginBattleAttempt(null);
            Require(preview.Seed != PocketStrikerStoryVariety.ForFight(null).Seed, "Preview retry retained its seed.");
            Require(ReferenceEquals(battle, PocketStrikerStoryVariety.ForFight(active)), "Preview retry changed an active battle.");
            foreach (string seed in new[] { "acceptance", "故事验证", "stable-request-1" })
            {
                Require(PocketStrikerStoryVariety.ForSeed(seed).Seed == seed, "Explicit seed was modified.");
                Require(PocketStrikerStoryVariety.ForSeed(seed).Seed == PocketStrikerStoryVariety.ForSeed(seed).Seed, "Explicit seed replay changed.");
            }
            foreach (string invalid in new[] { null, "", "  " })
            {
                bool rejected = false;
                try { PocketStrikerStoryVariety.ForSeed(invalid); }
                catch (ArgumentException) { rejected = true; }
                Require(rejected, "A missing explicit seed was accepted.");
            }
            Require(PocketStrikerStoryVariety.ImageAspectRatio == "9:16", "PocketStriker portrait transport ratio changed.");
            Console.WriteLine("Story attempt seeds: PASS (512 unique battle/retry seeds, stable reads, independent preview, explicit replay, invalid input).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
