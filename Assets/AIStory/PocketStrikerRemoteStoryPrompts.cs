using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Project-only, immutable request snapshot of remote MCombat prompts and options.
/// The shared package and MCombat's own assets are not changed.
/// </summary>
public sealed class PocketStrikerRemoteStoryPrompts
{
    public const string ConfigAddress = "Config/AIServiceConfig";
    public const string PromptsAddress = "Config/PocketStrikerStoryPrompts";
    const int TextCharactersPerPage = 200;
    static readonly char[] SplitCandidates =
    {
        ' ', '\t', '\r', '\n', ',', '.', ';', ':', '，', '。', '！', '？', '、', '；', '：',
        '!', '?', '…', '—', '-', '‧', '･'
    };

    readonly PromptDocument prompts;
    readonly string[] themes;
    readonly FallbackSnapshot fallback;
    readonly string storyStyle;
    readonly string imageStyle;
    public int PageCount { get; }
    public string ImageAspectRatio { get; }

    PocketStrikerRemoteStoryPrompts(AIServiceConfig config, PromptDocument document, StoryFallbackConfigBase fallbackConfig)
    {
        if (config == null) throw new InvalidOperationException("AI story remote configuration is missing.");
        Validate(document);
        if (config.PageCount < 1 || config.PageCount > 6)
            throw new InvalidOperationException("AI story page count must be between 1 and 6.");
        prompts = document;
        PageCount = config.PageCount;
        ImageAspectRatio = config.ImageAspectRatio;
        if (string.IsNullOrWhiteSpace(ImageAspectRatio))
            throw new InvalidOperationException("AI story image aspect ratio is missing.");
        themes = CopyValues(config.StoryThemes);
        storyStyle = ResolveStyle(document.storyStyles, config.StoryStyle.ToString(),
            config.StoryStyle == StoryStyle.Custom, config.CustomStoryStylePrompt,
            document.customStoryStyleFallback, config.AdditionalStoryRequirements);
        imageStyle = ResolveStyle(document.imageStyles, config.ImageStyle.ToString(),
            config.ImageStyle == ImageStyle.Custom, config.CustomImageStylePrompt,
            document.customImageStyleFallback, config.AdditionalImageRequirements);
        if (themes.Length == 0) fallback = new FallbackSnapshot(fallbackConfig);
    }

    /// <summary>Copies all values before releasing Addressables; no live asset is retained.</summary>
    public static async UniTask<PocketStrikerRemoteStoryPrompts> Load(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var configHandle = Addressables.LoadAssetAsync<AIServiceConfig>(ConfigAddress);
        AsyncOperationHandle<TextAsset> promptsHandle = default;
        AsyncOperationHandle<StoryFallbackConfigBase> fallbackHandle = default;
        try
        {
            promptsHandle = Addressables.LoadAssetAsync<TextAsset>(PromptsAddress);
            var config = await configHandle.ToUniTask(cancellationToken: cancellationToken);
            var textAsset = await promptsHandle.ToUniTask(cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (config == null || textAsset == null)
                throw new InvalidOperationException("AI story remote resources are missing.");
            var selectedFallback = config.GetSelectedFallbackConfig();
            if (CopyValues(config.StoryThemes).Length == 0 && selectedFallback == null)
            {
                string address = config.GetSelectedFallbackAddress();
                if (string.IsNullOrWhiteSpace(address))
                    throw new InvalidOperationException("AI story fallback resource is missing.");
                fallbackHandle = Addressables.LoadAssetAsync<StoryFallbackConfigBase>(address);
                selectedFallback = await fallbackHandle.ToUniTask(cancellationToken: cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new PocketStrikerRemoteStoryPrompts(config, ParsePrompts(textAsset.text), selectedFallback);
        }
        finally
        {
            if (fallbackHandle.IsValid()) Addressables.Release(fallbackHandle);
            if (promptsHandle.IsValid()) Addressables.Release(promptsHandle);
            if (configHandle.IsValid()) Addressables.Release(configHandle);
        }
    }

    /// <summary>Uses the same validation and copying as the remote runtime loader.</summary>
    public static PocketStrikerRemoteStoryPrompts CreateForValidation(AIServiceConfig config, string json) =>
        new PocketStrikerRemoteStoryPrompts(config, ParsePrompts(json), config?.GetSelectedFallbackConfig());

    public string BuildTextPrompt(string seed, SystemLanguage language)
    {
        if (language == SystemLanguage.ChineseSimplified || language == SystemLanguage.ChineseTraditional)
            language = SystemLanguage.Chinese;
        if (language == SystemLanguage.Unknown) language = SystemLanguage.English;
        string localeName = language == SystemLanguage.Chinese || language == SystemLanguage.Japanese
            ? language.ToString() : "English";
        var locale = prompts.locales.First(item => item.language == localeName);
        string languageInstruction = language.ToString() == localeName
            ? locale.languageInstruction
            : prompts.otherLanguageInstruction.Replace("{language}", language.ToString());
        var random = new System.Random(StableSeed(seed));
        string theme = themes.Length > 0 ? Pick(themes, random) : BuildFallbackTheme(random);
        // Seed affects selection only; it never changes the MCombat narrative instructions.
        return locale.template.Replace("{pageCount}", PageCount.ToString())
            .Replace("{languageInstruction}", languageInstruction)
            .Replace("{storyStyle}", storyStyle).Replace("{imageStyle}", imageStyle)
            .Replace("{storyTheme}", theme);
    }

    string BuildFallbackTheme(System.Random random) => prompts.fallbackThemeTemplate
        .Replace("{setting}", Pick(fallback.Settings, random))
        .Replace("{worldDetail}", Pick(fallback.WorldDetails, random))
        .Replace("{hero}", Pick(fallback.Heroes, random))
        .Replace("{companion}", Pick(fallback.Companions, random))
        .Replace("{goal}", Pick(fallback.Goals, random))
        .Replace("{conflict}", Pick(fallback.Conflicts, random))
        .Replace("{resolution}", Pick(fallback.Resolutions, random))
        .Replace("{styleGuidance}", fallback.StyleGuidance.Replace("{pageCount}", PageCount.ToString()));

    /// <summary>Accepts the native MCombat schema, preserving the complete story and image metadata.</summary>
    public StoryInfo ParseStory(string json)
    {
        StoryData data;
        try { data = JsonUtility.FromJson<StoryData>(NormalizeJson(json)); }
        catch (Exception error) { throw new InvalidOperationException("Invalid AI story JSON.", error); }
        if (data?.scenes == null || data.scenes.Length != PageCount || data.characters == null ||
            data.locations == null || data.style == null)
            throw new InvalidOperationException("AI story JSON is incomplete.");
        var characters = new List<StoryInfo.CharacterProfile>();
        var characterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in data.characters)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id) || !characterIds.Add(item.id))
                throw new InvalidOperationException("AI story character IDs are invalid.");
            characters.Add(new StoryInfo.CharacterProfile
            {
                Id = item.id, DisplayName = item.name, Description = item.appearance,
                Outfit = item.outfit, Personality = item.personality, VisualTags = item.visualTags
            });
        }
        var locations = new List<StoryInfo.LocationProfile>();
        var locationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in data.locations)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id) || !locationIds.Add(item.id))
                throw new InvalidOperationException("AI story location IDs are invalid.");
            locations.Add(new StoryInfo.LocationProfile
            {
                Id = item.id, DisplayName = item.name, Description = item.description,
                Palette = item.palette, TimeOfDay = item.timeOfDay, Atmosphere = item.atmosphere
            });
        }
        var scenes = new List<StoryInfo.StoryScene>();
        for (int i = 0; i < data.scenes.Length; i++)
        {
            var item = data.scenes[i];
            if (item == null || item.index != i + 1 || string.IsNullOrWhiteSpace(item.description) ||
                string.IsNullOrWhiteSpace(item.visualPrompt) || string.IsNullOrWhiteSpace(item.locationId) ||
                !locationIds.Contains(item.locationId) || item.characters == null || item.dialogues == null ||
                item.characters.Any(id => string.IsNullOrWhiteSpace(id) || !characterIds.Contains(id)) ||
                (item.additionalLocations != null && item.additionalLocations.Any(id =>
                    string.IsNullOrWhiteSpace(id) || !locationIds.Contains(id))))
                throw new InvalidOperationException("AI story scene data is invalid.");
            var scene = new StoryInfo.StoryScene
            {
                Title = item.title, Description = item.description, Setting = item.setting,
                LocationId = item.locationId, Camera = item.camera, Lighting = item.lighting,
                Mood = item.mood, ImportantObjects = item.importantObjects,
                VisualPromptNotes = item.visualPrompt, NegativePromptNotes = item.negativePrompt
            };
            scene.CharactersInScene.AddRange(item.characters);
            if (item.additionalLocations != null) scene.AdditionalLocationIds.AddRange(item.additionalLocations);
            AddTextLines(scene.Lines, item.description);
            foreach (var dialogue in item.dialogues)
            {
                if (dialogue == null || string.IsNullOrWhiteSpace(dialogue.text)) continue;
                scene.Dialogues.Add(new StoryInfo.StoryDialogueLine { Speaker = dialogue.speaker, Text = dialogue.text });
                AddTextLines(scene.Lines, string.IsNullOrWhiteSpace(dialogue.speaker)
                    ? dialogue.text : $"{dialogue.speaker}: {dialogue.text.Trim()}");
            }
            scenes.Add(scene);
        }
        var story = ScriptableObject.CreateInstance<StoryInfo>();
        story.Characters = characters;
        story.Locations = locations;
        story.StyleGuide = new StoryInfo.StoryStyleGuide
        {
            // The remote configured cartoon style remains authoritative if the model drifts.
            ArtDirection = imageStyle, Palette = data.style.palette, CameraPreferences = data.style.camera,
            Lighting = data.style.lighting, Keywords = data.style.keywords, NegativeKeywords = data.style.negativeKeywords
        };
        story.StoryScenes = scenes;
        return story;
    }

    public string BuildImagePrompt(StoryInfo story, int sceneIndex)
    {
        if (story?.StoryScenes == null || sceneIndex < 0 || sceneIndex >= story.StoryScenes.Count)
            throw new ArgumentOutOfRangeException(nameof(sceneIndex));
        var scene = story.StoryScenes[sceneIndex];
        if (scene == null) throw new InvalidOperationException("AI story scene is missing.");
        var image = prompts.image;
        string overview = BuildOverview(scene);
        if (overview.Length > image.overviewMaxCharacters)
            overview = overview.Substring(0, image.overviewMaxCharacters) + "...";
        var builder = new StringBuilder(image.sceneFormat.Replace("{sceneNumber}", (sceneIndex + 1).ToString())
            .Replace("{sceneCount}", story.StoryScenes.Count.ToString()));
        if (!string.IsNullOrEmpty(scene.Title)) builder.Append(Format(image.titleFormat, scene.Title));
        if (!string.IsNullOrEmpty(overview)) builder.Append(overview).Append(' ');
        string characterPrompt = BuildCharacters(story.Characters, scene.CharactersInScene);
        if (!string.IsNullOrEmpty(characterPrompt)) builder.Append(characterPrompt).Append(' ');
        string locationPrompt = BuildLocations(story.Locations, scene);
        if (!string.IsNullOrEmpty(locationPrompt)) builder.Append(locationPrompt).Append(' ');
        if (!string.IsNullOrEmpty(scene.ImportantObjects)) builder.Append(Format(image.keyPropsFormat, scene.ImportantObjects));
        if (!string.IsNullOrEmpty(scene.VisualPromptNotes)) builder.Append(Format(image.visualPromptFormat, scene.VisualPromptNotes));
        string stylePrompt = BuildStyle(story.StyleGuide, scene);
        if (!string.IsNullOrEmpty(stylePrompt)) builder.Append(stylePrompt).Append(' ');
        string prompt = builder.ToString().Trim();
        var negatives = image.defaultNegativeTokens.Concat(new[] { scene.NegativePromptNotes, story.StyleGuide?.NegativeKeywords })
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase);
        string negativePrompt = string.Join(", ", negatives);
        if (!string.IsNullOrEmpty(negativePrompt)) prompt += Format(image.avoidFormat, negativePrompt);
        scene.BuiltPrompt = prompt;
        return prompt;
    }

    string BuildCharacters(List<StoryInfo.CharacterProfile> profiles, IEnumerable<string> ids)
    {
        if (profiles == null || ids == null) return "";
        var entries = new List<string>();
        foreach (string id in ids)
        {
            var profile = profiles.FirstOrDefault(item => item != null && (Same(item.Id, id) || Same(item.DisplayName, id)));
            if (profile == null) continue;
            var parts = new List<string>();
            Add(parts, profile.Description); Add(parts, profile.Outfit); Add(parts, profile.VisualTags);
            if (!string.IsNullOrWhiteSpace(profile.Personality)) parts.Add(Format(prompts.image.personalityFormat, profile.Personality));
            string name = string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.Id : profile.DisplayName;
            entries.Add(parts.Count == 0 ? name : prompts.image.characterFormat.Replace("{name}", name)
                .Replace("{value}", string.Join(", ", parts)));
        }
        return entries.Count == 0 ? "" : Format(prompts.image.charactersFormat, string.Join("; ", entries));
    }

    string BuildLocations(List<StoryInfo.LocationProfile> profiles, StoryInfo.StoryScene scene)
    {
        var descriptions = new List<string>();
        var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new[] { scene.LocationId }.Concat(scene.AdditionalLocationIds ?? new List<string>());
        foreach (string id in ids)
        {
            if (string.IsNullOrWhiteSpace(id) || !handled.Add(id)) continue;
            var profile = profiles?.FirstOrDefault(item => item != null && (Same(item.Id, id) || Same(item.DisplayName, id)));
            if (profile == null) continue;
            var parts = new List<string>();
            Add(parts, profile.DisplayName); Add(parts, profile.Description);
            if (!string.IsNullOrWhiteSpace(profile.Palette)) parts.Add(Format(prompts.image.locationPaletteFormat, profile.Palette));
            Add(parts, profile.TimeOfDay); Add(parts, profile.Atmosphere);
            descriptions.Add(string.Join(", ", parts));
        }
        Add(descriptions, scene.Setting);
        return descriptions.Count == 0 ? "" : Format(prompts.image.environmentFormat, string.Join(". ", descriptions));
    }

    string BuildStyle(StoryInfo.StoryStyleGuide style, StoryInfo.StoryScene scene)
    {
        var parts = new List<string> { imageStyle };
        Add(parts, style?.ArtDirection);
        if (!string.IsNullOrWhiteSpace(style?.Palette)) parts.Add(Format(prompts.image.stylePaletteFormat, style.Palette));
        if (!string.IsNullOrWhiteSpace(scene.Camera)) parts.Add(Format(prompts.image.shotFormat, scene.Camera));
        else if (!string.IsNullOrWhiteSpace(style?.CameraPreferences)) parts.Add(Format(prompts.image.cameraFormat, style.CameraPreferences));
        string lighting = string.IsNullOrWhiteSpace(scene.Lighting) ? style?.Lighting : scene.Lighting;
        if (!string.IsNullOrWhiteSpace(lighting)) parts.Add(Format(prompts.image.lightingFormat, lighting));
        if (!string.IsNullOrWhiteSpace(scene.Mood)) parts.Add(Format(prompts.image.moodFormat, scene.Mood));
        Add(parts, style?.Keywords);
        return string.Join(" ", parts);
    }

    static string BuildOverview(StoryInfo.StoryScene scene)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(scene.Description)) parts.Add(scene.Description);
        else if (scene.Lines?.Count > 0) parts.Add(scene.Lines[0]);
        if (scene.Dialogues?.Count > 0)
        {
            foreach (var line in scene.Dialogues)
                if (line != null && !string.IsNullOrWhiteSpace(line.Text))
                    parts.Add(string.IsNullOrWhiteSpace(line.Speaker) ? line.Text : $"{line.Speaker}: {line.Text}");
        }
        else if (scene.Lines?.Count > 1) parts.AddRange(scene.Lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)));
        return string.Join(" ", parts).Trim();
    }

    static void AddTextLines(List<string> lines, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        string normalized = text.Trim().Replace("\r\n", "\n").Replace('\r', '\n');
        int index = 0;
        while (index < normalized.Length)
        {
            while (index < normalized.Length && char.IsWhiteSpace(normalized[index])) index++;
            if (index >= normalized.Length) break;
            int end = Math.Min(index + TextCharactersPerPage, normalized.Length);
            if (end < normalized.Length)
                for (int i = end - 1; i >= index; i--)
                    if (Array.IndexOf(SplitCandidates, normalized[i]) >= 0) { end = i + 1; break; }
            string chunk = normalized.Substring(index, end - index).Trim();
            if (chunk.Length > 0) lines.Add(chunk);
            index = end;
        }
    }

    static string NormalizeJson(string value)
    {
        value = value?.Trim() ?? "";
        if (value.StartsWith("```", StringComparison.Ordinal))
        {
            if (!value.EndsWith("```", StringComparison.Ordinal) || value.Length <= 6)
                throw new InvalidOperationException("Invalid AI story JSON.");
            int start = value.StartsWith("```json", StringComparison.OrdinalIgnoreCase) ? 7 : 3;
            value = value.Substring(start, value.Length - start - 3).Trim();
        }
        if (!value.StartsWith("{", StringComparison.Ordinal) || !value.EndsWith("}", StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid AI story JSON.");
        return value;
    }

    static PromptDocument ParsePrompts(string json)
    {
        try { return JsonUtility.FromJson<PromptDocument>(NormalizeJson(json)); }
        catch (Exception error) { throw new InvalidOperationException("Invalid remote AI story prompts.", error); }
    }

    static void Validate(PromptDocument value)
    {
        if (value == null || value.schemaVersion != 1 || value.locales == null || value.locales.Length != 3 ||
            value.storyStyles == null || value.imageStyles == null || value.image == null)
            throw new InvalidOperationException("Unsupported remote AI story prompt schema.");
        foreach (string language in new[] { "Chinese", "Japanese", "English" })
        {
            var items = value.locales.Where(item => item != null && item.language == language).ToArray();
            if (items.Length != 1) throw new InvalidOperationException("Remote AI story locale is missing.");
            Require(items[0].languageInstruction);
            Require(items[0].template, "{storyTheme}", "{pageCount}", "{storyStyle}", "{imageStyle}", "{languageInstruction}");
        }
        Require(value.otherLanguageInstruction, "{language}");
        Require(value.fallbackThemeTemplate, "{setting}", "{worldDetail}", "{hero}", "{companion}", "{goal}", "{conflict}", "{resolution}", "{styleGuidance}");
        Require(value.customStoryStyleFallback); Require(value.customImageStyleFallback);
        if (value.requirementsSeparator == null) throw new InvalidOperationException("Remote AI story style separator is missing.");
        foreach (var style in value.storyStyles.Concat(value.imageStyles))
        {
            if (style == null) throw new InvalidOperationException("Remote AI story style is missing.");
            Require(style.style); Require(style.prompt);
        }
        var image = value.image;
        Require(image.sceneFormat, "{sceneNumber}", "{sceneCount}"); Require(image.titleFormat, "{value}");
        Require(image.charactersFormat, "{value}"); Require(image.characterFormat, "{name}", "{value}");
        Require(image.personalityFormat, "{value}"); Require(image.environmentFormat, "{value}");
        Require(image.locationPaletteFormat, "{value}"); Require(image.keyPropsFormat, "{value}");
        Require(image.visualPromptFormat, "{value}"); Require(image.stylePaletteFormat, "{value}");
        Require(image.shotFormat, "{value}"); Require(image.cameraFormat, "{value}");
        Require(image.lightingFormat, "{value}"); Require(image.moodFormat, "{value}"); Require(image.avoidFormat, "{value}");
        if (image.overviewMaxCharacters < 1 || image.overviewMaxCharacters > 10000 || image.defaultNegativeTokens == null ||
            image.defaultNegativeTokens.Length == 0 || image.defaultNegativeTokens.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Remote AI image prompt options are invalid.");
    }

    string ResolveStyle(StyleEntry[] options, string style, bool custom, string customPrompt, string fallbackPrompt, string requirements)
    {
        string result;
        if (custom) result = string.IsNullOrWhiteSpace(customPrompt) ? fallbackPrompt : customPrompt.Trim();
        else
        {
            var entries = options.Where(item => item.style == style).ToArray();
            if (entries.Length != 1) throw new InvalidOperationException("Configured remote AI story style is missing.");
            result = entries[0].prompt;
        }
        if (!string.IsNullOrWhiteSpace(requirements)) result += prompts.requirementsSeparator + requirements.Trim();
        return result;
    }

    static void Require(string text, params string[] placeholders)
    {
        if (string.IsNullOrWhiteSpace(text) || placeholders.Any(value => !text.Contains(value)))
            throw new InvalidOperationException("Remote AI story prompt field is missing or incomplete.");
    }
    static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    static string Format(string template, string value) => template.Replace("{value}", value);
    static void Add(List<string> values, string value) { if (!string.IsNullOrWhiteSpace(value)) values.Add(value); }
    static string[] CopyValues(string[] values) => values?.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray() ?? Array.Empty<string>();
    static string Pick(string[] values, System.Random random) => values[random.Next(values.Length)];
    static int StableSeed(string seed)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in seed ?? "") hash = (hash ^ c) * 16777619;
            return (int)hash;
        }
    }

    sealed class FallbackSnapshot
    {
        public readonly string[] Settings, WorldDetails, Heroes, Companions, Goals, Conflicts, Resolutions;
        public readonly string StyleGuidance;
        public FallbackSnapshot(StoryFallbackConfigBase config)
        {
            if (config == null) throw new InvalidOperationException("Remote AI story fallback configuration is missing.");
            Settings = CopyValues(config.Settings); WorldDetails = CopyValues(config.WorldDetails);
            Heroes = CopyValues(config.Heroes); Companions = CopyValues(config.Companions); Goals = CopyValues(config.Goals);
            Conflicts = CopyValues(config.Conflicts); Resolutions = CopyValues(config.Resolutions); StyleGuidance = config.StyleGuidance;
            if (new[] { Settings, WorldDetails, Heroes, Companions, Goals, Conflicts, Resolutions }.Any(items => items.Length == 0) ||
                string.IsNullOrWhiteSpace(StyleGuidance))
                throw new InvalidOperationException("Remote AI story fallback configuration is incomplete.");
        }
    }

    [Serializable] sealed class PromptDocument
    {
        public int schemaVersion;
        public Locale[] locales;
        public string otherLanguageInstruction, fallbackThemeTemplate;
        public StyleEntry[] storyStyles, imageStyles;
        public string customStoryStyleFallback, customImageStyleFallback, requirementsSeparator;
        public ImagePrompt image;
    }
    [Serializable] sealed class Locale { public string language, languageInstruction, template; }
    [Serializable] sealed class StyleEntry { public string style, prompt; }
    [Serializable] sealed class ImagePrompt
    {
        public string sceneFormat, titleFormat, charactersFormat, characterFormat, personalityFormat, environmentFormat;
        public string locationPaletteFormat, keyPropsFormat, visualPromptFormat, stylePaletteFormat, shotFormat;
        public string cameraFormat, lightingFormat, moodFormat, avoidFormat;
        public int overviewMaxCharacters;
        public string[] defaultNegativeTokens;
    }
    [Serializable] sealed class StoryData { public CharacterData[] characters; public LocationData[] locations; public StyleData style; public SceneData[] scenes; }
    [Serializable] sealed class CharacterData { public string id, name, appearance, outfit, personality, visualTags; }
    [Serializable] sealed class LocationData { public string id, name, description, palette, timeOfDay, atmosphere; }
    [Serializable] sealed class StyleData { public string artDirection, palette, camera, lighting, keywords, negativeKeywords; }
    [Serializable] sealed class SceneData
    {
        public int index;
        public string title, description, setting, locationId, mood, importantObjects, camera, lighting, visualPrompt, negativePrompt;
        public string[] characters, additionalLocations;
        public DialogueData[] dialogues;
    }
    [Serializable] sealed class DialogueData { public string speaker, text; }
}
