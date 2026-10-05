using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Remote-configured PocketStriker adapter; shared MCombat package remains unchanged.</summary>
public static class PocketStrikerQueuedStory
{
    // Enable only after the companion Azure deployment passes cold/warm validation.
    // A compile-time opt-in prevents an old server from accidentally interpreting job polls as new generation.
    public static bool Enabled
    {
        get
        {
#if POCKETSTRIKER_QUEUED_STORIES
            return true;
#else
            return false;
#endif
        }
    }
    static readonly HashSet<StoryInfo> Owned = new HashSet<StoryInfo>();

    public static UniTask<StoryInfo> Load(CancellationToken cancellationToken, string seed = null)
    {
        var client = new PocketStrikerStoryJobClient();
        return LoadStory(cancellationToken, seed,
            (kind, prompt, token) => client.Generate(kind, prompt, token, kind == "text" ? 30000 : 90000, retryFailed: true),
            DownloadImage, 100000, PocketStrikerRemoteStoryPrompts.Load);
    }

    public static UniTask<StoryInfo> LoadLegacy(CancellationToken cancellationToken, string seed = null)
    {
        var client = new PocketStrikerLegacyStoryClient();
        return LoadStory(cancellationToken, seed, client.Generate, DownloadImage, 55000, PocketStrikerRemoteStoryPrompts.Load);
    }

#if UNITY_EDITOR
    public static UniTask<StoryInfo> LoadForValidation(CancellationToken token, string seed,
        Func<string, string, CancellationToken, UniTask<PocketStrikerStoryJobClient.Result>> generate,
        Func<string, CancellationToken, UniTask<Texture2D>> download,
        Func<CancellationToken, UniTask<PocketStrikerRemoteStoryPrompts>> loadPrompts = null) =>
        LoadStory(token, seed, generate, download, 100000, loadPrompts ?? PocketStrikerRemoteStoryPrompts.Load);
#endif

    static async UniTask<StoryInfo> LoadStory(CancellationToken cancellationToken, string seed,
        Func<string, string, CancellationToken, UniTask<PocketStrikerStoryJobClient.Result>> generate,
        Func<string, CancellationToken, UniTask<Texture2D>> download, int deadlineMs,
        Func<CancellationToken, UniTask<PocketStrikerRemoteStoryPrompts>> loadPrompts)
    {
        // Cancel the owned pipeline before BattleStoryRequest's 110-second consumer deadline,
        // so a timed-out text/image chain cannot create an unobserved owned texture later.
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lifetime.CancelAfter(deadlineMs);
        cancellationToken = lifetime.Token;
        cancellationToken.ThrowIfCancellationRequested();
        var prompts = await loadPrompts(cancellationToken).AttachExternalCancellation(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (prompts == null) throw new InvalidOperationException("Story prompt resources are unavailable.");
        var language = AIStoryRuntimeContext.GetLanguage();
        var variation = seed == null ? PocketStrikerStoryVariety.ForFight(FightLoad.Fight) : PocketStrikerStoryVariety.ForSeed(seed);
        string prompt = prompts.BuildTextPrompt(variation.Seed, language);
        var text = await generate("text", prompt, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var story = prompts.ParseStory(NormalizeJson(text?.text));
        Owned.Add(story);
        try
        {
            for (int i = 0; i < story.StoryScenes.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string imagePrompt = prompts.BuildImagePrompt(story, i);
                var generated = await generate("image", imagePrompt, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                string imageUrl = ValidateImageUrl(generated);
                Texture2D texture = null;
                try
                {
                    texture = await download(imageUrl, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (texture == null) throw new InvalidOperationException("Story image is empty.");
                    story.StoryScenes[i].Pic = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
                }
                catch
                {
                    DestroyOwned(texture);
                    throw;
                }
            }
            return story;
        }
        catch
        {
            Release(story);
            throw;
        }
    }

    public static string ValidateImageUrl(PocketStrikerStoryJobClient.Result result)
    {
        if (result?.images?.Length != 1 || !Uri.TryCreate(result.images[0]?.url, UriKind.Absolute, out var uri)
            || uri.Scheme != "https" || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("Invalid story image URL.");
        return uri.AbsoluteUri;
    }

    static async UniTask<Texture2D> DownloadImage(string url, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using (var request = UnityWebRequestTexture.GetTexture(url))
        {
            request.timeout = 15;
            try { await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken); }
            catch (OperationCanceledException) { throw; }
            catch { throw new InvalidOperationException("Story image download failed."); }
            return DownloadHandlerTexture.GetContent(request);
        }
    }
    // Gemini may wrap otherwise valid JSON in Markdown even when the prompt requests JSON only.
    // Accept only one complete fenced object; do not extract arbitrary fragments from malformed prose.
    public static string NormalizeJson(string value)
    {
        value = value?.Trim() ?? "";
        if (value.StartsWith("```"))
        {
            if (!value.EndsWith("```") || value.Length <= 6) throw new InvalidOperationException("Invalid story text JSON.");
            int start = value.StartsWith("```json", StringComparison.OrdinalIgnoreCase) ? 7 : 3;
            value = value.Substring(start, value.Length - start - 3).Trim();
        }
        if (!value.StartsWith("{") || !value.EndsWith("}")) throw new InvalidOperationException("Invalid story text JSON.");
        return value;
    }

    public static void Release(StoryInfo story)
    {
        if (story == null || !Owned.Remove(story)) return;
        foreach (var scene in story.StoryScenes)
        {
            if (scene?.Pic == null) continue;
            DestroyOwned(scene.Pic.texture);
            DestroyOwned(scene.Pic);
        }
        DestroyOwned(story);
    }

    static void DestroyOwned(UnityEngine.Object value)
    {
        if (value == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) { UnityEngine.Object.DestroyImmediate(value); return; }
#endif
        UnityEngine.Object.Destroy(value);
    }

}
