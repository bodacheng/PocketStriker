using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>Request-count and late-response contracts, without provider/account requests.</summary>
public static class PocketStrikerAIStoryLifecycleValidation
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    [Serializable] public sealed class Report { public bool passed; public List<string> checks = new List<string>(); public List<string> errors = new List<string>(); }
    public static void ValidateBatch()
    {
        var report = new Report();
        var originalFight = FightLoad.Fight;
        var originalController = global::FightScene.FightScene.target;
        var created = new List<UnityEngine.Object>();
        var texture = new Texture2D(4, 4); created.Add(texture);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f)); created.Add(sprite);
        StoryInfo Story(string line)
        {
            var story = ScriptableObject.CreateInstance<StoryInfo>(); created.Add(story);
            story.StoryScenes = new List<StoryInfo.StoryScene> { new StoryInfo.StoryScene { Pic = sprite, Lines = new List<string> { line } } };
            return story;
        }
        FightInfo Fight(string id = "3")
        {
            var fight = ScriptableObject.CreateInstance<FightInfo>(); created.Add(fight);
            fight.ID = id; fight.EventType = FightEventType.Quest;
            return fight;
        }
        global::FightScene.FightScene Controller(Func<UniTask<StoryInfo>> loader)
        {
            var root = new GameObject("Inactive AI story lifecycle probe"); root.SetActive(false); created.Add(root);
            var controller = root.AddComponent<global::FightScene.FightScene>();
            typeof(global::FightScene.FightScene).GetField("StoryLoaderForValidation", Fields).SetValue(controller, loader);
            return controller;
        }
        void Check(string name, Action action)
        {
            try { action(); report.checks.Add(name); }
            catch (Exception error) { report.errors.Add(name + ": " + error); }
        }
        try
        {
            Check("failed synchronous response is shared within one attempt", () =>
            {
                int calls = 0; FightLoad.Fight = Fight();
                var controller = Controller(() => { calls++; return UniTask.FromResult<StoryInfo>(null); });
                controller.BeginStoryBattleAttempt(); controller.PreloadAIStory(true); controller.PreloadAIStory();
                Require(controller.EnsureAIStory().GetAwaiter().GetResult() == null && calls == 1, "Ordinary reads restarted a failed network request.");
                controller.PreloadAIStory(); Require(calls == 1, "Repeated startup preload reissued a failed request.");
            });
            Check("same FightInfo next attempt can recover a previous empty response", () =>
            {
                int calls = 0; FightLoad.Fight = Fight(); var ready = Story("recovered");
                var controller = Controller(() => UniTask.FromResult(++calls == 1 ? null : ready));
                controller.BeginStoryBattleAttempt(); var failed = controller.EnsureAIStory();
                Require(failed.GetAwaiter().GetResult() == null, "First request should be empty.");
                controller.BeginStoryBattleAttempt();
                Require(controller.EnsureAIStory().GetAwaiter().GetResult() == ready && controller.AIStoryInfo == ready && calls == 2,
                    "The same-fight retry retained the completed empty source.");
            });
            Check("repeated preload keeps one pending provider request", () =>
            {
                int calls = 0; FightLoad.Fight = Fight(); var pending = new UniTaskCompletionSource<StoryInfo>(); var ready = Story("pending");
                var controller = Controller(() => { calls++; return pending.Task; });
                controller.PreloadAIStory(true); controller.PreloadAIStory(true); controller.PreloadAIStory();
                Require(calls == 1, "Pending request was duplicated."); pending.TrySetResult(ready);
                Require(controller.EnsureAIStory().GetAwaiter().GetResult() == ready && calls == 1, "Pending response was lost.");
            });
            Check("repeated preload reuses a ready story without another provider call", () =>
            {
                int calls = 0; FightLoad.Fight = Fight(); var ready = Story("ready");
                var controller = Controller(() => { calls++; return UniTask.FromResult(ready); });
                controller.PreloadAIStory(true); controller.PreloadAIStory(true);
                Require(controller.AIStoryInfo == ready && calls == 1, "Ready story triggered a duplicate provider call.");
            });
            Check("new fight cancels old consumer and rejects its late response", () =>
            {
                int calls = 0; var pendingA = new UniTaskCompletionSource<StoryInfo>(); var pendingB = new UniTaskCompletionSource<StoryInfo>();
                var storyA = Story("old"); var storyB = Story("new");
                FightLoad.Fight = Fight(); var controller = Controller(() => ++calls == 1 ? pendingA.Task : pendingB.Task);
                var consumerA = controller.EnsureAIStory(); FightLoad.Fight = Fight("4"); var consumerB = controller.EnsureAIStory();
                Require(consumerA.GetAwaiter().GetResult() == null && calls == 2, "Old fight consumer was not cancelled.");
                pendingB.TrySetResult(storyB); pendingA.TrySetResult(storyA);
                Require(consumerB.GetAwaiter().GetResult() == storyB && controller.AIStoryInfo == storyB, "Late old response overwrote the new fight.");
            });
            Check("destroyed scene completes pending consumer without a late story", () =>
            {
                FightLoad.Fight = Fight(); var pending = new UniTaskCompletionSource<StoryInfo>(); var ready = Story("late");
                var controller = Controller(() => pending.Task); var consumer = controller.EnsureAIStory();
                typeof(global::FightScene.FightScene).GetMethod("OnDestroy", Fields).Invoke(controller, null);
                Require(consumer.GetAwaiter().GetResult() == null, "Destroy left a consumer pending.");
                pending.TrySetResult(ready); Require(controller.AIStoryInfo == null, "Destroyed scene accepted a late story.");
            });
            Check("tutorial and training stay excluded from AI requests", () =>
            {
                int calls = 0; var controller = Controller(() => { calls++; return UniTask.FromResult<StoryInfo>(null); });
                foreach (var id in new[] { "1", "2" }) { FightLoad.Fight = Fight(id); controller.PreloadAIStory(true); }
                var tutorial = Fight(); tutorial.RunTutorial = true; FightLoad.Fight = tutorial; controller.PreloadAIStory(true);
                var training = Fight(); training.EventType = FightEventType.Self; FightLoad.Fight = training; controller.PreloadAIStory(true);
                Require(calls == 0 && controller.EnsureAIStory().GetAwaiter().GetResult() == null, "Excluded fight called a provider.");
            });
            Check("exception can recover only at an explicit next attempt", () =>
            {
                int calls = 0; FightLoad.Fight = Fight(); var ready = Story("recovered exception");
                var controller = Controller(() => ++calls == 1 ? UniTask.FromException<StoryInfo>(new InvalidOperationException("fixture provider fault")) : UniTask.FromResult(ready));
                controller.BeginStoryBattleAttempt(); Require(controller.EnsureAIStory().GetAwaiter().GetResult() == null && calls == 1, "Exception escaped or ordinary read retried.");
                controller.PreloadAIStory(true); Require(calls == 1, "Repeated preload retried a failed request.");
                controller.BeginStoryBattleAttempt(); Require(controller.AIStoryInfo == ready && calls == 2, "Explicit next attempt could not recover exception.");
            });
            Check("real retry changes a ready story and attempt seed on the same FightInfo", () =>
            {
                int calls = 0; FightLoad.Fight = Fight(); var old = Story("old attempt"); var next = Story("new attempt");
                var controller = Controller(() => UniTask.FromResult(++calls == 1 ? old : next));
                controller.BeginStoryBattleAttempt(); string firstSeed = PocketStrikerStoryVariety.ForFight(FightLoad.Fight).Seed;
                controller.PreloadAIStory(true); Require(controller.AIStoryInfo == old && calls == 1, "Repeated preload started another paid request.");
                controller.BeginStoryBattleAttempt();
                Require(controller.AIStoryInfo == next && calls == 2 && firstSeed != PocketStrikerStoryVariety.ForFight(FightLoad.Fight).Seed,
                    "Real retry reused the previous story or provider cache identity.");
            });
            Check("real retry cancels pending story and ignores its late response", () =>
            {
                int calls = 0; FightLoad.Fight = Fight(); var old = new UniTaskCompletionSource<StoryInfo>(); var next = Story("new request");
                var controller = Controller(() => ++calls == 1 ? old.Task : UniTask.FromResult(next));
                controller.BeginStoryBattleAttempt(); var previousConsumer = controller.EnsureAIStory(); controller.BeginStoryBattleAttempt();
                Require(previousConsumer.GetAwaiter().GetResult() == null && controller.AIStoryInfo == next && calls == 2, "Retry did not end the previous consumer.");
                old.TrySetResult(Story("late old request")); Require(controller.AIStoryInfo == next, "Late old attempt replaced the new story.");
            });
        }
        finally
        {
            FightLoad.Fight = originalFight; global::FightScene.FightScene.target = originalController;
            for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            report.passed = report.errors.Count == 0 && report.checks.Count == 10;
            Directory.CreateDirectory("Logs/AIStory/Lifecycle"); File.WriteAllText("Logs/AIStory/Lifecycle/report.json", JsonUtility.ToJson(report, true));
            Debug.Log("[AIStoryLifecycle] " + (report.passed ? "PASS" : "FAIL"));
            EditorApplication.Exit(report.passed ? 0 : 1);
        }
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
