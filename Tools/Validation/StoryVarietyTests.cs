using System;
using System.Collections.Generic;

// No Unity/player/account state is required to exercise the production selector.
public sealed class FightInfo { }

public static class StoryVarietyTests
{
    public static int Main()
    {
        try
        {
            Require(PocketStrikerStoryVariety.Themes.Length == 18, "Expected 18 subjects.");
            var seeds = new HashSet<string>(); int previous = -1;
            for (int cycle = 0; cycle < 12; cycle++)
            {
                var subjects = new HashSet<int>();
                for (int i = 0; i < 18; i++)
                {
                    var fight = new FightInfo(); var variant = PocketStrikerStoryVariety.ForFight(fight);
                    Require(variant.ThemeIndex != previous && subjects.Add(variant.ThemeIndex), "Genre repeated before exhaustion or across a bag boundary.");
                    Require(seeds.Add(variant.Seed), "Separate attempts reused a seed."); previous = variant.ThemeIndex;
                    Require(ReferenceEquals(variant, PocketStrikerStoryVariety.ForFight(fight)), "Ordinary read selected another story.");
                    string text = PocketStrikerStoryVariety.BuildTextPrompt(variant, "Chinese");
                    string image = PocketStrikerStoryVariety.BuildImagePrompt(variant, "A character takes the story's key action.");
                    foreach (string prompt in new[] { text, image })
                        Require(prompt.Contains(variant.Theme) && prompt.Contains(PocketStrikerStoryVariety.CartoonStyle), "Provider prompt omitted genre or art direction.");
                    Require(text.Contains(variant.Seed) && text.Contains(variant.Twist) && text.Contains(variant.Tone), "Narrative variation was not sent to provider.");
                }
                Require(subjects.Count == 18, "Not every genre was drawn once.");
            }
            var sameFight = new FightInfo(); var old = PocketStrikerStoryVariety.ForFight(sameFight);
            PocketStrikerStoryVariety.BeginBattleAttempt(sameFight); var next = PocketStrikerStoryVariety.ForFight(sameFight);
            Require(next.Seed != old.Seed && next.ThemeIndex != old.ThemeIndex, "Retry retained old subject/provider cache identity.");
            foreach (string seed in new[] { "acceptance", "故事验证", "stable-request-1" })
            {
                var first = PocketStrikerStoryVariety.ForSeed(seed); var second = PocketStrikerStoryVariety.ForSeed(seed);
                Require(first.Seed == second.Seed && first.Theme == second.Theme && first.Twist == second.Twist && first.Tone == second.Tone,
                    "Explicit cache replay seed produced a different selection.");
            }
            foreach (string token in new[] { "2D", "thick clean outlines", "flat cel shading", "no photorealism", "oil-painting texture", "no text" })
                Require(PocketStrikerStoryVariety.CartoonStyle.Contains(token), "Cartoon art direction missing: " + token);
            Console.WriteLine("Story variety: PASS (216 draws, 12 complete genre bags, stable attempts/seeds, same-fight retry, actual text/image prompts).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
