using System;
using System.Runtime.CompilerServices;

/// <summary>Project-owned story choices. Its RNG never changes combat randomness.</summary>
public static class PocketStrikerStoryVariety
{
    public const string ImageAspectRatio = "9:16";
    public const string PortraitComposition = "portrait 9:16 composition for a phone screen; make the characters and key action large and central, use the full vertical frame, and keep essential details above the lower caption area";
    public const string CartoonStyle = "playful 2D storybook cartoon, oversized expressive heads and eyes, compact bodies, soft rounded silhouettes, thick clean outlines, flat cel shading, vibrant candy colors with coral, turquoise, lemon yellow and lavender accents, simple backgrounds, readable full-body action, gentle lighting; no photorealism, realistic anatomy, oil-painting texture or cinematic lighting; no text, lettering, logos or HUD";
    public static readonly string[] Themes =
    {
        "Fairy tale: a novice sky-post carrier delivers a lost wish through a cloud village.",
        "Detective mystery: a curious clockwork-town sleuth discovers who borrowed the moon-shaped bakery sign.",
        "Seafaring adventure: a young navigator and a chatty crab find a treasure that can be shared with an island village.",
        "Woodland rescue: an inventive animal caretaker helps a timid forest creature find its way home.",
        "Kitchen comedy: a clumsy apprentice baker chases a runaway pudding before the town picnic.",
        "Science-fiction comedy: a friendly maintenance robot repairs a moon carnival with an unexpected invention.",
        "Prehistoric exploration: a small dinosaur gardener discovers a hidden valley of enormous singing flowers.",
        "Steampunk invention: a tinkerer tests a wind-powered flying workshop above a bustling brass city.",
        "Folklore: a lantern maker befriends a mischievous seasonal spirit and restores a village tradition.",
        "Musical adventure: a wandering musician helps a silent valley rediscover its song.",
        "Desert journey: a caravan mapmaker follows playful sand spirits to an oasis that needs help.",
        "Undersea discovery: a coral gardener and a tiny whale rescue a drifting reef.",
        "Miniature adventure: a beetle explorer builds a bridge across a puddle in a giant backyard.",
        "Artistic fantasy: a young painter returns stolen colors to a town where murals come alive.",
        "Time-travel mystery: a curious courier delivers a letter from tomorrow and untangles a harmless paradox.",
        "Festival friendship: a shy little monster finds a surprising role in a crowded lantern festival.",
        "Space exploration: a comet-orchard keeper and an alien visitor discover how to grow a new constellation.",
        "Puzzle fantasy: a traveling librarian solves a mirror-library riddle to free a lost story."
    };
    static readonly string[] Twists =
    {
        "An overlooked tiny object becomes the solution.",
        "The apparent obstacle turns out to be an unexpected helper.",
        "A funny mistake reveals the missing clue.",
        "Two characters exchange roles and discover a new skill.",
        "Listening to a quiet character changes the plan.",
        "A generous choice succeeds where a clever shortcut fails.",
        "An unusual local custom provides the answer.",
        "The reward is a new friendship rather than a prize."
    };
    static readonly string[] Tones = { "warm wonder", "mischievous comedy", "gentle mystery", "spirited exploration", "cozy friendship", "joyful discovery" };
    static readonly System.Random Random = new System.Random();
    static readonly ShuffleBag ThemeBag = new ShuffleBag(Themes.Length);
    static readonly ShuffleBag TwistBag = new ShuffleBag(Twists.Length);
    static readonly ShuffleBag ToneBag = new ShuffleBag(Tones.Length);
    static readonly ConditionalWeakTable<FightInfo, Variant> Attempts = new ConditionalWeakTable<FightInfo, Variant>();
    static Variant preview;

    public sealed class Variant
    {
        public readonly string Seed, Theme, Twist, Tone;
        public readonly int ThemeIndex;
        internal Variant(string seed, int theme, int twist, int tone)
        { Seed = seed; ThemeIndex = theme; Theme = Themes[theme]; Twist = Twists[twist]; Tone = Tones[tone]; }
    }

    // Only the real Preparing entry calls this. Ordinary reads keep one stable
    // selection/job, while a retry of the same FightInfo receives a fresh story.
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

    // Explicit seeds support reproducible provider/cache acceptance fixtures.
    public static Variant ForSeed(string seed)
    {
        if (string.IsNullOrWhiteSpace(seed)) throw new ArgumentException("Missing story variation seed.");
        uint hash = 2166136261;
        foreach (char value in seed) { hash ^= value; hash *= 16777619; }
        var random = new System.Random(unchecked((int)hash));
        return new Variant(seed, random.Next(Themes.Length), random.Next(Twists.Length), random.Next(Tones.Length));
    }

    static Variant Next() => new Variant(Guid.NewGuid().ToString("N"), ThemeBag.Draw(), TwistBag.Draw(), ToneBag.Draw());

    public static string BuildTextPrompt(Variant variant, string language)
    {
        return "Write one short standalone PocketStriker story in " + language + ". Selected subject: " + variant.Theme
            + " Plot variation: " + variant.Twist + " Narrative tone: " + variant.Tone
            + ". Tell a complete tiny story with a setup, a surprising action and a satisfying resolution. "
            + "Use protagonists and locations appropriate to this subject; characters may be people, creatures or robots. "
            + "The story need not feature a fight or repeat the gladiator/skeleton pair. No football. "
            + "Return only JSON with title (short), lines (2 or 3 brief narrative lines, total at most 200 characters), "
            + "visualPrompt (English description of this story's key action, characters and setting, no text). "
            + "The visualPrompt must follow this art direction: " + CartoonStyle + ". Variation seed: " + variant.Seed;
    }

    public static string BuildImagePrompt(Variant variant, string visualPrompt) =>
        "Art direction (required): " + CartoonStyle + ". Selected subject: " + variant.Theme
        + " Illustrate this story's key action: " + visualPrompt + ". Composition (required): " + PortraitComposition
        + ". Keep the required cartoon art direction throughout.";

    sealed class ShuffleBag
    {
        readonly int[] items;
        int remaining, previous = -1;
        public ShuffleBag(int count) { items = new int[count]; }
        public int Draw()
        {
            if (remaining == 0)
            {
                for (int i = 0; i < items.Length; i++) items[i] = i;
                for (int i = items.Length - 1; i > 0; i--)
                { int j = Random.Next(i + 1); int value = items[i]; items[i] = items[j]; items[j] = value; }
                // The last element is drawn first; never repeat across bag boundaries.
                if (items.Length > 1 && items[items.Length - 1] == previous)
                { int value = items[0]; items[0] = items[items.Length - 1]; items[items.Length - 1] = value; }
                remaining = items.Length;
            }
            return previous = items[--remaining];
        }
    }
}
