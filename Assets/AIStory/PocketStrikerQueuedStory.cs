using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Single-page PocketStriker adapter; shared MCombat package remains unchanged.</summary>
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
    [Serializable] sealed class Page { public string title; public string[] lines; public string visualPrompt; }

    public static async UniTask<StoryInfo> Load(CancellationToken cancellationToken, string seed = null)
    {
        // Cancel the owned pipeline before BattleStoryRequest's 110-second consumer deadline,
        // so a timed-out text/image chain cannot create an unobserved owned texture later.
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lifetime.CancelAfter(100000);
        cancellationToken = lifetime.Token;
        var client = new PocketStrikerStoryJobClient();
        string language = AIStoryRuntimeContext.GetLanguage().ToString();
        var context = AIStoryRuntimeContext.GetCacheContext();
        seed ??= context.FightId + ":" + context.EventType + ":" + context.FightMode + ":" + DateTime.UtcNow.ToString("yyyy-MM-dd");
        string theme = "A red-crested Roman gladiator and a skeleton warrior learn teamwork through elemental magic stones.";
        string prompt = "Write one short hopeful PocketStriker magic-stone fighting aftermath in " + language
            + ". " + theme + " No football, no science-fiction HUD. Return only JSON with title (short), lines (2 or 3 brief narrative lines, total at most 200 characters), visualPrompt (English illustration description, no text). "
            + "Keep characters recognizable: tan gladiator, silver crested helmet, gray cuirass, red scarf/cape and skirt; skeleton with beige skull, gray spiked armor and dark red belt. Story variation: "
            + seed;
        var text = await client.Generate("text", prompt, cancellationToken, 30000, retryFailed: true);
        Page page;
        try { page = JsonUtility.FromJson<Page>(NormalizeJson(text.text)); }
        catch { throw new InvalidOperationException("Invalid story text JSON."); }
        if (page?.lines == null || page.lines.Length < 1 || page.lines.Length > 3 || string.IsNullOrWhiteSpace(page.visualPrompt))
            throw new InvalidOperationException("Story text is incomplete.");
        var lines = new List<string>();
        foreach (string line in page.lines)
            if (!string.IsNullOrWhiteSpace(line)) lines.Add(line.Trim());
        if (lines.Count == 0 || string.Join("", lines).Length > 200) throw new InvalidOperationException("Story caption is invalid.");
        string imagePrompt = "Hand-painted colorful cartoon fantasy game illustration, faceted chunky silhouettes, warm adventurous tone, full-body readable duel, no text, no logos. "
            + theme + " Character details: tan Roman gladiator with red-crested silver helmet, gray cuirass, red scarf/cape/skirt; skeleton in gray spiked armor, dark red belt. " + page.visualPrompt;
        var generated = await client.Generate("image", imagePrompt, cancellationToken, retryFailed: true);
        if (!Uri.TryCreate(generated.images[0].url, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            throw new InvalidOperationException("Invalid story image URL.");
        Texture2D texture = null;
        Sprite sprite = null;
        try
        {
            using (var download = UnityWebRequestTexture.GetTexture(uri.AbsoluteUri))
            {
                download.timeout = 15;
                try { await download.SendWebRequest().ToUniTask(cancellationToken: cancellationToken); }
                catch (OperationCanceledException) { throw; }
                catch { throw new InvalidOperationException("Story image download failed."); }
                texture = DownloadHandlerTexture.GetContent(download);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (texture == null) throw new InvalidOperationException("Story image is empty.");
            sprite = Sprite.Create(texture, new Rect(0,0,texture.width,texture.height), new Vector2(.5f,.5f));
            var story = ScriptableObject.CreateInstance<StoryInfo>();
            story.StoryScenes = new List<StoryInfo.StoryScene> { new StoryInfo.StoryScene { Pic = sprite, Title = page.title, Lines = lines } };
            Owned.Add(story);
            return story;
        }
        catch
        {
            if (sprite != null) UnityEngine.Object.Destroy(sprite);
            if (texture != null) UnityEngine.Object.Destroy(texture);
            throw;
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
            UnityEngine.Object.Destroy(scene.Pic.texture);
            UnityEngine.Object.Destroy(scene.Pic);
        }
        UnityEngine.Object.Destroy(story);
    }

}
