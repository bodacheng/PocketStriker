using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using FightScene;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class PocketStrikerCombatFlowSmoke
{
    [MenuItem("PocketStriker/Validation/Group Stage Loading Playmode Smoke")]
    public static void StartGroupStageLoadingBatch()
    {
        SessionState.SetBool(Key + ".TutorialBalance", false);
        SessionState.SetBool(Key + ".EvolutionHeal", false);
        SessionState.SetBool(Key + ".StoryFailures", false);
        Begin(groupStageLoading: true);
    }

    static async UniTask ValidateGroupStageLoading()
    {
        report.scope = "Published Quest stages 30 and 36 through ArcadeModeManager.LoadStage and FightLoad.Go: actual Addressables maps, authored enemy models/skills, 12/24/48 fighters per team, countdown, live combat and 48-unit in-scene retries. Every fresh visit loads a new FightScene.";
        report.limitation = "Editor Play mode with local resources and a completed fixture account, mirrored legal player loadouts and an empty story provider. No account, reward, ad or story network requests; does not validate published remote bundles or physical-device performance.";
        var stages = new ArcadeModeManager();
        await stages.Initialize().Timeout(TimeSpan.FromSeconds(30));
        foreach (int number in new[] { 30, 36 })
        foreach (int limit in new[] { 12, 24, 48 })
        {
            var stage = await stages.LoadStage(number).Timeout(TimeSpan.FromSeconds(30)) as GangbangInfo;
            Require(stage != null && stage.IsGroupBattle && stage.EventType == FightEventType.Quest,
                "Published stage is not a Quest Group battle: " + number);
            try
            {
                stage.Team1ID = Account;
                stage.Team2ID = Account + "-enemy";
                stage.FightMembers.HeroSets = new MultiDic<int, int, UnitInfo>();
                for (int index = 0; index < stage.UnitsData.Count; index++)
                {
                    var unit = stage.UnitsData[index].DeepCopy();
                    unit.id = "fixture-player-" + index;
                    stage.FightMembers.HeroSets.Set(0, index, unit);
                }
                stage.ApplyTeamLimit(limit);
                stage.ConvertTeamToGangbang();
                await CheckGroupStageOpening(stage, limit, false);
                if (limit == 48)
                    await CheckGroupStageOpening(FightLoad.Fight, limit, true);
            }
            finally { UnityEngine.Object.Destroy(stage); }
        }
        Require(report.groupStageLoadingChecks.Count == 8, "Missing Group stage loading cases.");
    }

    static async UniTask CheckGroupStageOpening(FightInfo stage, int limit, bool retry)
    {
        var name = "stage-" + stage.ID + "-" + limit + (retry ? "-retry" : "-entry");
        var previousManager = RTFightManager.Target;
        bool sawPreparing = false, sawCountdown = false;
        float started = Time.realtimeSinceStartup;
        FightLoad.Go(stage, retry);
        while (true)
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            Require(Time.realtimeSinceStartup - started < 120, name + " loading timed out. " + Diagnostic());
            if (SceneManager.GetActiveScene().name != "FightScene")
            {
                Require(!sawPreparing, name + " left FightScene during preparation.");
                continue;
            }
            var manager = RTFightManager.Target;
            if (manager == null || (!retry && manager == previousManager)) continue;
            var process = FSceneProcessesRunner.Main.currentProcess;
            sawPreparing |= process is PreparingProcess;
            sawCountdown |= process is CountDownProcess;
            if (process is not FightingProcess) continue;
            Require(sawPreparing && sawCountdown, name + " skipped preparation or countdown.");
            Require(manager.team1.teamMembers.Count == limit && manager.team2.teamMembers.Count == limit,
                name + " loaded incorrect team sizes.");
            Require(manager.team1.IfAllUnitsPreparedForBattle() && manager.team2.IfAllUnitsPreparedForBattle()
                && manager.team1.Auto && manager.team2.Auto, name + " did not initialize both automatic teams.");
            var ground = BoundaryControlByGod.target.CurrentBattleGround;
            Require(ground != null && ground.GetComponent<BattleGround>() != null,
                name + " entered battle without its map.");
            Require(FightLoad.Fight.battleGroundID == stage.battleGroundID, name + " lost its authored map ID.");
            await UniTask.Delay(TimeSpan.FromSeconds(2), DelayType.Realtime);
            Require(SceneManager.GetActiveScene().name == "FightScene"
                && FSceneProcessesRunner.Main.currentProcess is FightingProcess,
                name + " did not remain in live combat.");
            Require(manager.team1.teamMembers.GetValues().Concat(manager.team2.teamMembers.GetValues())
                .All(unit => unit != null && unit.WholeT != null), name + " lost a loaded model.");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output, name + ".png"));
            await UniTask.NextFrame();
            report.groupStageLoadingChecks.Add(name + ": map=" + ground.name + ", teams=" + limit + "/" + limit);
            Debug.Log("[GroupStageLoading] PASS " + name);
            return;
        }
    }
}
