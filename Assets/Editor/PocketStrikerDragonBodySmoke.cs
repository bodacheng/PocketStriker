using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using FightScene;
using HittingDetection;
using mainMenu;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class PocketStrikerCombatFlowSmoke
{
    [Serializable]
    public sealed class DragonBodySize
    {
        public string unit;
        public float runtimeRadius, runtimeHeight, referenceTorsoRadius, torsoContactRadius;
        public Vector2 referenceTorsoSize;
        public Vector3 runtimeCenterOffset;
        public int hurtTriggers;
        public bool enabledSolidBody;
    }

    [Serializable]
    public sealed class DragonBodyContact
    {
        public string name;
        public float dragonColliderRadius, humanColliderRadius, rootSeparation, minTorsoGap, maxPenetration;
        public int settledSamples;
        public bool continuedApproach, oldHumanRadiusBaseline;
        public string screenshot;
    }

    [Serializable]
    public sealed class DragonBodyAttack
    {
        public string unit, skill, state;
        public float startRootSeparation, triggerRangeMin, triggerRangeMax, damage;
        public bool sensorAllowsAttack;
        public int activeAttackTriggers;
    }

    [MenuItem("PocketStriker/Validation/Dragon Body Contact Playmode Smoke")]
    public static void StartDragonBodyBatch()
    {
        SessionState.SetBool(Key + ".TutorialBalance", false);
        SessionState.SetBool(Key + ".EvolutionHeal", false);
        SessionState.SetBool(Key + ".StoryFailures", false);
        Begin(dragonBody: true);
    }

    static async UniTask ValidateDragonBodyContact()
    {
        report.scope = "One actual local FightLoad 1v1 with dragon 17 and human 1. Four directions exercise idle overlap settlement and sustained 0.45 metre/second approach on both bodies, with Physics.ComputePenetration and animated PlanarTorsoGap samples. Temporary former 1.08 metre dragon-radius front and side cases supply runtime comparisons. Red element, formation body bounds and four-yaw impact following are checked. Each side then performs a native close attack from naturally settled contact.";
        report.limitation = "Editor Play mode, local fixture account, AI disabled and high HP; no remote inventory/rewards. Only initial fixture placement changes root positions; contact uses the Rigidbody solver with no scripted separation. Empty-state translation constraints are released for the contact exercise. Limb hurt volumes and active attack markers must remain triggers. Two representative close attacks check reach, not every skill or device performance.";
        var random = UnityEngine.Random.state;
        var fight = ScriptableObject.CreateInstance<FightInfo>();
        try
        {
            UnityEngine.Random.InitState(1714);
            fight.ID = "dragon-body-contact";
            fight.EventType = FightEventType.Self;
            fight.FightMode = FightMode.Rotate;
            fight.Team1ID = Account;
            fight.Team2ID = Account + "-enemy";
            fight.battleGroundID = 0;
            fight.team1CGMode = fight.team2CGMode = CriticalGaugeMode.Unlimited;
            fight.team1HpRate = fight.team2HpRate = 20;
            fight.FightMembers = new FightMembers();
            fight.FightMembers.HeroSets.Set(0, 0, DragonBodyFixtureUnit("17", "195"));
            fight.FightMembers.EnemySets.Set(0, 0, DragonBodyFixtureUnit("1", "27"));
            FightLoad.Go(fight, false);
            await UniTask.WaitUntil(() => SceneManager.GetActiveScene().name == "FightScene"
                && FightLoad.Fight?.ID == fight.ID && FSceneProcessesRunner.Main.currentProcess is FightingProcess
                && UnityEngine.Object.FindFirstObjectByType<FightingStepLayer>()?.Initialized == true)
                .Timeout(TimeSpan.FromSeconds(90));
            var manager = RTFightManager.Target;
            manager.team1.Auto = manager.team2.Auto = false;
            var dragon = manager.team1.RMode_Unit.Value;
            var human = manager.team2.RMode_Unit.Value;
            Require(dragon != null && human != null && dragon.UnitInfo.r_id == "17" && human.UnitInfo.r_id == "1",
                "Actual dragon/human models failed to load.");
            Require(Units.GetUnitConfig("17")?.element == Element.redMagic && dragon.element == Element.redMagic,
                "Body-contact fixture did not load the dragon's configured red element.");
            report.dragonChecks.Add("body-contact: actual dragon 17 and its production unit config both use redMagic");
            var dragonProfile = RecordDragonBodySize(dragon);
            var humanProfile = RecordDragonBodySize(human);
            Require(dragonProfile.Radius > 1.08f && dragonProfile.Radius > humanProfile.Radius,
                "Dragon body is still limited to the human's former 1.08 metre maximum radius.");
            Require(dragonProfile.Height >= dragonProfile.Radius * 2f - .001f,
                "Dragon profile height is smaller than its actual capsule diameter.");
            Require(humanProfile.Radius >= .88f && humanProfile.Radius <= 1.08f,
                "Dragon calibration changed the existing human contact width.");

            // The baseline is the same real models and physics solver with the
            // previous human-capped dimensions. Restore before every new-profile case.
            float oldRadius = dragonProfile.Body.radius;
            float oldHeight = dragonProfile.Body.height;
            var oldCenter = dragonProfile.Body.center;
            try
            {
                float horizontalScale = Mathf.Max(Mathf.Abs(dragon.WholeT.lossyScale.x), Mathf.Abs(dragon.WholeT.lossyScale.z));
                float verticalScale = Mathf.Abs(dragon.WholeT.lossyScale.y);
                dragonProfile.Body.radius = 1.08f / horizontalScale;
                dragonProfile.Body.height = 3.4f / verticalScale;
                dragonProfile.Body.center = new Vector3(0, 1.7f / verticalScale, 0);
                await SampleDragonBodyContact(dragon, human, Vector3.forward, "former-human-cap-front-approach", true, true);
                await SampleDragonBodyContact(dragon, human, Vector3.left, "former-human-cap-left-approach", true, true);
            }
            finally
            {
                dragonProfile.Body.radius = oldRadius;
                dragonProfile.Body.height = oldHeight;
                dragonProfile.Body.center = oldCenter;
            }
            var directions = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            var names = new[] { "front", "back", "left", "right" };
            for (int side = 0; side < directions.Length; side++)
            {
                await SampleDragonBodyContact(dragon, human, directions[side], names[side] + "-idle", false, false);
                await SampleDragonBodyContact(dragon, human, directions[side], names[side] + "-approach", true, false);
            }
            foreach (var side in new[] { "front", "left" })
            {
                var baseline = report.dragonBodyContacts.Single(item => item.name == "former-human-cap-" + side + "-approach");
                var current = report.dragonBodyContacts.Single(item => item.name == side + "-approach");
                Require(current.minTorsoGap > baseline.minTorsoGap + .05f,
                    "Wider dragon body did not improve actual " + side + " torso separation over the former human radius.");
            }
            ValidateDragonBodyFormationAndImpact(dragon, human);
            await ValidateDragonBodyAttackReach(dragon, human, "195");
            await ValidateDragonBodyAttackReach(human, dragon, "27");
            RecordDragonBodySize(dragon);
            RecordDragonBodySize(human);
            Require(report.dragonBodyContacts.Count == 10 && report.dragonBodyAttacks.Count == 2,
                "Dragon body smoke did not complete its bounded contact and attack checks.");
        }
        finally
        {
            UnityEngine.Random.state = random;
            UnityEngine.Object.Destroy(fight);
        }
    }

    static UnitInfo DragonBodyFixtureUnit(string id, string skillId)
    {
        var config = Units.GetUnitConfig(id);
        Require(config != null, "Missing body-smoke unit " + id);
        var set = SkillSet.RandomSkillSet(skillId, false, new SkillStonesBox.StoneFilterForm(config.TYPE));
        Require(set.SkillIDList().Contains(skillId), "Missing equipped close-attack skill " + skillId);
        set.SetPassive(config.DEFENDABLE_FLAG, config.MoveType, config.RushType);
        set.SortNineAndTwo();
        return new UnitInfo { id = "0", r_id = id, level = 10, set = set };
    }

    static CombatBodyProfile RecordDragonBodySize(Data_Center unit)
    {
        var profile = unit._BasicPhysicSupport.GetComponent<CombatBodyProfile>();
        Require(profile != null && profile.Body != null && profile.Body.enabled && !profile.Body.isTrigger,
            "Unit " + unit.UnitInfo.r_id + " has no enabled solid contact body.");
        var limbs = unit.WholeT.GetComponentsInChildren<BO_Limb>(true);
        Require(limbs.Length > 0 && limbs.All(limb =>
            (limb.myColliderMustEquip ?? limb.GetComponent<Collider>())?.isTrigger == true),
            "Unit " + unit.UnitInfo.r_id + " converted an animated hurt volume to a solid body.");
        // Keep the initial dimensions in the report; repeat calls enforce that
        // attack/hurt transitions preserve the solid/trigger split.
        if (!report.dragonBodySizes.Any(item => item.unit == unit.UnitInfo.r_id))
            report.dragonBodySizes.Add(new DragonBodySize
            {
                unit = unit.UnitInfo.r_id, runtimeRadius = profile.Radius, runtimeHeight = profile.Height,
                referenceTorsoRadius = profile.ReferenceTorsoRadius, referenceTorsoSize = profile.ReferenceTorsoSize,
                torsoContactRadius = profile.ContactRadiusAtWorldHeight(profile.TorsoCenter.y),
                runtimeCenterOffset = profile.Body.transform.TransformPoint(profile.Body.center) - unit.WholeT.position,
                enabledSolidBody = true, hurtTriggers = limbs.Length
            });
        return profile;
    }

    static void PrepareDragonBodyContact(Data_Center dragon, Data_Center human, Vector3 side, bool approach)
    {
        foreach (var unit in new[] { dragon, human })
        {
            unit.FightDataRef.Invincible = true;
            unit._MyBehaviorRunner.AI = false;
            unit._MyBehaviorRunner.ChangeState("Move");
            unit._MyBehaviorRunner.ChangeState("Empty");
            var body = unit._BasicPhysicSupport.Rigidbody;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.linearVelocity = body.angularVelocity = Vector3.zero;
            unit._BasicPhysicSupport.OpenEnemyTouchingDrag(1);
        }
        float sumRadius = WorldDragonBodyRadius(dragon) + WorldDragonBodyRadius(human);
        float separation = sumRadius + (approach ? .35f : -.08f);
        var dragonRotation = Quaternion.identity;
        var humanRotation = Quaternion.LookRotation(-side);
        // Contact centers, including the dragon's authored offset, define the
        // start gap in every direction. Roots stay on the battlefield floor.
        SetDragonBodyRootPose(dragon, -side * separation * .5f, dragonRotation);
        SetDragonBodyRootPose(human, side * separation * .5f, humanRotation);
        Physics.SyncTransforms();
    }

    static void SetDragonBodyRootPose(Data_Center unit, Vector3 planarContactCenter, Quaternion rotation)
    {
        var rigidbody = unit._BasicPhysicSupport.Rigidbody;
        rigidbody.rotation = rotation;
        unit.WholeT.rotation = rotation;
        var collider = unit._BasicPhysicSupport.GetComponent<CombatBodyProfile>().Body;
        var offset = collider.transform.TransformPoint(collider.center) - unit.WholeT.position;
        offset.y = 0;
        var position = planarContactCenter - offset;
        position.y = 0;
        rigidbody.position = position;
        unit.WholeT.SetPositionAndRotation(position, rotation);
    }

    static float WorldDragonBodyRadius(Data_Center unit)
    {
        var body = unit._BasicPhysicSupport.GetComponent<CombatBodyProfile>().Body;
        return body.radius * Mathf.Max(Mathf.Abs(body.transform.lossyScale.x), Mathf.Abs(body.transform.lossyScale.z));
    }

    static async UniTask SampleDragonBodyContact(Data_Center dragon, Data_Center human, Vector3 side,
        string name, bool approach, bool baseline)
    {
        PrepareDragonBodyContact(dragon, human, side, approach);
        var pa = dragon._BasicPhysicSupport.GetComponent<CombatBodyProfile>();
        var pb = human._BasicPhysicSupport.GetComponent<CombatBodyProfile>();
        var row = new DragonBodyContact
        {
            name = name, continuedApproach = approach, oldHumanRadiusBaseline = baseline,
            dragonColliderRadius = WorldDragonBodyRadius(dragon), humanColliderRadius = WorldDragonBodyRadius(human),
            minTorsoGap = float.PositiveInfinity
        };
        report.dragonBodyContacts.Add(row);
        float started = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - started < 2.7f)
        {
            // Velocity input alone continues to press against the other body;
            // PhysX resolves all separation after the initial placement.
            if (approach)
            {
                var direction = pb.Body.bounds.center - pa.Body.bounds.center;
                direction.y = 0;
                direction.Normalize();
                dragon._BasicPhysicSupport.Rigidbody.linearVelocity = direction * .45f;
                human._BasicPhysicSupport.Rigidbody.linearVelocity = -direction * .45f;
            }
            await UniTask.WaitForFixedUpdate();
            if (Time.realtimeSinceStartup - started < 1.5f) continue;
            row.settledSamples++;
            row.minTorsoGap = Mathf.Min(row.minTorsoGap, pa.PlanarTorsoGap(pb));
            if (Physics.ComputePenetration(pa.Body, pa.Body.transform.position, pa.Body.transform.rotation,
                pb.Body, pb.Body.transform.position, pb.Body.transform.rotation, out _, out float depth))
                row.maxPenetration = Mathf.Max(row.maxPenetration, depth);
        }
        dragon._BasicPhysicSupport.Rigidbody.linearVelocity = Vector3.zero;
        human._BasicPhysicSupport.Rigidbody.linearVelocity = Vector3.zero;
        var delta = dragon.WholeT.position - human.WholeT.position;
        delta.y = 0;
        row.rootSeparation = delta.magnitude;
        Require(row.settledSamples >= 10, name + " produced too few settled physics samples.");
        if (!baseline)
        {
            Require(row.maxPenetration < .07f, name + " retained solid-body penetration: " + row.maxPenetration);
            Require(row.minTorsoGap >= -.08f, name + " retained significant animated torso overlap: " + row.minTorsoGap);
        }
        if (name == "front-approach" || name == "left-approach" || baseline)
        {
            row.screenshot = Path.GetFullPath(Path.Combine(Output, name + ".png"));
            ScreenCapture.CaptureScreenshot(row.screenshot);
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        }
    }

    static async UniTask ValidateDragonBodyAttackReach(Data_Center attacker, Data_Center victim, string skillId)
    {
        // Always face both fighters at the front-contact orientation, then let
        // their current production body shapes naturally settle before casting.
        var dragon = attacker.UnitInfo.r_id == "17" ? attacker : victim;
        var human = attacker.UnitInfo.r_id == "1" ? attacker : victim;
        await SampleDragonBodyAttackContact(dragon, human);
        BoundaryControlByGod.target.SensorUnity.ForceImmediateDetection();
        var entry = attacker._MyBehaviorRunner.SkillEntityDic.Single(pair => pair.Value.SkillID == skillId);
        var state = attacker._MyBehaviorRunner.GetState(entry.Key);
        var row = new DragonBodyAttack
        {
            unit = attacker.UnitInfo.r_id, skill = skillId, state = entry.Key,
            startRootSeparation = Vector3.Distance(attacker.WholeT.position, victim.WholeT.position),
            triggerRangeMin = state.triggerAttackRangeMin, triggerRangeMax = state.triggerAttackRangeMax,
            sensorAllowsAttack = state.TimeToAttack_Reluctant()
        };
        report.dragonBodyAttacks.Add(row);
        Require(row.sensorAllowsAttack, "Unit " + row.unit + " cannot find its contact opponent in close-attack range.");
        victim.FightDataRef.Invincible = false;
        float hp = victim.FightDataRef.CurrentHp.Value;
        attacker._MyBehaviorRunner.ChangeState(entry.Key);
        float started = Time.realtimeSinceStartup;
        var seen = new HashSet<Collider>();
        while (Time.realtimeSinceStartup - started < 4.5f)
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            foreach (var marker in UnityEngine.Object.FindObjectsByType<BO_Marker>(FindObjectsSortMode.None))
            {
                var hitBox = marker.GetComponentInParent<HitBoxManager>();
                if (hitBox == null || !hitBox.Enabled || hitBox.GetOwnerFACR() != attacker.FightDataRef) continue;
                var collider = marker.GetComponent<Collider>();
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                Require(collider.isTrigger, "An active attack marker became a solid contact volume.");
                seen.Add(collider);
            }
            row.damage = hp - victim.FightDataRef.CurrentHp.Value;
            if (row.damage > 0 && seen.Count > 0) break;
        }
        row.activeAttackTriggers = seen.Count;
        Require(row.damage > 0 && row.activeAttackTriggers > 0,
            "Unit " + row.unit + " skill " + skillId + " failed to inflict native damage from settled body contact.");
        await UniTask.Delay(600, DelayType.Realtime);
        foreach (var unit in new[] { attacker, victim })
        {
            unit.FightDataRef.Invincible = true;
            unit._MyBehaviorRunner.ChangeState("Move");
            unit._MyBehaviorRunner.ChangeState("Empty");
        }
    }

    static void ValidateDragonBodyFormationAndImpact(Data_Center dragon, Data_Center human)
    {
        foreach (var unit in new[] { dragon, human })
        {
            var body = unit._BasicPhysicSupport.GetComponent<CombatBodyProfile>().Body;
            Require(BattleFormationPlacement.TryGetBodyBounds(unit, out var formation),
                "Formation did not return bounds for unit " + unit.UnitInfo.r_id);
            var contact = body.bounds;
            const float tolerance = .001f;
            Require(formation.min.x <= contact.min.x + tolerance && formation.max.x >= contact.max.x - tolerance
                && formation.min.y <= contact.min.y + tolerance && formation.max.y >= contact.max.y - tolerance
                && formation.min.z <= contact.min.z + tolerance && formation.max.z >= contact.max.z - tolerance,
                "Formation bounds exclude unit " + unit.UnitInfo.r_id + "'s actual solid contact body.");
            report.dragonChecks.Add("body-contact: unit " + unit.UnitInfo.r_id + " formation bounds enclose actual contact bounds");
        }
        PrepareDragonBodyContact(dragon, human, Vector3.forward, false);
        var sourceBody = dragon._BasicPhysicSupport.GetComponent<CombatBodyProfile>().Body;
        var victimBody = human._BasicPhysicSupport.GetComponent<CombatBodyProfile>().Body;
        float minimumSeparation = WorldDragonBodyRadius(dragon) + WorldDragonBodyRadius(human) + .05f;
        human._BasicPhysicSupport.BeginImpactFollow(dragon);
        try
        {
            foreach (int yaw in new[] { 0, 90, 180, 270 })
            {
                var rotation = Quaternion.Euler(0, yaw, 0);
                dragon._BasicPhysicSupport.Rigidbody.rotation = rotation;
                dragon.WholeT.rotation = rotation;
                Require(human._BasicPhysicSupport.UpdateImpactFollow(), "Impact following stopped at dragon yaw " + yaw);
                Physics.SyncTransforms();
                var centers = sourceBody.transform.TransformPoint(sourceBody.center)
                    - victimBody.transform.TransformPoint(victimBody.center);
                centers.y = 0;
                Require(centers.magnitude >= minimumSeparation,
                    "Dragon yaw " + yaw + " reduced actual body-center clearance during impact following.");
                Require(!Physics.ComputePenetration(sourceBody, sourceBody.transform.position, sourceBody.transform.rotation,
                    victimBody, victimBody.transform.position, victimBody.transform.rotation, out _, out _),
                    "Dragon yaw " + yaw + " made impact-following contact bodies overlap.");
                report.dragonChecks.Add("body-contact: impact-follow yaw " + yaw + " preserves actual body-center gap "
                    + centers.magnitude.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                    + " with no solid-body penetration");
            }
        }
        finally { human._BasicPhysicSupport.EndImpactFollow(); }
        Require(!human._BasicPhysicSupport.FollowingImpact && !Physics.GetIgnoreCollision(sourceBody, victimBody),
            "Impact-follow fixture did not restore ordinary body collisions.");
        PrepareDragonBodyContact(dragon, human, Vector3.forward, false);
    }

    static async UniTask SampleDragonBodyAttackContact(Data_Center dragon, Data_Center human)
    {
        PrepareDragonBodyContact(dragon, human, Vector3.forward, false);
        await UniTask.Delay(1200, DelayType.Realtime);
        dragon._BasicPhysicSupport.Rigidbody.linearVelocity = Vector3.zero;
        human._BasicPhysicSupport.Rigidbody.linearVelocity = Vector3.zero;
    }
}
