using System;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static partial class PocketStrikerCombatFlowSmoke
{
    [MenuItem("PocketStriker/Validation/Tutorial Difficulty Playmode Smoke")]
    public static void StartTutorialBalanceBatch()
    {
        SessionState.SetBool(Key + ".TutorialBalance", true);
        SessionState.SetBool(Key + ".EvolutionHeal", false);
        SessionState.SetBool(Key + ".StoryFailures", false);
        Begin();
    }

    static async UniTask ValidateTutorialBalance(ArcadeModeManager manager)
    {
        var oldRandom = UnityEngine.Random.state;
        try
        {
            for (int stageNumber = 1; stageNumber <= 2; stageNumber++)
            for (int sample = 0; sample < 3; sample++)
            {
                UnityEngine.Random.InitState(731 + sample);
                var fight = await manager.LoadStage(stageNumber).Timeout(TimeSpan.FromSeconds(30));
                try
                {
                    Require(fight != null && !fight.IsGroupBattle && !fight.EvolutionMode
                        && fight.FightMode == FightMode.Rotate, "Tutorial must remain a single-hero rotation battle.");
                    Require(fight.UnitsData.Count == stageNumber && fight.stageRefLevel == 1f
                        && fight.team1HpRate > 1f && fight.team2HpRate < .6f,
                        "Published tutorial still selects the four-opponent evolution difficulty asset.");
                    // Keep every authored combat parameter, replacing only account routing
                    // and player inventory so this can run without a live server account.
                    fight.EventType = FightEventType.Self;
                    fight.ID = "tutorial-balance-" + stageNumber + "-" + sample;
                    fight.Team1ID = Account;
                    fight.Team2ID = Account + "-enemy";
                    fight.FightMembers = new FightMembers();
                    var set = sample == 1
                        ? new SkillSet { a1 = "5", a2 = "176", a3 = "44", b1 = "27", b2 = "58", b3 = "118", c1 = "53", c2 = "95", c3 = "80" }
                        : new SkillSet { a1 = "5", a2 = "12", a3 = "4", b1 = "27", b2 = "58", b3 = "77", c1 = "53", c2 = "95", c3 = "80" };
                    var validation = set.CheckEdit();
                    Require(validation == SkillSet.SkillEditError.Perfect,
                        "Difficulty fixture must satisfy the production skill editor rules: " + validation);
                    var hero = new UnitInfo { id = "0", r_id = sample == 1 ? "1" : "3", level = 1, set = set };
                    var config = Units.GetUnitConfig(hero.r_id);
                    set.SetPassive(config.DEFENDABLE_FLAG, config.MoveType, config.RushType);
                    set.SortNineAndTwo();
                    fight.FightMembers.HeroSets.Set(0, 0, hero);
                    for (int index = 0; index < fight.UnitsData.Count; index++)
                    {
                        var enemy = fight.UnitsData[index].DeepCopy();
                        enemy.id = index.ToString();
                        fight.FightMembers.EnemySets.Set(0, index, enemy);
                    }
                    await RunBattle(fight, "tutorial-" + stageNumber + "-level1-sample" + sample, report.cases.Count > 0);
                    var result = report.cases[report.cases.Count - 1];
                    Require(result.winner == Team.player1.ToString() && result.playerRemainingHpFraction >= .1f,
                        "A legal level-1 starter lacks a safe tutorial victory margin: stage " + stageNumber
                        + ", sample " + sample + ", HP=" + result.playerRemainingHpFraction);
                }
                finally { if (fight != null) UnityEngine.Object.Destroy(fight); }
            }
            Require(report.cases.Count == 6, "Both tutorial stages need three natural battle samples.");
        }
        finally { UnityEngine.Random.state = oldRandom; }
    }
}
