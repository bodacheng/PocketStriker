using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using FightScene;
using mainMenu;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static partial class PocketStrikerCombatFlowSmoke
{
    [MenuItem("PocketStriker/Validation/Dragon Playmode Smoke")]
    public static void StartDragonBatch()
    {
        SessionState.SetBool(Key + ".TutorialBalance", false);
        SessionState.SetBool(Key + ".EvolutionHeal", false);
        SessionState.SetBool(Key + ".StoryFailures", false);
        Begin(true);
    }

    static async UniTask ValidateDragonCombat()
    {
        report.scope = "Imported MCombat dragon on each team in two actual local rotation battles: production Addressables, model/animation loading, AI, skill events, damage, death and results; authored material and ground shadow proxies. Three additional real evolution UI selections verify the sparse dragon skill pool.";
        report.limitation = "Editor Play mode with local fixture account and shortened HP; no remote inventory/reward changes. Two AI battles use natural damage; the separate evolution UI fixture uses invulnerability and explicit opponent death notifications. Animation binding validation separately covers all twelve dragon skills; natural AI is not guaranteed to select every skill.";
        var random = UnityEngine.Random.state;
        try
        {
            for (int sample = 0; sample < 2; sample++)
            {
                UnityEngine.Random.InitState(1713 + sample);
                var fight = ScriptableObject.CreateInstance<FightInfo>();
                try
                {
                    fight.ID = "dragon-import-" + sample;
                    fight.EventType = FightEventType.Self;
                    fight.FightMode = FightMode.Rotate;
                    fight.Team1ID = Account;
                    fight.Team2ID = Account + "-enemy";
                    fight.battleGroundID = 0;
                    fight.team1CGMode = fight.team2CGMode = CriticalGaugeMode.Unlimited;
                    fight.team1AIMode = fight.team2AIMode = AIMode.Aggressive;
                    fight.team1HpRate = fight.team2HpRate = .35f;
                    fight.FightMembers = new FightMembers();
                    fight.FightMembers.HeroSets.Set(0, 0, DragonFixtureUnit(sample == 0 ? "1" : "17"));
                    fight.FightMembers.EnemySets.Set(0, 0, DragonFixtureUnit(sample == 0 ? "17" : "1"));
                    await RunBattle(fight, sample == 0 ? "dragon-enemy" : "dragon-player", sample > 0);
                }
                finally { UnityEngine.Object.Destroy(fight); }
            }
            Require(report.cases.Count == 2 && report.cases.All(item => item.damageEvents > 0 && item.deathCount > 0),
                "Dragon battles did not finish through real damage and elimination.");
            Require(report.dragonAttackStates.Count > 0, "Dragon AI never executed a native attack.");
            await ValidateDragonEvolutionChoices();
        }
        finally { UnityEngine.Random.state = random; }
    }

    static async UniTask ValidateDragonEvolutionChoices()
    {
        var fight = ScriptableObject.CreateInstance<FightInfo>();
        try
        {
            fight.ID = "dragon-evolution-choices";
            fight.EventType = FightEventType.Self;
            fight.FightMode = FightMode.Evolve;
            fight.Team1ID = Account;
            fight.Team2ID = Account + "-enemy";
            fight.battleGroundID = 0;
            fight.FightMembers = new FightMembers();
            fight.FightMembers.HeroSets.Set(0, 0, DragonFixtureUnit("17"));
            for (int index = 0; index < 4; index++)
            {
                var enemy = DragonFixtureUnit("1");
                enemy.id = index.ToString();
                fight.FightMembers.EnemySets.Set(0, index, enemy);
            }
            FightLoad.Go(fight, true);
            await UniTask.WaitUntil(() => SceneManager.GetActiveScene().name == "FightScene"
                && FightLoad.Fight?.ID == fight.ID && FSceneProcessesRunner.Main.currentProcess is FightingProcess
                && UnityEngine.Object.FindFirstObjectByType<FightingStepLayer>()?.Initialized == true)
                .Timeout(TimeSpan.FromSeconds(90));
            var manager = RTFightManager.Target;
            manager.team1.TurnAllUnitsInvincible(true);
            manager.team2.TurnAllUnitsInvincible(true);
            manager.team1.Auto = manager.team2.Auto = false;
            var dragon = manager.team1.RMode_Unit.Value;
            for (int phase = 1; phase <= 3; phase++)
            {
                var enemy = manager.team2.RMode_Unit.Value;
                enemy.FightDataRef.IsDead.Value = true;
                await UniTask.WaitUntil(() => ActiveEvolutionLayer() != null).Timeout(TimeSpan.FromSeconds(15));
                var layer = ActiveEvolutionLayer();
                await UniTask.WaitUntil(() => ((UnityEngine.UI.Text)typeof(InBattleEvolution)
                    .GetField("upperText", PrivateInstance).GetValue(layer)).text == Translate.Get("ChooseYourEvolution"))
                    .Timeout(TimeSpan.FromSeconds(20));
                var options = (EvolutionSkill[])typeof(InBattleEvolution).GetField("skillOptions", PrivateInstance).GetValue(layer);
                var shown = options.Where(option => option.gameObject.activeInHierarchy).ToArray();
                Require(shown.Length == (phase == 3 ? 2 : 1), "Dragon evolution shows missing or surplus options at phase " + phase);
                await UniTask.Delay(150, DelayType.Realtime);
                ScreenCapture.CaptureScreenshot(Path.Combine(Output, "dragon-evolution-" + phase + ".png"));
                shown[0].Btn.onClick.Invoke();
                await UniTask.WaitUntil(() => ActiveEvolutionLayer() == null && manager.team2.RMode_Unit.Value != enemy)
                    .Timeout(TimeSpan.FromSeconds(30));
                Require(dragon.UnitInfo.set.SkillIDList().All(id => SkillConfigTable.GetSkillConfigByRecordId(id)?.TYPE == "e_dragon"),
                    "Evolution equipped a missing or non-dragon skill.");
                report.dragonChecks.Add("evolution-" + phase + ": " + shown.Length + " valid options; native UI selection resumes with dragon-only skills");
            }
        }
        finally { UnityEngine.Object.Destroy(fight); }
    }

    static UnitInfo DragonFixtureUnit(string id)
    {
        var config = Units.GetUnitConfig(id);
        Require(config != null, "Missing smoke unit " + id);
        var set = SkillSet.RandomSkillSet(UnitPassiveTable.GetUnitPassiveRecordId(id), false,
            new SkillStonesBox.StoneFilterForm(config.TYPE), true);
        Require(set.SkillIDList().Count > 0, "No native skills for " + config.TYPE);
        set.SetPassive(config.DEFENDABLE_FLAG, config.MoveType, config.RushType);
        set.SortNineAndTwo();
        return new UnitInfo { id = "0", r_id = id, level = 10, set = set };
    }

    static void SampleDragonCombat(List<Data_Center> units, string name, int frame)
    {
        var dragon = units.SingleOrDefault(unit => unit.UnitInfo.r_id == "17");
        Require(dragon != null, "Actual battle did not load the dragon.");
        if (frame == 1)
        {
            var renderers = dragon.WholeT.GetComponentsInChildren<Renderer>();
            var body = renderers.Where(renderer => renderer.gameObject.layer == 3 && renderer is SkinnedMeshRenderer).ToArray();
            Require(body.Length > 0 && body.All(renderer => renderer.sharedMaterials.All(material => material != null
                && material.shader.name == "OmniShade/Standard URP" && material.mainTexture != null)),
                "Runtime dragon lost its project material or diffuse texture.");
            Require(renderers.Any(renderer => renderer.name.StartsWith("shadow_", StringComparison.Ordinal)
                && renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly), "Dragon has no ground shadow proxy.");
            report.dragonChecks.Add(name + ": actual unit17, colored OmniShade materials, diffuse atlas and shadows loaded");
        }
        var state = dragon._MyBehaviorRunner.GetNowState();
        if (state != null && state.StateKey != null && state.StateKey.StartsWith("El_Attack_", StringComparison.Ordinal)
            && !report.dragonAttackStates.Contains(state.StateKey)) report.dragonAttackStates.Add(state.StateKey);
        if (frame == 60) ScreenCapture.CaptureScreenshot(Path.Combine(Output, name + ".png"));
    }
}
