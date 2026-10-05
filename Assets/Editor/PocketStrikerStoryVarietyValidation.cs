using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlayFab.Json;
using UnityEditor;
using UnityEngine;

/// <summary>Production prompt/pipeline fixtures; no account, provider, or network calls.</summary>
public static class PocketStrikerStoryVarietyValidation
{
    [Serializable] public sealed class Report { public bool passed; public List<string> checks = new List<string>(); public List<string> failures = new List<string>(); }
    [Serializable] sealed class Parameters { public string prompt, model, imageModel, aspectRatio, cacheKey, storyId; public int timeoutMs, sampleCount, sceneIndex; }
    const string ConfigPath = "Assets/AIStory/AIServiceConfig.asset";
    const string TemplatesPath = "Assets/AIStory/PocketStrikerStoryPrompts.json";
    const string Page = "{\"characters\":[{\"id\":\"hero\",\"name\":\"邮差\",\"appearance\":\"short violet hair\",\"outfit\":\"turquoise coat\",\"personality\":\"curious and kind\",\"visualTags\":\"star-shaped satchel\"}],\"locations\":[{\"id\":\"village\",\"name\":\"云村\",\"description\":\"floating cloud cottages\",\"palette\":\"coral rooftops\",\"timeOfDay\":\"dawn\",\"atmosphere\":\"breezy\"}],\"style\":{\"artDirection\":\"storybook cartoon\",\"palette\":\"lemon and lavender\",\"camera\":\"readable full body\",\"lighting\":\"gentle morning light\",\"keywords\":\"rounded silhouettes\",\"negativeKeywords\":\"extra fingers\"},\"scenes\":[{\"index\":1,\"title\":\"小小发现\",\"description\":\"邮差在云间找到失落的愿望。\",\"setting\":\"village at dawn\",\"locationId\":\"village\",\"characters\":[\"hero\"],\"mood\":\"joyful\",\"importantObjects\":\"glowing wish\",\"camera\":\"eye-level full body\",\"lighting\":\"soft dawn light\",\"visualPrompt\":\"The courier catches a glowing wish above a cloud village\",\"negativePrompt\":\"blurred faces\",\"dialogues\":[{\"speaker\":\"邮差\",\"text\":\"我带你回家。\"}]}]}";

    public static void ValidateBatch() => Run().Forget();
    static async UniTaskVoid Run()
    {
        var report = new Report();
        int attempted = 0;
        var originalLanguage = AIStoryRuntimeContext.LanguageProvider;
        var config = AssetDatabase.LoadAssetAtPath<AIServiceConfig>(ConfigPath);
        string templates = File.Exists(TemplatesPath) ? File.ReadAllText(TemplatesPath) : "";
        async UniTask Check(string name, Func<UniTask> action)
        {
            attempted++;
            try { await action(); report.checks.Add(name); }
            catch (Exception error) { report.failures.Add(name + ": " + error); }
        }
        AIStoryRuntimeContext.LanguageProvider = () => SystemLanguage.Chinese;
        try
        {
            await Check("battle attempts keep a stable seed and real retry gets a new seed", () =>
            {
                var fight = ScriptableObject.CreateInstance<FightInfo>();
                try
                {
                    var first = PocketStrikerStoryVariety.ForFight(fight);
                    Require(ReferenceEquals(first, PocketStrikerStoryVariety.ForFight(fight)), "Reading an attempt changed its seed.");
                    PocketStrikerStoryVariety.BeginBattleAttempt(fight);
                    var retry = PocketStrikerStoryVariety.ForFight(fight);
                    Require(first.Seed != retry.Seed && ReferenceEquals(retry, PocketStrikerStoryVariety.ForFight(fight)), "Retry seed failed to change/stay stable.");
                }
                finally { UnityEngine.Object.DestroyImmediate(fight); }
                return UniTask.CompletedTask;
            });
            await Check("remote config retains MCombat narrative defaults and uses cartoon portrait artwork", () =>
            {
                Require(config != null && config.PageCount == 1 && config.StoryThemes.Length == 0, "MCombat theme/page defaults changed.");
                Require(config.StoryStyle == StoryStyle.FairyTale && string.IsNullOrEmpty(config.CustomStoryStylePrompt)
                    && string.IsNullOrEmpty(config.AdditionalStoryRequirements), "MCombat narrative rules changed.");
                Require(config.FallbackTone == StoryFallbackTone.FairyTale && config.FairyTaleFallbackConfig != null
                    && config.FairyTaleFallbackConfigAddress == "Config/FairyTaleFallbackConfig", "Active MCombat fallback is missing.");
                Require(config.ImageStyle == ImageStyle.Custom && config.GetImageStylePrompt().Contains("cartoon")
                    && config.ImageAspectRatio == PocketStrikerStoryVariety.ImageAspectRatio, "Cartoon portrait configuration is missing.");
                return UniTask.CompletedTask;
            });
            await Check("remote localized templates exactly match MCombat prompt text", () =>
            {
                var clone = UnityEngine.Object.Instantiate(config);
                var host = new GameObject("MCombat prompt parity fixture");
                try
                {
                    Set(clone, "storyThemes", new[] { "shared narrative theme fixture" });
                    var manager = host.AddComponent<AIServiceManager>();
                    Set(manager, "serviceConfig", clone);
                    var remote = PocketStrikerRemoteStoryPrompts.CreateForValidation(clone, templates);
                    var sharedBuilder = typeof(AIServiceManager).GetMethod("BuildStoryPrompt", BindingFlags.Instance | BindingFlags.NonPublic);
                    Require(sharedBuilder != null, "Shared MCombat prompt builder not found.");
                    foreach (var language in new[] { SystemLanguage.Chinese, SystemLanguage.Japanese, SystemLanguage.English,
                        SystemLanguage.French, SystemLanguage.ChineseSimplified, SystemLanguage.ChineseTraditional, SystemLanguage.Unknown })
                    {
                        AIStoryRuntimeContext.LanguageProvider = () => language;
                        string shared = (string)sharedBuilder.Invoke(manager, new object[] { null });
                        string actual = remote.BuildTextPrompt("parity-seed", language);
                        Require(actual.Replace("\r\n", "\n") == shared.Replace("\r\n", "\n"), "Template differs from MCombat for " + language);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(clone); AIStoryRuntimeContext.LanguageProvider = () => SystemLanguage.Chinese; }
                return UniTask.CompletedTask;
            });
            await Check("remote MCombat fallback selection is deterministic for one attempt and changes across attempts", () =>
            {
                var remote = PocketStrikerRemoteStoryPrompts.CreateForValidation(config, templates);
                string first = remote.BuildTextPrompt("stable-fallback", SystemLanguage.Chinese);
                Require(first == remote.BuildTextPrompt("stable-fallback", SystemLanguage.Chinese), "One seed selected different fallback content.");
                var prompts = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < 32; index++) prompts.Add(remote.BuildTextPrompt("fallback-" + index, SystemLanguage.Chinese));
                Require(prompts.Count > 1, "All attempts selected the same fallback content.");
                return UniTask.CompletedTask;
            });
            await Check("legacy envelopes retain registered endpoints and bounded cached budgets", () =>
            {
                var text = PocketStrikerLegacyStoryClient.BuildRequest("text", "selected story prompt");
                var image = PocketStrikerLegacyStoryClient.BuildRequest("image", "selected image prompt");
                var textInput = ParametersOf(text.FunctionParameter);
                var imageInput = ParametersOf(image.FunctionParameter);
                Require(text.FunctionName == "generateGeminiText" && image.FunctionName == "generateGeminiImages", "Registered function names changed.");
                Require(textInput.model == "gemini-2.5-flash-lite" && textInput.timeoutMs == 20000 && !string.IsNullOrEmpty(textInput.cacheKey), "Text budget/cache missing.");
                Require(imageInput.imageModel == "gemini-3.1-flash-image" && imageInput.sampleCount == 1 && imageInput.aspectRatio == "9:16"
                    && imageInput.timeoutMs == 60000 && imageInput.storyId == imageInput.cacheKey && imageInput.sceneIndex == 1, "Image budget/envelope changed.");
                var replay = ParametersOf(PocketStrikerLegacyStoryClient.BuildRequest("text", "selected story prompt").FunctionParameter);
                Require(replay.cacheKey == textInput.cacheKey && text.GeneratePlayStreamEvent == false && image.GeneratePlayStreamEvent == false, "Replay cache/event policy changed.");
                return UniTask.CompletedTask;
            });
            var payloads = new Dictionary<string, string>();
            foreach (bool queued in new[] { false, true })
            {
                await Check((queued ? "queued" : "legacy") + " payloads change with remote templates and config without code changes", async () =>
                {
                    string route = queued ? "queued" : "legacy";
                    for (int revision = 0; revision < 2; revision++)
                    {
                        string marker = "remote-revision-" + revision;
                        var clone = UnityEngine.Object.Instantiate(config);
                        StoryInfo story = null;
                        var texture = new Texture2D(4, 4);
                        try
                        {
                            Set(clone, "storyThemes", new[] { marker + " theme" });
                            Set(clone, "customImageStylePrompt", marker + " cartoon style");
                            var remote = PocketStrikerRemoteStoryPrompts.CreateForValidation(clone, AddRemoteMarker(templates, marker));
                            Func<string, string, CancellationToken, UniTask<PocketStrikerStoryJobClient.Result>> generate;
                            if (queued)
                            {
                                var client = new PocketStrikerStoryJobClient((request, token) =>
                                {
                                    Require(request.operation == "start", "Ready fixture unexpectedly polled.");
                                    payloads[route + revision + request.kind] = request.input.prompt;
                                    return UniTask.FromResult(new PocketStrikerStoryJobClient.Reply { protocol = PocketStrikerStoryJobClient.Protocol,
                                        id = request.kind + "-job", status = "ready", result = Ready(request.kind) });
                                });
                                generate = (kind, prompt, token) => client.Generate(kind, prompt, token);
                            }
                            else
                            {
                                var client = new PocketStrikerLegacyStoryClient((request, token) =>
                                {
                                    string kind = request.FunctionName == "generateGeminiText" ? "text" : "image";
                                    payloads[route + revision + kind] = ParametersOf(request.FunctionParameter).prompt;
                                    return UniTask.FromResult(Ready(kind));
                                });
                                generate = client.Generate;
                            }
                            story = await PocketStrikerQueuedStory.LoadForValidation(default, "same-attempt", generate, (url, token) =>
                            { Require(url == "https://story-fixture.invalid/scene.jpg", "Image URL changed before download."); return UniTask.FromResult(texture); },
                                token => UniTask.FromResult(remote));
                            Require(story.HasVisualScene() && story.StoryScenes.Count == 1 && story.StoryScenes[0].Lines.Count >= 2, "Complete MCombat story was lost.");
                            Require(story.Characters[0].Description == "short violet hair" && story.Locations[0].Description == "floating cloud cottages", "Story metadata was lost.");
                            Require(payloads[route + revision + "text"].Contains(marker + " theme") && payloads[route + revision + "text"].Contains(marker + " text-template"), "Remote narrative update did not reach provider payload.");
                            Require(payloads[route + revision + "image"].Contains(marker + " cartoon style") && payloads[route + revision + "image"].Contains(marker + " image-template"), "Remote visual update did not reach provider payload.");
                            foreach (string detail in new[] { "short violet hair", "turquoise coat", "star-shaped satchel", "floating cloud cottages", "coral rooftops", "dawn",
                                "lemon and lavender", "soft dawn light", "glowing wish", "blurred faces", "extra fingers" })
                                Require(payloads[route + revision + "image"].Contains(detail), "Generated scene detail missing from image prompt: " + detail);
                            PocketStrikerQueuedStory.Release(story);
                            Require(story == null && texture == null, "Owned story/image was retained after release.");
                        }
                        finally { PocketStrikerQueuedStory.Release(story); if (texture != null) UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(clone); }
                    }
                    Require(payloads[route + "0text"] != payloads[route + "1text"] && payloads[route + "0image"] != payloads[route + "1image"], "Remote update reused the old payload.");
                    var before = ParametersOf(PocketStrikerLegacyStoryClient.BuildRequest("text", payloads[route + "0text"]).FunctionParameter);
                    var after = ParametersOf(PocketStrikerLegacyStoryClient.BuildRequest("text", payloads[route + "1text"]).FunctionParameter);
                    Require(before.cacheKey != after.cacheKey, "Remote update reused old provider cache identity.");
                });
            }
            await Check("legacy and queued transports use identical remote narrative and visual prompts", () =>
            {
                for (int revision = 0; revision < 2; revision++)
                    foreach (string kind in new[] { "text", "image" })
                        Require(payloads["legacy" + revision + kind] == payloads["queued" + revision + kind], "Transport changed remote prompt content.");
                return UniTask.CompletedTask;
            });
            await Check("missing or invalid remote resources never start provider work", async () =>
            {
                foreach (string invalid in new[] { null, "{}", "not-json", "{\"locales\":[]}" })
                {
                    int generations = 0, downloads = 0; bool rejected = false;
                    try
                    {
                        await PocketStrikerQueuedStory.LoadForValidation(default, "invalid-remote", (kind, prompt, token) =>
                        { generations++; return UniTask.FromResult(Ready(kind)); }, (url, token) =>
                        { downloads++; return UniTask.FromResult<Texture2D>(null); }, token =>
                        {
                            if (invalid == null) return UniTask.FromException<PocketStrikerRemoteStoryPrompts>(new InvalidOperationException("Missing remote resource fixture."));
                            return UniTask.FromResult(PocketStrikerRemoteStoryPrompts.CreateForValidation(config, invalid));
                        });
                    }
                    catch (Exception) { rejected = true; }
                    Require(rejected && generations == 0 && downloads == 0, "Invalid remote config started provider work.");
                }
                bool missingConfig = false;
                try { PocketStrikerRemoteStoryPrompts.CreateForValidation(null, templates); } catch (Exception) { missingConfig = true; }
                Require(missingConfig, "Missing AIServiceConfig was accepted.");
            });
            await Check("malformed or incomplete MCombat story responses never request images or downloads", async () =>
            {
                var remote = PocketStrikerRemoteStoryPrompts.CreateForValidation(config, templates);
                foreach (string invalid in new[] { "not-json", "{}", "{\"title\":\"old format\",\"lines\":[\"one\",\"two\"],\"visualPrompt\":\"scene\"}",
                    Page.Replace("\"index\":1", "\"index\":2"), Page.Replace("\"locationId\":\"village\"", "\"locationId\":\"missing\""),
                    Page.Replace("\"characters\":[\"hero\"]", "\"characters\":[\"missing\"]"),
                    Page.Replace("\"visualPrompt\":\"The courier catches a glowing wish above a cloud village\"", "\"visualPrompt\":\"\"") })
                {
                    int texts = 0, images = 0, downloads = 0; bool rejected = false;
                    try
                    {
                        await PocketStrikerQueuedStory.LoadForValidation(default, "invalid-story", (kind, prompt, token) =>
                        { if (kind == "text") texts++; else images++; return UniTask.FromResult(new PocketStrikerStoryJobClient.Result { text = invalid }); },
                        (url, token) => { downloads++; return UniTask.FromResult<Texture2D>(null); }, token => UniTask.FromResult(remote));
                    }
                    catch (InvalidOperationException) { rejected = true; }
                    Require(rejected && texts == 1 && images == 0 && downloads == 0, "Incomplete story started image work: " + invalid);
                }
            });
            await Check("generated MCombat metadata survives JSON fences and cartoon style overrides conflicting generated art", () =>
            {
                var remote = PocketStrikerRemoteStoryPrompts.CreateForValidation(config, templates);
                var story = remote.ParseStory("```json\n" + Page.Replace("storybook cartoon", "photorealistic oil painting") + "\n```");
                try
                {
                    string image = remote.BuildImagePrompt(story, 0);
                    Require(story.Characters.Count == 1 && story.Locations.Count == 1 && story.StoryScenes[0].Dialogues[0].Speaker == "邮差", "Full scene format was not preserved.");
                    Require(image.Contains(config.GetImageStylePrompt()) && !image.Contains("photorealistic oil painting"), "Generated art direction overrode remote cartoon rules.");
                }
                finally { UnityEngine.Object.DestroyImmediate(story); }
                return UniTask.CompletedTask;
            });
            await Check("remote image prompt exactly matches MCombat assembly including style fallback and truncation", () =>
            {
                var host = new GameObject("MCombat image parity fixture");
                var remote = PocketStrikerRemoteStoryPrompts.CreateForValidation(config, templates);
                var story = remote.ParseStory(Page);
                try
                {
                    var manager = host.AddComponent<AIServiceManager>();
                    Set(manager, "serviceConfig", config);
                    Set(manager, "currentStoryCharacters", story.Characters);
                    Set(manager, "currentStoryLocations", story.Locations);
                    Set(manager, "currentStoryStyleGuide", story.StyleGuide);
                    var sharedBuilder = typeof(AIServiceManager).GetMethod("BuildImagePrompt", BindingFlags.Instance | BindingFlags.NonPublic);
                    Require(sharedBuilder != null, "Shared MCombat image builder not found.");
                    for (int fixture = 0; fixture < 2; fixture++)
                    {
                        var scene = story.StoryScenes[0];
                        if (fixture == 1)
                        {
                            scene.Camera = ""; scene.Lighting = "";
                            scene.Description = new string('x', 550);
                            scene.NegativePromptNotes = "watermark";
                            story.StyleGuide.NegativeKeywords = "WATERMARK";
                        }
                        string actual = remote.BuildImagePrompt(story, 0);
                        string shared = (string)sharedBuilder.Invoke(manager, new object[] { scene, 0, story.StoryScenes.Count });
                        Require(actual == shared, "Image prompt differs from MCombat in fixture " + fixture);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(story); }
                return UniTask.CompletedTask;
            });
            await Check("long MCombat captions use the same per-line 200-character segmentation", () =>
            {
                string description = new string('长', 220) + "。" + new string('文', 225);
                var remote = PocketStrikerRemoteStoryPrompts.CreateForValidation(config, templates);
                var story = remote.ParseStory(Page.Replace("邮差在云间找到失落的愿望。", description));
                var host = new GameObject("MCombat caption parity fixture");
                try
                {
                    var expected = new StoryInfo.StoryScene { Lines = new List<string> { description, "邮差: 我带你回家。" } };
                    var manager = host.AddComponent<AIServiceManager>();
                    var normalize = typeof(AIServiceManager).GetMethod("NormalizeStorySceneLines", BindingFlags.Instance | BindingFlags.NonPublic);
                    normalize.Invoke(manager, new object[] { expected });
                    var actual = story.StoryScenes[0].Lines;
                    Require(actual.Count == expected.Lines.Count && actual.Count > 2, "Long caption segments changed.");
                    for (int index = 0; index < actual.Count; index++)
                        Require(actual[index].Length <= 200 && actual[index] == expected.Lines[index], "Caption segment differs from MCombat.");
                }
                finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(story); }
                return UniTask.CompletedTask;
            });
            await Check("legacy failure never silently retries provider work", async () =>
            {
                int calls = 0;
                var client = new PocketStrikerLegacyStoryClient((request, token) => { calls++; return UniTask.FromException<PocketStrikerStoryJobClient.Result>(new InvalidOperationException("fixture failure")); });
                bool failed = false; try { await client.Generate("text", "fixture", default); } catch (InvalidOperationException) { failed = true; }
                Require(failed && calls == 1, "Legacy failure started another provider request.");
            });
            await Check("cancellation ends pending legacy transport without image requests", async () =>
            {
                using var cancel = new CancellationTokenSource(); int calls = 0;
                var pending = new UniTaskCompletionSource<PocketStrikerStoryJobClient.Result>();
                var client = new PocketStrikerLegacyStoryClient((request, token) => { calls++; return pending.Task; });
                var task = client.Generate("text", "fixture", cancel.Token); cancel.Cancel(); bool cancelled = false;
                try { await task; } catch (OperationCanceledException) { cancelled = true; }
                pending.TrySetResult(Ready("text")); Require(cancelled && calls == 1, "Cancelled legacy transport continued generation.");
            });
            await Check("cancellation after remote loading never starts text generation", async () =>
            {
                using var cancel = new CancellationTokenSource(); int calls = 0; bool cancelled = false;
                try
                {
                    await PocketStrikerQueuedStory.LoadForValidation(cancel.Token, "cancel-config", (kind, prompt, token) =>
                    { calls++; return UniTask.FromResult(Ready(kind)); }, (url, token) => throw new InvalidOperationException("Unexpected download."), token =>
                    { var remote = PocketStrikerRemoteStoryPrompts.CreateForValidation(config, templates); cancel.Cancel(); return UniTask.FromResult(remote); });
                }
                catch (OperationCanceledException) { cancelled = true; }
                Require(cancelled && calls == 0, "Cancelled remote loading started text generation.");
            });
            await Check("invalid image URLs are rejected before download", () =>
            {
                foreach (string url in new[] { "", "http://fixture.invalid/image", "file:///tmp/image", "https://user:secret@fixture.invalid/image" })
                {
                    bool rejected = false; try { PocketStrikerQueuedStory.ValidateImageUrl(new PocketStrikerStoryJobClient.Result { images = new[] { new PocketStrikerStoryJobClient.Image { url = url } } }); }
                    catch (InvalidOperationException) { rejected = true; }
                    Require(rejected, "Unsafe/missing image URL was accepted.");
                }
                return UniTask.CompletedTask;
            });
            await Check("cancellation after image download destroys the owned texture", async () =>
            {
                using var cancel = new CancellationTokenSource(); Texture2D texture = null; bool cancelled = false;
                var remote = PocketStrikerRemoteStoryPrompts.CreateForValidation(config, templates);
                try
                {
                    await PocketStrikerQueuedStory.LoadForValidation(cancel.Token, "cancel-image", (kind, prompt, token) => UniTask.FromResult(Ready(kind)),
                        (url, token) => { texture = new Texture2D(4, 4); cancel.Cancel(); return UniTask.FromResult(texture); }, token => UniTask.FromResult(remote));
                }
                catch (OperationCanceledException) { cancelled = true; }
                Require(cancelled && texture == null, "Cancelled pipeline retained an allocated texture.");
            });
            await Check("a late image download after cancellation still releases its texture", async () =>
            {
                using var cancel = new CancellationTokenSource(); bool cancelled = false;
                var pending = new UniTaskCompletionSource<Texture2D>();
                var remote = PocketStrikerRemoteStoryPrompts.CreateForValidation(config, templates);
                var task = PocketStrikerQueuedStory.LoadForValidation(cancel.Token, "cancel-late-download", (kind, prompt, token) => UniTask.FromResult(Ready(kind)),
                    (url, token) => pending.Task, token => UniTask.FromResult(remote));
                cancel.Cancel(); var texture = new Texture2D(4, 4); pending.TrySetResult(texture);
                try { await task; } catch (OperationCanceledException) { cancelled = true; }
                Require(cancelled && texture == null, "Late completion leaked an allocated texture.");
            });
        }
        finally
        {
            AIStoryRuntimeContext.LanguageProvider = originalLanguage;
            report.passed = report.failures.Count == 0 && report.checks.Count == attempted && attempted >= 17;
            Directory.CreateDirectory("Logs/AIStory/Variety"); File.WriteAllText("Logs/AIStory/Variety/report.json", JsonUtility.ToJson(report, true));
            Debug.Log("[StoryVarietyValidation] " + (report.passed ? "PASS" : "FAIL") + ": " + report.checks.Count + " checks");
            EditorApplication.Exit(report.passed ? 0 : 1);
        }
    }
    static string AddRemoteMarker(string json, string marker)
    {
        var document = (IDictionary<string, object>)PlayFabSimpleJson.DeserializeObject(json);
        foreach (var locale in (IList<object>)document["locales"])
        {
            var fields = (IDictionary<string, object>)locale;
            fields["template"] = (string)fields["template"] + "\n" + marker + " text-template";
        }
        var image = (IDictionary<string, object>)document["image"];
        image["sceneFormat"] = (string)image["sceneFormat"] + marker + " image-template ";
        return PlayFabSimpleJson.SerializeObject(document);
    }
    static Parameters ParametersOf(object parameters) => JsonUtility.FromJson<Parameters>(PlayFabSimpleJson.SerializeObject(parameters));
    static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static PocketStrikerStoryJobClient.Result Ready(string kind) => kind == "text"
        ? new PocketStrikerStoryJobClient.Result { text = Page }
        : new PocketStrikerStoryJobClient.Result { images = new[] { new PocketStrikerStoryJobClient.Image { url = "https://story-fixture.invalid/scene.jpg", mimeType = "image/jpeg" } } };
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
