using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using FightScene;
using HittingDetection;
using mainMenu;
using MCombat.Shared.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class PocketStrikerCombatFlowSmoke
{
    [MenuItem("PocketStriker/Validation/Head Strike Playmode Smoke")]
    public static void StartHeadStrikeBatch()
    {
        SessionState.SetBool(Key + ".TutorialBalance", false);
        SessionState.SetBool(Key + ".EvolutionHeal", false);
        SessionState.SetBool(Key + ".StoryFailures", false);
        Begin(headStrike: true);
    }

    static async UniTask ValidateHeadStrike()
    {
        report.scope = "Skill 27 Head Strike: actual FightScene, Addressables animation/weapon preload, authored frank_heada MagicForward event and MCombat k_hitbox sphere/sound. A separated cast checks creation and harmless expiry; two casts at 3.25 metres check real damage, pool reuse, owner/marker cleanup and the restored 1.5 metre attack radius.";
        report.limitation = "Editor Play mode, local fixture account, scripted skill selection/placement with AI disabled. MCombat authors this attack as a collision sphere with a whoosh sound; no standalone attack renderer is expected. No account/reward services or remote bundle publication.";
        var random = UnityEngine.Random.state;
        var fight = ScriptableObject.CreateInstance<FightInfo>();
        try
        {
            UnityEngine.Random.InitState(2714);
            var config = Units.GetUnitConfig("1");
            var set = SkillSet.RandomSkillSet("27", false, new SkillStonesBox.StoneFilterForm("human"));
            Require(set.SkillIDList().Contains("27"), "Head Strike is absent from the equipped fixture.");
            set.SetPassive(config.DEFENDABLE_FLAG, config.MoveType, config.RushType);
            set.SortNineAndTwo();
            var hero = new UnitInfo { id = "0", r_id = "1", level = 10, set = set };
            fight.ID = "head-strike-import";
            fight.EventType = FightEventType.Self;
            fight.FightMode = FightMode.Rotate;
            fight.Team1ID = Account;
            fight.Team2ID = Account + "-enemy";
            fight.team1CGMode = fight.team2CGMode = CriticalGaugeMode.Unlimited;
            fight.team1HpRate = fight.team2HpRate = 20;
            fight.battleGroundID = 0;
            fight.FightMembers = new FightMembers();
            fight.FightMembers.HeroSets.Set(0, 0, hero);
            fight.FightMembers.EnemySets.Set(0, 0, hero.DeepCopy());
            FightLoad.Go(fight, false);
            await UniTask.WaitUntil(() => SceneManager.GetActiveScene().name == "FightScene"
                && FightLoad.Fight?.ID == fight.ID && FSceneProcessesRunner.Main.currentProcess is FightingProcess
                && UnityEngine.Object.FindFirstObjectByType<FightingStepLayer>()?.Initialized == true)
                .Timeout(TimeSpan.FromSeconds(90));
            var manager = RTFightManager.Target;
            manager.team1.Auto = manager.team2.Auto = false;
            var attacker = manager.team1.RMode_Unit.Value;
            var victim = manager.team2.RMode_Unit.Value;
            attacker.FightDataRef.Invincible = true;
            victim.FightDataRef.Invincible = false;
            var skill = attacker._MyBehaviorRunner.SkillEntityDic.Single(pair => pair.Value.SkillID == "27");
            var pool = HurtObjectManager.GetHurtObjectPool("k_hitbox", FightGlobalSetting.EffectPathDefine(config.element));
            Require(pool != null, "Head Strike did not preload the defaultmagic k_hitbox weapon fallback.");
            Decomposition first = null;
            for (int cast = 0; cast < 3; cast++)
            {
                bool separated = cast == 0;
                foreach (var unit in new[] { attacker, victim })
                {
                    unit._MyBehaviorRunner.AI = false;
                    unit._MyBehaviorRunner.ChangeState("Move");
                    unit._MyBehaviorRunner.ChangeState("Empty");
                    unit._BasicPhysicSupport.Rigidbody.linearVelocity = Vector3.zero;
                }
                float distance = separated ? 12f : 3.25f;
                CombatPlacementUtility.PlaceRootByGeometryCenter(attacker.WholeT,
                    attacker._BasicPhysicSupport.Rigidbody, attacker.geometryCenter,
                    new Vector3(0, 1.2f, -distance / 2f), Quaternion.identity);
                CombatPlacementUtility.PlaceRootByGeometryCenter(victim.WholeT,
                    victim._BasicPhysicSupport.Rigidbody, victim.geometryCenter,
                    new Vector3(0, 1.2f, distance / 2f), Quaternion.Euler(0, 180, 0));
                Physics.SyncTransforms();
                await UniTask.Delay(250);
                float initialHp = victim.FightDataRef.CurrentHp.Value;
                attacker._MyBehaviorRunner.ChangeState(skill.Key);
                Decomposition weapon = null;
                float start = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - start < 3 && weapon == null)
                {
                    await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
                    weapon = UnityEngine.Object.FindObjectsByType<Decomposition>(FindObjectsSortMode.None)
                        .FirstOrDefault(item => item.name.StartsWith("k_hitbox", StringComparison.Ordinal)
                            && item.IsWeapon && item._HitBox != null && item._HitBox.Enabled
                            && item._HitBox.GetOwnerFACR() == attacker.FightDataRef);
                }
                Require(weapon != null, "Head Strike animation never released its attack object, cast " + cast);
                float releaseTime = Time.time;
                float releaseRealtime = Time.realtimeSinceStartup;
                var marker = weapon.GetComponentInChildren<BO_Marker>();
                var collider = marker != null ? marker.GetComponent<SphereCollider>() : null;
                Require(marker != null && collider != null && collider.enabled && collider.isTrigger
                    && Mathf.Approximately(marker.radius, 1.5f) && Mathf.Approximately(collider.radius, 1.5f),
                    "Head Strike attack object lost its MCombat sphere marker or 1.5 metre radius.");
                Require(weapon.AudioSource != null && weapon.AudioSource.clip != null,
                    "Head Strike attack object has no authored whoosh sound.");
                Require(weapon._HitBox.AT_weight > 0 && weapon._HitBox.damage_type == DamageType.push_to_mid,
                    "Head Strike attack object must apply damage and the authored push reaction.");
                if (cast == 0) first = weapon;
                else Require(first == weapon, "Head Strike did not reuse its retired pooled attack object, cast " + cast);
                float releaseDistance = Vector3.Distance(weapon.transform.position, victim.geometryCenter.position);
                while (weapon.gameObject.activeSelf && Time.time - releaseTime < 1f
                    && Time.realtimeSinceStartup - releaseRealtime < 5f)
                    await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
                Require(!weapon.gameObject.activeSelf && weapon.Phase == 0 && !weapon._HitBox.Enabled
                    && weapon._HitBox.GetOwnerFACR() == null && !collider.enabled,
                    "Head Strike attack object did not retire its markers/owner and return to the pool.");
                float damage = initialHp - victim.FightDataRef.CurrentHp.Value;
                if (separated) Require(Mathf.Approximately(damage, 0), "Separated Head Strike unexpectedly inflicted damage.");
                else Require(damage > 0, "Head Strike inflicted no damage from its expected attack range, cast " + cast);
                report.headStrikeChecks.Add("cast " + cast + ": authored animation spawned k_hitbox with a 1.5 metre sphere and whoosh; "
                    + "target distance from attack sphere=" + releaseDistance.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                    + "; damage=" + damage.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                    + "; pooled attack returned with disabled marker and cleared owner.");
            }
        }
        finally
        {
            UnityEngine.Random.state = random;
            UnityEngine.Object.Destroy(fight);
        }
    }
}
