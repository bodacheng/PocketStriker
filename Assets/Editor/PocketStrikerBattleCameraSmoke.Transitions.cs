using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using FightScene;
using MCombat.Shared.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static partial class PocketStrikerBattleCameraSmoke
{
    [Serializable] public sealed class TransitionCase
    {
        public string name;
        public List<CameraFrame> frames = new List<CameraFrame>();
        public List<string> screenshots = new List<string>();
    }

    [Serializable] public sealed class CameraFrame
    {
        public int frame;
        public float elapsed, distance, required, centerLag, pitch, fieldOfView;
        public Vector3 cameraPosition, center, projectedCenter;
        public float yaw, yawSpeed;
        public string process;
        public Rect usable;
        public int fielded, clippedCorners;
        public bool holdingReplacement;
        public List<FighterFrame> fighters = new List<FighterFrame>();
    }

    [Serializable] public sealed class FighterFrame
    {
        public string id;
        public Vector3 root, geometry, boundsCenter, boundsSize;
        public Rect projected;
    }

    static async UniTask ReviewTransitions(UnitInfo leader)
    {
        report.scope = "Actual FightScene, Addressables Fast Mode, local Self battle and native production rotation-switch, death replacement and retry callbacks. Per-frame camera and renderer bounds recorded; close/separated/airborne placement is controlled by the fixture. The initial natural segment retains actual battle motion.";
        report.limitation += " No separate in-match resurrection action exists in the inspected game flow; retry reinitializes dead fighters. Controlled placements are camera-envelope cases, not proof of jump/knockback combat mechanics.";
        UnityEngine.Random.InitState(731);
        var fight = CreateOrdinary(leader, TeamMode.Rotation);
        FightLoad.Go(fight);
        await WaitReady(3, 3, false, "rotation-start");
        await RecordTransition("natural-opening", 4);
        HoldTeams(); PlaceDuel(2, 0);
        await RecordTransition("close-before-switch", 5);
        var manager = RTFightManager.Target;
        var next = manager.team1.teamMembers.mDict.Values.First(u => u != manager.team1.RMode_Unit.Value);
        ClickPortrait(next);
        await RecordTransition("player-reserve-switch", 4);
        Require(manager.team1.RMode_Unit.Value == next, "Production reserve switch did not occur.");
        next = manager.team2.teamMembers.mDict.Values.First(u => u != manager.team2.RMode_Unit.Value);
        manager.team2.ReadyForNextMember(next);
        await RecordTransition("enemy-reserve-switch", 4);
        Require(manager.team2.RMode_Unit.Value == next, "Production enemy reserve switch did not occur.");
        PlaceDuel(9, 0);
        await RecordTransition("separated", 3);
        PlaceDuel(2, 0);
        await RecordTransition("return-to-melee", 4);
        PlaceDuel(2, 6);
        await RecordTransition("airborne-envelope", 3, () => PlaceDuel(2, 6));
        PlaceDuel(2, 0);
        await RecordTransition("landed", 4);
        var retained = manager.team1.RMode_Unit.Value;
        manager.team1.RMode_Unit.Value = null;
        await RecordTransition("temporary-target-gap", .6f);
        manager.team1.RMode_Unit.Value = retained;
        await RecordTransition("target-reacquired", 1);
        var dead = manager.team1.RMode_Unit.Value;
        dead._MyBehaviorRunner.ChangeState("Death");
        await RecordTransition("death-replacement", 5);
        Require(dead.FightDataRef.IsDead.Value && manager.team1.RMode_Unit.Value != dead,
            "Production Death did not replace the rotation fighter.");
        var previous = RTFightManager.Target;
        FightLoad.Go(FightLoad.Fight, true);
        await UniTask.NextFrame();
        await WaitReady(3, 3, false, "rotation-retry");
        Require(RTFightManager.Target == previous, "Retry replaced the scene instead of using its production reset.");
        Require(manager.team1.teamMembers.mDict.Values.All(u => !u.FightDataRef.IsDead.Value), "Retry did not reset dead fighters.");
        await RecordTransition("retry-opening", 3);
        FightLoad.Go(CreateOrdinary(leader, TeamMode.Rotation, 1));
        await WaitReady(1, 1, false, "duel-reload");
        await RecordTransition("duel-reload-opening", 3);
        var mixed = CreateOrdinary(leader, TeamMode.Rotation);
        var ids = new[] { "3", "15", "8" }; // authored haruka, baruk and earth_golem; all human animation type
        for (int index = 0; index < ids.Length; index++)
        {
            Require(Units.GetUnitConfig(ids[index])?.TYPE == Units.GetUnitConfig(leader.r_id)?.TYPE,
                "Mixed-model fixture must use the authored compatible animation type.");
            mixed.FightMembers.HeroSets.Get(0, index).r_id = ids[index];
            mixed.FightMembers.EnemySets.Get(0, index).r_id = ids[ids.Length - 1 - index];
        }
        FightLoad.Go(mixed); await WaitReady(3, 3, false, "mixed-model-start");
        HoldTeams(); PlaceDuel(2, 0); await RecordTransition("mixed-model-close", 3);
        manager = RTFightManager.Target;
        next = manager.team1.teamMembers.Get(0, 1); ClickPortrait(next);
        await RecordTransition("mixed-model-switch", 3);
        Require(manager.team1.RMode_Unit.Value == next, "Mixed-model portrait input did not switch fighter.");
        next = manager.team1.teamMembers.Get(0, 2); ClickPortrait(next);
        await RecordTransition("large-model-switch", 3);
        Require(manager.team1.RMode_Unit.Value == next, "Large-model portrait input did not switch fighter.");
        ValidateTransitionObservations();
    }

    static void ClickPortrait(Data_Center target)
    {
        var button = GetHUD().Team1UI.UnitIconDic[target].Icon.iconButton;
        Require(button.IsInteractable() && button.gameObject.activeInHierarchy && !BOButton.AnyProcess,
            "Native portrait is not ready for switching.");
        var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
        var rect = (RectTransform)button.transform;
        var point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),
            "Native portrait pointer is blocked by another UI element.");
        pointer.pointerPressRaycast = hits[0]; pointer.pointerCurrentRaycast = hits[0];
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    static void ValidateTransitionObservations()
    {
        Require(report.transitions.Count == 16 && report.starts.Count == 4, "Transition review did not finish all scenarios.");
        foreach (var item in report.transitions)
        {
            Require(item.frames.Count > 5 && item.frames.All(frame => frame.clippedCorners == 0),
                item.name + " clipped a fielded fighter or did not sample enough live frames.");
            Require(item.frames.All(frame => !float.IsNaN(frame.distance) && !float.IsInfinity(frame.distance)
                && frame.distance > 0), item.name + " produced an invalid camera pose.");
        }
        var close = report.transitions.Single(item => item.name == "return-to-melee");
        Require(close.frames.Where(frame => frame.elapsed >= 2).All(frame => frame.distance <= frame.required * 1.1f + .2f),
            "Duel remains unnecessarily distant two seconds after returning to melee.");
        var death = report.transitions.Single(item => item.name == "death-replacement");
        float initial = death.frames[0].distance;
        Require(death.frames.Any(frame => frame.holdingReplacement && frame.fielded == 1)
            && death.frames.Any(frame => !frame.holdingReplacement && frame.fielded == 2),
            "Death/replacement did not exercise the partial-roster hold and release.");
        Require(death.frames.Max(frame => frame.distance) < initial * 1.2f + .5f
            && death.frames.Min(frame => frame.distance) > initial * .85f,
            "Replacement gap still causes an inward/outward camera swing.");
        var gap = report.transitions.Single(item => item.name == "temporary-target-gap");
        Require(gap.frames.All(frame => frame.holdingReplacement), "Temporary null fighter lost the established duel composition.");
        var recovered = report.transitions.Single(item => item.name == "target-reacquired");
        Require(recovered.frames.All(frame => !frame.holdingReplacement && frame.fielded == 2),
            "Reacquired target did not release the temporary hold.");
        foreach (var name in new[] { "player-reserve-switch", "enemy-reserve-switch", "mixed-model-switch", "large-model-switch" })
        {
            var change = report.transitions.Single(item => item.name == name);
            Require(change.frames.All(frame => frame.fielded == 2 && frame.distance < frame.required * 1.3f + .5f),
                name + " inherited unnecessary distance while replacing a visible fighter.");
        }
    }

    static void HoldTeams()
    {
        var manager = RTFightManager.Target;
        manager.team1.Auto = false; manager.team2.Auto = false;
        foreach (var team in new[] { manager.team1, manager.team2 })
        foreach (var unit in team.teamMembers.mDict.Values)
        {
            unit._MyBehaviorRunner.AI = false;
            unit._MyBehaviorRunner.ChangeState("Empty");
        }
    }

    static void PlaceDuel(float halfSeparation, float lift)
    {
        var manager = RTFightManager.Target;
        int side = -1;
        foreach (var team in new[] { manager.team1, manager.team2 })
        {
            var unit = team.RMode_Unit.Value;
            if (unit == null) continue;
            var body = unit._BasicPhysicSupport.Rigidbody;
            CombatPlacementUtility.PlaceRootByGeometryCenter(unit.WholeT, body, unit.geometryCenter,
                new Vector3(side * halfSeparation, 1.2f + (side == 1 ? lift : 0), 0),
                Quaternion.Euler(0, side == -1 ? 90 : -90, 0));
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            side = 1;
        }
        Physics.SyncTransforms();
    }

    static async UniTask RecordTransition(string name, float seconds, Action placement = null)
    {
        var item = new TransitionCase { name = name }; report.transitions.Add(item);
        float begin = Time.realtimeSinceStartup;
        var captures = new[] { 0f, .15f, .5f, 1.5f, seconds - .2f };
        if (name == "death-replacement") captures = new[] { 0f, .5f, 1.5f, 2.05f, 2.3f, 3f, seconds - .2f };
        int capture = 0;
        while (Time.realtimeSinceStartup - begin < seconds)
        {
            placement?.Invoke();
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            var camera = CameraManager._camera;
            var manager = RTFightManager.Target;
            var mode = manager._CameraManager.CurrentBattleCamera;
            Require(mode != null && mode.IsFramingInitialized, name + " lost its battle camera.");
            var frame = new CameraFrame { frame = Time.frameCount, elapsed = Time.realtimeSinceStartup - begin,
                cameraPosition = camera.transform.position, center = mode.CurrentPose.Center,
                distance = mode.CurrentPose.Distance, required = mode.DesiredPose.Distance,
                centerLag = Vector3.Distance(mode.CurrentPose.Center, mode.DesiredPose.Center),
                pitch = camera.transform.eulerAngles.x, fieldOfView = camera.fieldOfView, fielded = mode.FramedUnitCount,
                holdingReplacement = mode.IsHoldingReplacementFraming };
            var usable = mode.GetUsableViewport(camera);
            frame.usable = usable;
            frame.projectedCenter = camera.WorldToViewportPoint(mode.CurrentPose.Center);
            frame.yaw = camera.transform.eulerAngles.y;
            frame.process = FSceneProcessesRunner.Main.currentProcess?.GetType().Name;
            if (item.frames.Count > 0)
            {
                var last = item.frames[item.frames.Count - 1];
                frame.yawSpeed = Mathf.Abs(Mathf.DeltaAngle(last.yaw, frame.yaw)) / Mathf.Max(.001f, frame.elapsed-last.elapsed);
            }
            foreach (var team in new[] { manager.team1, manager.team2 })
            foreach (var unit in team.teamMembers.mDict.Values)
            {
                if (!Fielded(team, unit)) continue;
                if (!BattleCameraFraming.TryGetModelBounds(unit.WholeT, out var bounds)) continue;
                var fighter = new FighterFrame { id = team.teamConfig.myTeam + "/" + unit.UnitInfo.id,
                    root = unit.WholeT.position, geometry = unit.geometryCenter.position,
                    boundsCenter = bounds.center, boundsSize = bounds.size };
                var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    var vp = camera.WorldToViewportPoint(point);
                    min = Vector2.Min(min, vp); max = Vector2.Max(max, vp);
                    if (vp.z <= camera.nearClipPlane || vp.x < usable.xMin - .002f || vp.x > usable.xMax + .002f
                        || vp.y < usable.yMin - .002f || vp.y > usable.yMax + .002f) frame.clippedCorners++;
                }
                fighter.projected = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                frame.fighters.Add(fighter);
            }
            item.frames.Add(frame);
            if (capture < captures.Length && frame.elapsed >= captures[capture])
            {
                var path = Path.GetFullPath(Path.Combine(Output, name + "-" + capture + ".png"));
                ScreenCapture.CaptureScreenshot(path); item.screenshots.Add(path); capture++;
            }
        }
        Debug.Log("[CameraTransition] " + name + ": frames=" + item.frames.Count
            + ", distance=" + item.frames.Min(f => f.distance) + ".." + item.frames.Max(f => f.distance)
            + ", required=" + item.frames.Min(f => f.required) + ".." + item.frames.Max(f => f.required)
            + ", clipped=" + item.frames.Sum(f => f.clippedCorners));
        File.WriteAllText(Path.Combine(Output, "transitions-progress.json"), JsonUtility.ToJson(report, true));
    }
}
