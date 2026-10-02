using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using FightScene;
using MCombat.Shared.Combat;
using UnityEngine;

public static partial class PocketStrikerBattleCameraSmoke
{
    [Serializable] public sealed class OpeningReport
    {
        public bool complete, passed;
        public int width, height, measuredFrames, fullModelCorners, clippedCorners;
        public string utcTime, unityVersion;
        public string scope = "Actual FightScene, local Self account, authored haruka/baruk/earth-golem animated models, production countdown, native reserve pointer switch, enemy switch, death replacement and in-scene retry. Six-unit MultiRaid and 200-unit Group countdowns are included. Every recorded full-model corner and pixel-space team axis is checked. Explicit separated/airborne/landed placements isolate framing from combat. Countdown retains its diagonal; the separated fighting duel must settle to a horizontal ground axis while height-only launches/landings leave yaw stable.";
        public string limitation = "Editor Play mode with controlled placements and invulnerability. No device touch/performance certification; the airborne case checks camera framing, while actual skill145 impact/landing mechanics are validated separately.";
        public List<OpeningSample> samples = new List<OpeningSample>();
        public List<string> errors = new List<string>();
    }

    [Serializable] public sealed class OpeningSample
    {
        public string name;
        public int frames, minimumFielded, clippedCorners;
        public bool requiresDiagonal, requiresHorizontal;
        public int horizontalFrames;
        public float maximumSettledAxisAngle;
        public float minimumAxisAngle = float.MaxValue, maximumAxisAngle = float.MinValue;
        public float minimumDistance = float.MaxValue, maximumDistance, maximumYawSpeed, maximumWallYawSpeed;
        public Vector3 firstPlayerViewport, firstOpponentViewport;
        public List<string> screenshots = new List<string>();
    }

    static OpeningReport opening;

    static async UniTask ReviewOpening(UnitInfo leader)
    {
        opening = new OpeningReport { width = Screen.width, height = Screen.height,
            unityVersion = Application.unityVersion, utcTime = DateTime.UtcNow.ToString("O") };
        report.scope = opening.scope;
        try
        {
            UnityEngine.Random.InitState(924);
            var mixed = OpeningFight(leader, TeamMode.Rotation);
            FightLoad.Go(mixed);
            await OpeningStart("mixed-rotation", 3, false);
            var manager = RTFightManager.Target;
            ClickPortrait(manager.team1.teamMembers.Get(0, 1));
            await MeasureOpening("native-player-reserve", 2, false);
            HoldTeams();
            manager.team2.ReadyForNextMember(manager.team2.teamMembers.Get(0, 2));
            await MeasureOpening("native-enemy-reserve", 2, false);
            HoldTeams();
            manager.team1.RMode_Unit.Value._MyBehaviorRunner.ChangeState("Death");
            await MeasureOpening("death-replacement", 5, false);
            Require(manager.team1.RMode_Unit.Value != null && !manager.team1.RMode_Unit.Value.FightDataRef.IsDead.Value,
                "Actual death did not activate a living replacement.");
            FightLoad.Go(FightLoad.Fight, true); await UniTask.NextFrame();
            await OpeningStart("rotation-retry", 3, false);
            HoldTeams();
            var battleCamera = manager._CameraManager.CurrentBattleCamera;
            float yawOffset = BattleCameraOpening.CalculatePlanarYaw(Vector3.zero, Vector3.forward,
                BattleCameraProfiles.Duel, CameraManager._camera.aspect, battleCamera.GetUsableViewport(CameraManager._camera));
            var axis = Quaternion.Euler(0, CameraManager._camera.transform.eulerAngles.y - yawOffset, 0) * Vector3.forward;
            PlaceOpeningDuel(axis, 4, 0); await MeasureOpening("separated-ground", 7, false, horizontal: true);
            float separatedYaw = report.transitions.Last().frames.Last().yaw;
            await MeasureOpening("separated-airborne", 2, false, 5, () => PlaceOpeningDuel(axis, 4, 6));
            var air = report.transitions.Last();
            Require(air.frames.All(f => Mathf.Abs(Mathf.DeltaAngle(separatedYaw, f.yaw)) < 9),
                "Height-only launch at orbiting distance rotates the arena.");
            PlaceOpeningDuel(axis, 4, 0); await MeasureOpening("separated-landed", 3, false);
            Require(report.transitions.Last().frames.All(f => Mathf.Abs(Mathf.DeltaAngle(separatedYaw, f.yaw)) < 9),
                "Height-only landing at orbiting distance rotates the arena.");
            FightLoad.Go(OpeningFight(leader, TeamMode.MultiRaid));
            await OpeningStart("mixed-multiraid-six", 3, false);
            FightLoad.Go(CreateGroup(leader));
            await OpeningStart("group-two-hundred", 100, true);
            opening.complete = true;
            opening.passed = opening.errors.Count == 0 && opening.samples.Count >= 12
                && opening.measuredFrames >= 60 && opening.clippedCorners == 0;
            Require(opening.passed, "Opening review assertions failed; inspect opening-report.json.");
        }
        catch (Exception error)
        {
            opening.errors.Add(error.ToString());
            throw;
        }
        finally { File.WriteAllText(Path.Combine(Output, "opening-report.json"), JsonUtility.ToJson(opening, true)); }
    }

    static void PlaceOpeningDuel(Vector3 axis, float halfSeparation, float lift)
    {
        var manager = RTFightManager.Target;
        foreach (var team in new[] { manager.team1, manager.team2 })
        {
            var unit = team.RMode_Unit.Value;
            if (unit == null) continue;
            float side = team.teamConfig.myTeam == RTFightManager.playerTeam ? -1 : 1;
            var body = unit._BasicPhysicSupport.Rigidbody;
            CombatPlacementUtility.PlaceRootByGeometryCenter(unit.WholeT, body, unit.geometryCenter,
                axis * (side * halfSeparation) + Vector3.up * (1.2f + (side > 0 ? lift : 0)),
                Quaternion.LookRotation(-axis * side, Vector3.up));
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        }
        Physics.SyncTransforms();
    }

    static FightInfo OpeningFight(UnitInfo leader, TeamMode mode)
    {
        var fight = CreateOrdinary(leader, mode);
        var ids = new[] { "3", "15", "8" };
        for (int index = 0; index < ids.Length; index++)
        {
            Require(Units.GetUnitConfig(ids[index])?.TYPE == Units.GetUnitConfig(leader.r_id)?.TYPE,
                "Opening mixed models must use the authored compatible animation type.");
            fight.FightMembers.HeroSets.Get(0, index).r_id = ids[index];
            fight.FightMembers.EnemySets.Get(0, index).r_id = ids[ids.Length - index - 1];
        }
        return fight;
    }

    static async UniTask OpeningStart(string name, int count, bool group)
    {
        await UniTask.WaitUntil(() => IsLoaded(count, count, group)
            && FSceneProcessesRunner.Main.currentProcess is CountDownProcess).Timeout(TimeSpan.FromSeconds(180));
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        await CheckCountDownStart(name, group, count, count);
        HoldTeams();
        await MeasureOpening(name + "-countdown", 1.5f, true);
        await UniTask.WaitUntil(() => FSceneProcessesRunner.Main.currentProcess is FightingProcess)
            .Timeout(TimeSpan.FromSeconds(15));
        RTFightManager.Target.team1.TurnAllUnitsInvincible(true);
        RTFightManager.Target.team2.TurnAllUnitsInvincible(true);
        HoldTeams();
        await MeasureOpening(name + "-fight-start", .5f,
            RTFightManager.Target._CameraManager.CurrentBattleCamera is not DuelBattleCamera, 6);
    }

    static async UniTask MeasureOpening(string name, float seconds, bool diagonal, float angleTolerance = 5,
        Action placement = null, bool horizontal = false)
    {
        await RecordTransition(name, seconds, placement);
        var transition = report.transitions.Last();
        var sample = new OpeningSample { name = name, frames = transition.frames.Count,
            minimumFielded = transition.frames.Min(f => f.fielded), requiresDiagonal = diagonal,
            requiresHorizontal = horizontal,
            screenshots = transition.screenshots.ToList() };
        opening.samples.Add(sample);
        var playerPrefix = RTFightManager.playerTeam + "/";
        foreach (var frame in transition.frames)
        {
            opening.measuredFrames++;
            opening.fullModelCorners += frame.fighters.Count * 8;
            opening.clippedCorners += frame.clippedCorners;
            sample.clippedCorners += frame.clippedCorners;
            sample.minimumDistance = Mathf.Min(sample.minimumDistance, frame.distance);
            sample.maximumDistance = Mathf.Max(sample.maximumDistance, frame.distance);
            sample.maximumYawSpeed = Mathf.Max(sample.maximumYawSpeed, frame.yawSpeed);
            sample.maximumWallYawSpeed = Mathf.Max(sample.maximumWallYawSpeed, frame.yawWallSpeed);
            var players = frame.fighters.Where(f => f.id.StartsWith(playerPrefix)).ToArray();
            var enemies = frame.fighters.Where(f => !f.id.StartsWith(playerPrefix)).ToArray();
            if (players.Length == 0 || enemies.Length == 0) continue;
            var player = players.Aggregate(Vector3.zero, (sum, f) => sum + f.geometry) / players.Length;
            var enemy = enemies.Aggregate(Vector3.zero, (sum, f) => sum + f.geometry) / enemies.Length;
            var rotation = Quaternion.Euler(frame.pitch, frame.yaw, 0);
            Vector3 Viewport(Vector3 position)
            {
                var local = Quaternion.Inverse(rotation) * (position - frame.cameraPosition);
                float tan = Mathf.Tan(frame.fieldOfView * .5f * Mathf.Deg2Rad);
                return new Vector3(.5f + local.x / (2 * tan * ((float)opening.width / opening.height) * local.z),
                    .5f + local.y / (2 * tan * local.z), local.z);
            }
            var pv = Viewport(player); var ev = Viewport(enemy);
            if (sample.firstPlayerViewport == Vector3.zero)
            { sample.firstPlayerViewport = pv; sample.firstOpponentViewport = ev; }
            float angle = Mathf.Atan2(ev.y - pv.y, (pv.x - ev.x) * opening.width / opening.height) * Mathf.Rad2Deg;
            sample.minimumAxisAngle = Mathf.Min(sample.minimumAxisAngle, angle);
            sample.maximumAxisAngle = Mathf.Max(sample.maximumAxisAngle, angle);
            if (diagonal && (pv.x <= ev.x || pv.y >= ev.y || Mathf.Abs(angle - 45) > angleTolerance))
            {
                opening.errors.Add(name + ": frame " + frame.frame + " player must be lower-right, enemy upper-left; pixel angle=" + angle);
                break;
            }
            if (horizontal && frame.elapsed >= 5)
            {
                // Different authored body heights need not share a geometry
                // center. The automatic orbit aligns their ground positions.
                var playerGround = players.Aggregate(Vector3.zero, (sum, f) => sum + f.root) / players.Length;
                var enemyGround = enemies.Aggregate(Vector3.zero, (sum, f) => sum + f.root) / enemies.Length;
                playerGround.y = enemyGround.y = 0;
                var pg = Viewport(playerGround); var eg = Viewport(enemyGround);
                float groundAngle = Mathf.Atan2(eg.y - pg.y, (pg.x - eg.x) * opening.width / opening.height) * Mathf.Rad2Deg;
                sample.horizontalFrames++;
                sample.maximumSettledAxisAngle = Mathf.Max(sample.maximumSettledAxisAngle, Mathf.Abs(groundAngle));
                if (pg.x <= eg.x || Mathf.Abs(groundAngle) > angleTolerance)
                {
                    opening.errors.Add(name + ": settled automatic duel ground axis must be horizontal; pixel angle=" + groundAngle);
                    break;
                }
            }
        }
        if (horizontal && sample.horizontalFrames < 5) opening.errors.Add(name + ": no settled horizontal frames measured.");
        if (sample.clippedCorners > 0) opening.errors.Add(name + ": " + sample.clippedCorners + " full-model corners clipped.");
        if (sample.maximumYawSpeed > 21) opening.errors.Add(name + ": automatic camera exceeded stable orbit speed.");
        File.WriteAllText(Path.Combine(Output, "opening-report.json"), JsonUtility.ToJson(opening, true));
        Require(opening.errors.Count == 0, "Opening camera assertions failed in " + name + ".");
    }
}
