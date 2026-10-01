using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using FightScene;
using UnityEngine;

public static partial class PocketStrikerBattleCameraSmoke
{
    static async UniTask ReviewHandoffs(UnitInfo leader)
    {
        report.scope = "Live FightScene camera continuity review: full countdown, ordinary native reserve switches, production death/replacement, last-opponent death and retry. A temporary HUD hide and lost-target interval are explicit integration simulations; placements and death triggers are controlled, using actual models and callbacks. Per-frame poses, process, body projections and screenshots are retained.";
        UnityEngine.Random.InitState(731);
        FightLoad.Go(CreateOrdinary(leader, TeamMode.Rotation));
        await HandoffStart(3, "rotation");
        HoldTeams(); PlaceDuel(2, 0); await RecordTransition("handoff-settle", 2);
        var manager = RTFightManager.Target;
        var next = manager.team1.teamMembers.mDict.Values.First(u => u != manager.team1.RMode_Unit.Value);
        ClickPortrait(next);
        await RecordTransition("handoff-native-switch", 3);
        HoldTeams(); PlaceDuel(2, 0); await RecordTransition("handoff-before-hud", 2);
        var hud = GetHUD(); hud.gameObject.SetActive(false);
        await RecordTransition("handoff-hidden-hud", .5f);
        hud.gameObject.SetActive(true);
        await RecordTransition("handoff-returned-hud", .5f);
        var retained = manager.team1.RMode_Unit.Value;
        manager.team1.RMode_Unit.Value = null;
        await RecordTransition("handoff-target-gap", .5f);
        manager.team1.RMode_Unit.Value = retained;
        await RecordTransition("handoff-target-return", 1);
        retained._MyBehaviorRunner.ChangeState("Death");
        await RecordTransition("handoff-player-death", 5);
        Require(retained.FightDataRef.IsDead.Value && manager.team1.RMode_Unit.Value != retained,
            "Production player death did not replace the fighter.");
        HoldTeams(); PlaceDuel(2, 0); await RecordTransition("handoff-before-enemy-death", 2);
        var enemy = manager.team2.RMode_Unit.Value;
        enemy._MyBehaviorRunner.ChangeState("Death");
        await RecordTransition("handoff-enemy-death", 5);
        Require(enemy.FightDataRef.IsDead.Value && manager.team2.RMode_Unit.Value != enemy,
            "Production enemy death did not replace the fighter.");
        FightLoad.Go(FightLoad.Fight, true); await UniTask.NextFrame();
        await HandoffStart(3, "retry");
        Require(manager.team1.teamMembers.mDict.Values.All(u => !u.FightDataRef.IsDead.Value),
            "Retry retained a dead fighter.");
        FightLoad.Go(CreateOrdinary(leader, TeamMode.Rotation, 1));
        await HandoffStart(1, "duel");
        HoldTeams(); PlaceDuel(2, 0); await RecordTransition("handoff-before-final-death", 2);
        RTFightManager.Target.team2.RMode_Unit.Value._MyBehaviorRunner.ChangeState("Death");
        // Production result animation switches camera ownership after its hold.
        // Sample the first second while the battle camera still owns the view.
        await RecordTransition("handoff-final-death", 1);
        ValidateHandoffs();
    }

    static async UniTask HandoffStart(int count, string name)
    {
        await UniTask.WaitUntil(() => IsLoaded(count, count, false)
            && FSceneProcessesRunner.Main.currentProcess is CountDownProcess).Timeout(TimeSpan.FromSeconds(180));
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        await CheckCountDownStart("handoff-" + name, false, count, count);
        await RecordTransition("handoff-" + name + "-countdown", 3);
        await UniTask.WaitUntil(() => FSceneProcessesRunner.Main.currentProcess is FightingProcess)
            .Timeout(TimeSpan.FromSeconds(15));
        RTFightManager.Target.team1.TurnAllUnitsInvincible(true);
        RTFightManager.Target.team2.TurnAllUnitsInvincible(true);
    }

    static void ValidateHandoffs()
    {
        foreach (var item in report.transitions)
        {
            if (item.frames.Count < 5 || item.frames.Any(f => f.clippedCorners != 0))
                report.errors.Add(item.name + " clipped a live model or sampled too few frames.");
            if (item.name.EndsWith("-countdown") && item.frames.Any(f => Mathf.Abs(f.projectedCenter.x - .5f) > .025f))
                report.errors.Add(item.name + " biases the duel midpoint horizontally.");
        }
        var previous = report.transitions.Single(t => t.name == "handoff-before-hud").frames.Last();
        foreach (var name in new[] { "handoff-hidden-hud", "handoff-returned-hud" })
        {
            var first = report.transitions.Single(t => t.name == name).frames.First();
            if (Vector3.Distance(first.cameraPosition, previous.cameraPosition) > .15f)
                report.errors.Add(name + " jumps when only HUD visibility changes.");
            previous = report.transitions.Single(t => t.name == name).frames.Last();
        }
        var final = report.transitions.Single(t => t.name == "handoff-final-death");
        previous = report.transitions.Single(t => t.name == "handoff-before-final-death").frames.Last();
        if (final.frames.Any(f => Vector3.Distance(f.center, previous.center) > .1f
            || Mathf.Abs(f.distance - previous.distance) > .25f))
            report.errors.Add("Last-opponent death abandons the established duel composition before result ownership.");
        foreach (var name in new[] { "handoff-player-death", "handoff-enemy-death" })
        {
            var item = report.transitions.Single(t => t.name == name);
            var start = item.frames.First();
            if (item.frames.Where(f => f.elapsed < 1.8f).Any(f => Vector3.Distance(f.center, start.center) > .1f))
                report.errors.Add(name + " loses the fallen fighter during its replacement delay.");
            if (item.frames.Any(f => f.yawSpeed > 21))
                report.errors.Add(name + " turns too quickly during replacement.");
        }
        Require(report.errors.Count == 0, "Handoff continuity assertions failed; see recorded frames and errors.");
    }
}
