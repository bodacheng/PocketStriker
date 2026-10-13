using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using FightScene;
using mainMenu;
using MCombat.Shared.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class PocketStrikerCombatFlowSmoke
{
    [MenuItem("PocketStriker/Validation/Ruby Impact Playmode Smoke")]
    public static void StartRubyImpactBatch()
    {
        SessionState.SetBool(Key + ".TutorialBalance", false);
        SessionState.SetBool(Key + ".EvolutionHeal", false);
        SessionState.SetBool(Key + ".StoryFailures", false);
        Begin(rubyImpact: true);
    }

    static async UniTask ValidateRubyImpact()
    {
        report.scope = "Skill 16 Ruby Impact: actual local FightScene, Addressables preload, authored rollfoward animation prepare/release, attached body weapon and active particle/render evidence. One separated-target cast checks no damage and retirement within two game seconds; two contact casts check real damage, disabled hitboxes and pooled-instance reuse.";
        report.limitation = "Editor Play mode; local test account, scripted skill selection and positioning with AI disabled. Does not contact account/reward services, publish bundles or exercise a physical-device GPU.";
        var random = UnityEngine.Random.state;
        var fight = ScriptableObject.CreateInstance<FightInfo>();
        try
        {
            UnityEngine.Random.InitState(1613);
            var config = Units.GetUnitConfig("1");
            var set = SkillSet.RandomSkillSet("16", false, new SkillStonesBox.StoneFilterForm("human"));
            Require(set.SkillIDList().Contains("16"), "Ruby Impact is absent from the equipped fixture.");
            set.SetPassive(config.DEFENDABLE_FLAG, config.MoveType, config.RushType);
            set.SortNineAndTwo();
            var hero = new UnitInfo { id = "0", r_id = "1", level = 10, set = set };
            fight.ID = "ruby-impact-import";
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
            var skill = attacker._MyBehaviorRunner.SkillEntityDic.Single(pair => pair.Value.SkillID == "16");
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
                // Keep both units inside the arena; the first cast has no contact
                // so a hit-triggered GMB state exit cannot retire the effect early.
                float separation = separated ? 6f : 1.5f;
                // Fixture placement must bypass the skill-motion sweep, which
                // correctly prevents teleporting through the other fighter.
                CombatPlacementUtility.PlaceRootByGeometryCenter(attacker.WholeT,
                    attacker._BasicPhysicSupport.Rigidbody, attacker.geometryCenter,
                    new Vector3(0, 1.2f, -separation), Quaternion.identity);
                CombatPlacementUtility.PlaceRootByGeometryCenter(victim.WholeT,
                    victim._BasicPhysicSupport.Rigidbody, victim.geometryCenter,
                    new Vector3(0, 1.2f, separation), Quaternion.Euler(0, 180, 0));
                Physics.SyncTransforms();
                await UniTask.Delay(250);
                Require(!attacker._MyBehaviorRunner.AI && !victim._MyBehaviorRunner.AI,
                    "Ruby Impact fixture AI was re-enabled before casting.");
                Require(!separated || Vector3.Distance(attacker.geometryCenter.position, victim.geometryCenter.position) > 10f,
                    "Ruby Impact no-contact fixture lost its separated starting positions.");
                float initialHp = victim.FightDataRef.CurrentHp.Value;
                attacker._MyBehaviorRunner.ChangeState(skill.Key);
                Decomposition effect = null;
                float start = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - start < 3 && effect == null)
                {
                    await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
                    effect = UnityEngine.Object.FindObjectsByType<Decomposition>(FindObjectsSortMode.None)
                        .FirstOrDefault(item => item.name.StartsWith("rollfoward", StringComparison.Ordinal)
                            && item.IsWeapon && item._HitBox != null && item._HitBox.Enabled
                            && item._HitBox.GetOwnerFACR() == attacker.FightDataRef);
                }
                Require(effect != null, "Authored animation never released the rollfoward body weapon, cast " + cast);
                float releaseTime = Time.time;
                float releaseRealtime = Time.realtimeSinceStartup;
                var constraint = effect.GetPositionConstraint();
                Require(constraint != null && constraint.constraintActive && constraint.sourceCount == 1
                    && constraint.GetSource(0).sourceTransform == attacker.geometryCenter,
                    "Ruby Impact did not attach to the caster's body center.");
                if (cast == 0) first = effect;
                else Require(first == effect, "Ruby Impact cast " + cast + " did not reuse the retired pooled weapon.");
                bool sawParticles = false, sawRenderer = false, captured = false;
                // A successful GMB hit can immediately exit the skill and return
                // its weapon. Collect evidence while it is active, before that exit.
                while (effect.gameObject.activeInHierarchy && Time.time - releaseTime < 2f
                    && Time.realtimeSinceStartup - releaseRealtime < 8f)
                {
                    sawParticles |= effect.GetComponentsInChildren<ParticleSystem>().Any(system => system.particleCount > 0);
                    sawRenderer |= effect.GetComponentsInChildren<Renderer>().Any(renderer => renderer.enabled
                        && renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null
                        && renderer.sharedMaterial.shader.isSupported);
                    if (sawParticles && sawRenderer && !captured)
                    {
                        ScreenCapture.CaptureScreenshot(Path.Combine(Output, "ruby-impact-cast-" + cast + ".png"));
                        captured = true;
                    }
                    await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
                }
                float activeSeconds = Time.time - releaseTime;
                Require(sawParticles && sawRenderer, "Ruby Impact emitted no particles with a supported renderer during its active window, cast " + cast);
                Require(!effect.gameObject.activeSelf && effect.Phase == 0 && activeSeconds < 2f,
                    "Ruby Impact did not return to its pool within two game seconds, cast " + cast);
                Require(!effect._HitBox.Enabled && effect._HitBox.GetOwnerFACR() == null,
                    "Ruby Impact retained an active hitbox or owner after returning to the pool.");
                if (separated)
                    Require(Mathf.Approximately(victim.FightDataRef.CurrentHp.Value, initialHp),
                        "Separated-target cast unexpectedly inflicted damage; early retirement is not isolated.");
                else
                    Require(victim.FightDataRef.CurrentHp.Value < initialHp, "Ruby Impact did not inflict real damage, cast " + cast);
                report.rubyImpactChecks.Add("cast " + cast + ": authored release, body-center attachment, active particles/renderer, pool return after "
                    + activeSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " game seconds; "
                    + (separated ? "no-contact retirement before the prefab's six-second lifetime; no damage"
                        : "real hit damage; pooled instance reused"));
            }
        }
        finally
        {
            UnityEngine.Random.state = random;
            UnityEngine.Object.Destroy(fight);
        }
    }
}
