using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using PlayFab.CloudScriptModels;
using PlayFab.Json;
using UnityEditor;
using UnityEngine;

/// <summary>Actual project pipeline/payload fixtures; no account, provider or network calls.</summary>
public static class PocketStrikerStoryVarietyValidation
{
    [Serializable] public sealed class Report { public bool passed; public List<string> checks = new List<string>(); public List<string> failures = new List<string>(); }
    [Serializable] sealed class Parameters { public string prompt, model, imageModel, aspectRatio, cacheKey, storyId; public int timeoutMs, sampleCount, sceneIndex; }
    const string Page = "{\"title\":\"小小发现\",\"lines\":[\"邮差在云间找到失落的愿望。\",\"一只小蟹帮它回到了家。\"],\"visualPrompt\":\"A joyful courier catches a glowing wish above a cloud village.\"}";
    public static void ValidateBatch() => Run().Forget();
    static async UniTaskVoid Run()
    {
        var report = new Report();
        var originalLanguage = AIStoryRuntimeContext.LanguageProvider;
        AIStoryRuntimeContext.LanguageProvider = () => SystemLanguage.Chinese;
        async UniTask Check(string name, Func<UniTask> action)
        {
            try { await action(); report.checks.Add(name); }
            catch (Exception error) { report.failures.Add(name + ": " + error); }
        }
        try
        {
            await Check("18 genre shuffle bag exhausts without repeats or boundary duplicates", () =>
            {
                var type = typeof(PocketStrikerStoryVariety).GetNestedType("ShuffleBag", BindingFlags.NonPublic);
                var bag = Activator.CreateInstance(type, new object[] { PocketStrikerStoryVariety.Themes.Length });
                var draw = type.GetMethod("Draw"); int previous = -1;
                for (int cycle = 0; cycle < 8; cycle++)
                {
                    var seen = new HashSet<int>();
                    for (int i = 0; i < PocketStrikerStoryVariety.Themes.Length; i++)
                    { int choice = (int)draw.Invoke(bag, null); Require(choice != previous && seen.Add(choice), "A subject repeated before bag exhaustion."); previous = choice; }
                    Require(seen.Count == 18, "Subject count changed.");
                }
                return UniTask.CompletedTask;
            });
            await Check("attempt choices are stable and real retry changes seed and subject", () =>
            {
                var fight = ScriptableObject.CreateInstance<FightInfo>();
                try
                {
                    var first = PocketStrikerStoryVariety.ForFight(fight);
                    Require(ReferenceEquals(first, PocketStrikerStoryVariety.ForFight(fight)), "Ordinary read selected a new subject.");
                    PocketStrikerStoryVariety.BeginBattleAttempt(fight); var next = PocketStrikerStoryVariety.ForFight(fight);
                    Require(first.Seed != next.Seed && first.ThemeIndex != next.ThemeIndex, "Retry repeated selection/cache identity.");
                    var seeded = PocketStrikerStoryVariety.ForSeed("offline-seed"); var again = PocketStrikerStoryVariety.ForSeed("offline-seed");
                    Require(seeded.Theme == again.Theme && seeded.Twist == again.Twist && seeded.Tone == again.Tone, "Explicit acceptance seed changed.");
                }
                finally { UnityEngine.Object.DestroyImmediate(fight); }
                return UniTask.CompletedTask;
            });
            await Check("shared legacy configuration includes all genres and required cartoon style", () =>
            {
                var config = AssetDatabase.LoadAssetAtPath<AIServiceConfig>("Assets/AIStory/AIServiceConfig.asset");
                Require(config != null && config.StoryThemes.SequenceEqual(PocketStrikerStoryVariety.Themes), "Legacy asset subject catalog diverged.");
                Require(config.ImageStyle == ImageStyle.Custom && config.GetImageStylePrompt().Contains(PocketStrikerStoryVariety.CartoonStyle), "Legacy art direction is missing.");
                Require(config.ImageAspectRatio == PocketStrikerStoryVariety.ImageAspectRatio, "Legacy asset must request portrait artwork.");
                return UniTask.CompletedTask;
            });
            await Check("legacy envelopes preserve registered endpoints and bounded cached budgets", () =>
            {
                var text = PocketStrikerLegacyStoryClient.BuildRequest("text", "selected story prompt");
                var image = PocketStrikerLegacyStoryClient.BuildRequest("image", "selected image prompt");
                var textInput = JsonUtility.FromJson<Parameters>(PlayFabSimpleJson.SerializeObject(text.FunctionParameter));
                var imageInput = JsonUtility.FromJson<Parameters>(PlayFabSimpleJson.SerializeObject(image.FunctionParameter));
                Require(text.FunctionName == "generateGeminiText" && image.FunctionName == "generateGeminiImages", "Registered function names changed.");
                Require(textInput.model == "gemini-2.5-flash-lite" && textInput.timeoutMs == 20000 && !string.IsNullOrEmpty(textInput.cacheKey), "Text budget/cache missing.");
                Require(imageInput.imageModel == "gemini-3.1-flash-image" && imageInput.sampleCount == 1 && imageInput.aspectRatio == "9:16"
                    && imageInput.timeoutMs == 60000 && imageInput.storyId == imageInput.cacheKey && imageInput.sceneIndex == 1, "Image budget/envelope changed.");
                var replay = JsonUtility.FromJson<Parameters>(PlayFabSimpleJson.SerializeObject(PocketStrikerLegacyStoryClient.BuildRequest("text", "selected story prompt").FunctionParameter));
                Require(replay.cacheKey == textInput.cacheKey && text.GeneratePlayStreamEvent == false && image.GeneratePlayStreamEvent == false, "Replay cache key or event policy changed.");
                return UniTask.CompletedTask;
            });
            var prompts = new Dictionary<string, string>();
            foreach (bool queued in new[] { false, true })
            {
                await Check((queued ? "queued" : "legacy") + " sends selected subject and cartoon constraints through real text/image pipeline", async () =>
                {
                    string route = queued ? "queued" : "legacy";
                    var variant = PocketStrikerStoryVariety.ForSeed("offline-pipeline");
                    var texture = new Texture2D(4, 4); StoryInfo story = null;
                    Func<string, string, CancellationToken, UniTask<PocketStrikerStoryJobClient.Result>> generate;
                    if (queued)
                    {
                        var client = new PocketStrikerStoryJobClient((request, token) =>
                        {
                            Require(request.operation == "start", "Ready fixture unexpectedly polled.");
                            prompts[route + request.kind] = request.input.prompt;
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
                            prompts[route + kind] = JsonUtility.FromJson<Parameters>(PlayFabSimpleJson.SerializeObject(request.FunctionParameter)).prompt;
                            return UniTask.FromResult(Ready(kind));
                        });
                        generate = client.Generate;
                    }
                    try
                    {
                        story = await PocketStrikerQueuedStory.LoadForValidation(default, variant.Seed, generate, (url, token) =>
                        { Require(url == "https://story-fixture.invalid/scene.jpg", "URL changed before download."); return UniTask.FromResult(texture); });
                        Require(story.HasVisualScene() && story.StoryScenes.Count == 1 && story.StoryScenes[0].Lines.Count == 2, "Complete one-page story was lost.");
                        foreach (string kind in new[] { "text", "image" })
                            Require(prompts[route + kind].Contains(variant.Theme) && prompts[route + kind].Contains(PocketStrikerStoryVariety.CartoonStyle), "Actual provider prompt missed selected subject/style.");
                        Require(prompts[route + "image"].Contains(PocketStrikerStoryVariety.PortraitComposition), "Actual image prompt missed portrait composition.");
                        PocketStrikerQueuedStory.Release(story); Require(story == null && texture == null, "Owned story/image was retained after release.");
                    }
                    finally { PocketStrikerQueuedStory.Release(story); if (texture != null) UnityEngine.Object.DestroyImmediate(texture); }
                });
            }
            await Check("legacy and queued routes use identical narrative and visual prompts", () =>
            {
                Require(prompts["legacytext"] == prompts["queuedtext"] && prompts["legacyimage"] == prompts["queuedimage"], "Transport changed story/art direction.");
                return UniTask.CompletedTask;
            });
            await Check("legacy failure never silently retries provider work", async () =>
            {
                int calls = 0;
                var client = new PocketStrikerLegacyStoryClient((request, token) => { calls++; return UniTask.FromException<PocketStrikerStoryJobClient.Result>(new InvalidOperationException("fixture failure")); });
                bool failed = false; try { await client.Generate("text", "fixture", default); } catch (InvalidOperationException) { failed = true; }
                Require(failed && calls == 1, "Legacy failure started another provider request.");
            });
            await Check("cancellation ends pending legacy transport without creating images", async () =>
            {
                using var cancel = new CancellationTokenSource(); int calls = 0;
                var pending = new UniTaskCompletionSource<PocketStrikerStoryJobClient.Result>();
                var client = new PocketStrikerLegacyStoryClient((request, token) => { calls++; return pending.Task; });
                var task = client.Generate("text", "fixture", cancel.Token); cancel.Cancel(); bool cancelled = false;
                try { await task; } catch (OperationCanceledException) { cancelled = true; }
                pending.TrySetResult(Ready("text")); Require(cancelled && calls == 1, "Cancelled transport continued generation.");
            });
            await Check("invalid generated caption never requests an image", async () =>
            {
                int images = 0; bool rejected = false;
                try { await PocketStrikerQueuedStory.LoadForValidation(default, "invalid", (kind, prompt, token) =>
                    { if (kind == "image") images++; return UniTask.FromResult(new PocketStrikerStoryJobClient.Result { text = "{\"lines\":[\"only one\"],\"visualPrompt\":\"scene\"}" }); },
                    (url, token) => throw new InvalidOperationException("Unexpected download.")); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected && images == 0, "Malformed text caused paid image work.");
            });
            await Check("invalid image URL results are rejected before download", () =>
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
                try { await PocketStrikerQueuedStory.LoadForValidation(cancel.Token, "cancel-image", (kind, prompt, token) => UniTask.FromResult(Ready(kind)),
                    (url, token) => { texture = new Texture2D(4, 4); cancel.Cancel(); return UniTask.FromResult(texture); }); }
                catch (OperationCanceledException) { cancelled = true; }
                Require(cancelled && texture == null, "Cancelled pipeline retained an allocated texture.");
            });
        }
        finally
        {
            AIStoryRuntimeContext.LanguageProvider = originalLanguage;
            report.passed = report.failures.Count == 0 && report.checks.Count == 12;
            Directory.CreateDirectory("Logs/AIStory/Variety"); File.WriteAllText("Logs/AIStory/Variety/report.json", JsonUtility.ToJson(report, true));
            Debug.Log("[StoryVarietyValidation] " + (report.passed ? "PASS" : "FAIL") + ": " + report.checks.Count + " checks");
            EditorApplication.Exit(report.passed ? 0 : 1);
        }
    }
    static PocketStrikerStoryJobClient.Result Ready(string kind) => kind == "text"
        ? new PocketStrikerStoryJobClient.Result { text = Page }
        : new PocketStrikerStoryJobClient.Result { images = new[] { new PocketStrikerStoryJobClient.Image { url = "https://story-fixture.invalid/scene.jpg", mimeType = "image/jpeg" } } };
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
