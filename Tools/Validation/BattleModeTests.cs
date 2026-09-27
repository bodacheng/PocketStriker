using System;
using System.Collections.Generic;
using System.IO;
using mainMenu;
using PlayFab;
using PlayFab.ClientModels;

// Production rules and callback chains run unchanged. Only Unity menu plumbing
// and the CloudScript transport are substituted; no account data is written.
internal static class BattleModeTests
{
    static int checks;

    public static int Main(string[] args)
    {
        try
        {
            Check(args.Length == 1, "stage-mode table path supplied");
            CheckAdventureTable(args[0]);
            CheckAdventureTeams();
            CheckNavigation();
            CheckBossRewards();
            Console.WriteLine("PASS: " + checks + " battle mode checks");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    static void CheckAdventureTable(string path)
    {
        var rows = File.ReadAllLines(path);
        Check(rows.Length > 0 && rows[0].StartsWith("STAGE_ID,"), "stage table header retained");
        var stages = new Dictionary<int, int>();
        var modes = new HashSet<int>();
        for (int row = 1; row < rows.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(rows[row])) continue;
            var columns = rows[row].Split(',');
            Check(columns.Length == 2, "stage table row has an ID and mode");
            int mode = int.Parse(columns[1]);
            if (!int.TryParse(columns[0], out int stage))
            {
                Check(columns[0] == "easy_1" || columns[0] == "normal_1" || columns[0] == "hard_1",
                    "only the three legacy Boss rows accompany adventure stages");
                continue;
            }
            Check(stage >= 1 && stage <= 130 && !stages.ContainsKey(stage), "stage IDs are unique and within 1–130");
            Check(mode >= 1 && mode <= 3, "stage has a fixed supported mode");
            stages.Add(stage, mode);
            modes.Add(AdventureModeRules.ResolveMode(stage.ToString(), mode));
        }
        Check(stages.Count == 130, "all 130 adventure stages are configured");
        for (int stage = 1; stage <= 130; stage++)
            Check(stages.ContainsKey(stage), "no missing adventure stage: " + stage);
        Check(stages[1] == 2 && stages[2] == 2, "tutorial table rows are rotation battles");
        Check(modes.SetEquals(new[] { 1, 2, 3 }), "adventure contains team, rotation and evolution battles");
        foreach (string tutorial in new[] { "1", "2" })
        {
            foreach (int configured in new[] { -1, 0, 1, 2, 3, 4 })
                Check(AdventureModeRules.ResolveMode(tutorial, configured) == 2,
                    "tutorial remains rotation despite stale mode configuration");
        }
        foreach (int mode in new[] { 1, 2, 3 })
            Check(AdventureModeRules.ResolveMode("3", mode) == mode, "normal stages use their configured mode");
        foreach (int invalid in new[] { -1, 0, 4, int.MaxValue })
            Check(AdventureModeRules.ResolveMode("130", invalid) == 2, "invalid mode falls back to rotation");
    }

    static void CheckAdventureTeams()
    {
        foreach (string tutorial in new[] { "1", "2" })
        {
            Check(AdventureModeRules.IsTutorialStage(tutorial), "first two stages are tutorial stages");
            foreach (bool evolution in new[] { false, true })
            {
                Check(AdventureModeRules.UsesSingleHero(tutorial, evolution), "tutorial always uses one hero");
                Check(AdventureModeRules.GetHeroLimit(tutorial, evolution) == 1, "tutorial hero limit");
                Check(AdventureModeRules.GetTeamSetKey(tutorial, evolution) == "arcade", "tutorial uses the saved adventure hero");
                Check(AdventureModeRules.IsValidHeroCount(tutorial, evolution, 1), "tutorial accepts one hero");
                foreach (int invalid in new[] { -1, 0, 2, 3, 4 })
                    Check(!AdventureModeRules.IsValidHeroCount(tutorial, evolution, invalid), "tutorial rejects invalid hero count");
            }
        }
        foreach (string stage in new[] { "3", "50", "130" })
        {
            Check(!AdventureModeRules.IsTutorialStage(stage), "later stages are not tutorial stages");
            Check(AdventureModeRules.UsesSingleHero(stage, true), "evolution uses one hero");
            Check(AdventureModeRules.GetHeroLimit(stage, true) == 1, "evolution hero limit");
            Check(AdventureModeRules.GetTeamSetKey(stage, true) == "arcade", "evolution preserves adventure hero selection");
            Check(AdventureModeRules.IsValidHeroCount(stage, true, 1), "evolution accepts one hero");
            foreach (int invalid in new[] { -1, 0, 2, 3, 4 })
                Check(!AdventureModeRules.IsValidHeroCount(stage, true, invalid), "evolution rejects invalid hero count");
            Check(!AdventureModeRules.UsesSingleHero(stage, false), "team and rotation battles use a squad");
            Check(AdventureModeRules.GetHeroLimit(stage, false) == 3, "squad hero limit");
            Check(AdventureModeRules.GetTeamSetKey(stage, false) == "origin", "squad reuses the standard team selection");
            foreach (int valid in new[] { 1, 2, 3 })
                Check(AdventureModeRules.IsValidHeroCount(stage, false, valid), "squad accepts one to three owned heroes");
            foreach (int invalid in new[] { -1, 0, 4, int.MaxValue })
                Check(!AdventureModeRules.IsValidHeroCount(stage, false, invalid), "squad rejects empty or oversized teams");
        }
    }

    static void CheckNavigation()
    {
        var runner = ProcessesRunner.Main;
        runner.Clear();
        var front = new MenuProcess(MainSceneStep.FrontPage);
        var boss = new MenuProcess(MainSceneStep.RandomBoss);
        runner.Add(MainSceneStep.FrontPage, front);
        runner.Add(MainSceneStep.RandomBoss, boss);
        Check(runner.ChangeProcess(MainSceneStep.GangBangFront), "retired Chaos route redirects");
        Check(runner.currentProcess == front && front.Entered == 1, "Chaos route opens home");
        Check(runner.ChangeProcess(MainSceneStep.EventFight, "legacy payload"), "retired Boss route redirects");
        Check(front.Entered == 2 && front.TypedEntered == 0, "legacy payload is not passed to the home screen");
        Check(runner.ChangeProcess(MainSceneStep.RandomBoss), "random Boss route available");
        Check(runner.currentProcess == boss && runner.CurrentStep.Value == MainSceneStep.RandomBoss, "random Boss navigation state");
        Check(!runner.ChangeProcess((MainSceneStep)999), "unknown route fails");
        Check(runner.currentProcess == boss && boss.Ended == 0, "unknown route preserves the current screen");
        boss.Ready = false;
        Check(!runner.ChangeProcess(MainSceneStep.EventFight), "legacy redirect respects a busy screen");
        Check(runner.currentProcess == boss && boss.Ended == 0, "busy screen remains open");
        Check((int)MainSceneStep.EventFight == 12 && (int)MainSceneStep.GangBangFront == 13 && (int)MainSceneStep.RandomBoss == 28,
            "serialized scene enum values retained");
        Check((int)FightEventType.Quest == 1 && (int)FightEventType.Gangbang == 3 && (int)FightEventType.Event == 6,
            "serialized combat enum values retained");
    }

    static void CheckBossRewards()
    {
        foreach (string level in new[] { "easy", "normal", "hard" })
        {
            foreach (string suffix in new[] { "1", "20260927" })
            {
                CloudScript.Requests.Clear();
                int successes = 0, errors = 0;
                ExecuteCloudScriptResult received = null;
                string id = level + "_" + suffix;
                CloudScript.EventBattleProgress(id, result => { successes++; received = result; }, error => errors++);
                Check(CloudScript.Requests.Count == 1, "Boss progress is sent before reward claim");
                Check(CloudScript.Requests[0].Request.FunctionName == "checkAndUpdateEventBattleProgress", "Boss progress function name");
                Check(Parameter(CloudScript.Requests[0].Request, "eventBattleId") == id, "complete encounter ID sent unchanged");
                Check(successes == 0 && errors == 0, "reward completion waits for responses");
                CloudScript.Requests[0].Succeed(new ExecuteCloudScriptResult());
                Check(CloudScript.Requests.Count == 2, "successful progress schedules reward claim");
                Check(CloudScript.Requests[1].Request.FunctionName == "claimEventReward", "Boss reward function name");
                Check(Parameter(CloudScript.Requests[1].Request, "level") == level, "reward tier matches encounter difficulty");
                Check(successes == 0 && errors == 0, "progress alone does not report reward success");
                var reward = new ExecuteCloudScriptResult { FunctionResult = "reward" };
                CloudScript.Requests[1].Succeed(reward);
                Check(successes == 1 && errors == 0 && ReferenceEquals(received, reward), "reward result forwarded exactly once");
            }
        }

        foreach (string invalid in new[] { null, "", "easy", "unknown_20260927", "not_easy_20260927", "notnormal_1", "shard_1" })
        {
            CloudScript.Requests.Clear();
            int successes = 0, errors = 0;
            PlayFabError received = null;
            CloudScript.EventBattleProgress(invalid, result => successes++, error => { errors++; received = error; });
            Check(CloudScript.Requests.Count == 0, "invalid encounter ID never reaches the server");
            Check(successes == 0 && errors == 1 && received.Error == PlayFabErrorCode.InvalidParams,
                "invalid encounter reports a validation failure");
        }
        CloudScript.Requests.Clear();
        CloudScript.EventBattleProgress(null, result => { throw new Exception("Invalid encounter reported success"); });
        Check(CloudScript.Requests.Count == 0, "optional error callback remains compatible");

        foreach (int failedRequest in new[] { 0, 1 })
        {
            CloudScript.Requests.Clear();
            int successes = 0, errors = 0;
            PlayFabError received = null;
            CloudScript.EventBattleProgress("hard_20260927", result => successes++, error => { errors++; received = error; });
            if (failedRequest == 1) CloudScript.Requests[0].Succeed(new ExecuteCloudScriptResult());
            var failure = new PlayFabError { Error = PlayFabErrorCode.CloudScriptAPIRequestError, ErrorMessage = "controlled failure" };
            CloudScript.Requests[failedRequest].Fail(failure);
            Check(successes == 0 && errors == 1 && ReferenceEquals(received, failure),
                "progress and reward failures reach the caller without a success callback");
            Check(CloudScript.Requests.Count == failedRequest + 1, "failed request does not schedule further reward calls");
        }
    }

    static string Parameter(ExecuteCloudScriptRequest request, string key)
    {
        var json = PlayFab.Json.PlayFabSimpleJson.SerializeObject(request.FunctionParameter);
        var data = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<Dictionary<string, object>>(json);
        Check(data.Count == 1, "Boss request sends only the expected contract field");
        return (string)data[key];
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + message);
        checks++;
    }

    sealed class MenuProcess : MSceneProcess
    {
        internal bool Ready = true;
        internal int Entered, TypedEntered, Ended;
        internal MenuProcess(MainSceneStep step) { Step = step; }
        public override bool CanEnterOtherProcess() => Ready;
        public override void ProcessEnter() { Entered++; }
        public override void ProcessEnter<T>(T value) { TypedEntered++; }
        public override void ProcessEnd() { Ended++; }
    }
}

public partial class CloudScript
{
    internal static readonly List<PendingRequest> Requests = new List<PendingRequest>();

    public static void ExecuteCloudScriptMainSceneCommon(ExecuteCloudScriptRequest request,
        Action<ExecuteCloudScriptResult> success, Action<PlayFabError> error = null,
        object customData = null, Dictionary<string, string> extraHeaders = null,
        bool showLoading = true, bool showErrorPopup = true)
    {
        Requests.Add(new PendingRequest { Request = request, Success = success, Error = error });
    }

    internal sealed class PendingRequest
    {
        internal ExecuteCloudScriptRequest Request;
        internal Action<ExecuteCloudScriptResult> Success;
        internal Action<PlayFabError> Error;
        bool completed;
        internal void Succeed(ExecuteCloudScriptResult result)
        {
            if (completed) throw new InvalidOperationException("Request completed twice");
            completed = true;
            Success?.Invoke(result);
        }
        internal void Fail(PlayFabError error)
        {
            if (completed) throw new InvalidOperationException("Request completed twice");
            completed = true;
            Error?.Invoke(error);
        }
    }
}

namespace UniRx
{
    public sealed class ReactiveProperty<T> { public T Value; }
}

namespace UnityEngine
{
    public static class Debug { public static void Log(object value) { } }
}

public sealed class Any { }
public sealed class MissionWatcher { }
public sealed class MainSceneLog { public MainSceneStep step; public string description; }
public static class MainSceneLogger { public static readonly List<MainSceneLog> Logs = new List<MainSceneLog>(); }
public static class DicAdd<K, V>
{
    public static void Add(IDictionary<K, V> dictionary, K key, V value) { dictionary[key] = value; }
}
