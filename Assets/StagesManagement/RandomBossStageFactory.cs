using System;
using System.Linq;
using mainMenu;
using MCombat.Shared.Combat;
using UnityEngine;

/// <summary>Creates the three daily event difficulties with fresh enemies and skill sets.</summary>
public static class RandomBossStageFactory
{
    public static FightInfo Create(string stageId, CriticalGaugeMode gaugeMode, int unitCount, float level)
    {
        if (string.IsNullOrEmpty(stageId))
            throw new ArgumentException("A random Boss stage needs a reward ID.", nameof(stageId));

        // PocketStriker's unit table contains main characters only. Also exclude the
        // shared runtime's sub-unit ID convention if such entries are introduced.
        var recordIds = Units.GetMonsterIDsAndNamesDic("human").Keys
            .Where(id => !SubUnitUtility.IsSubUnitId(id) && Units.GetUnitConfig(id) != null)
            .ToList();
        if (recordIds.Count == 0)
            throw new InvalidOperationException("No main characters are available for random Boss battles.");

        var selected = RandomSelect.Get(0, recordIds.Count - 1, Mathf.Clamp(unitCount, 1, recordIds.Count));
        var members = new FightMembers();
        for (var index = 0; index < selected.Count; index++)
        {
            var recordId = recordIds[selected[index]];
            var config = Units.GetUnitConfig(recordId);
            var filter = new SkillStonesBox.StoneFilterForm(config.TYPE);
            if (gaugeMode != CriticalGaugeMode.Normal)
                filter.ExType = new[] { 1, 2, 3 };

            var unit = new UnitInfo
            {
                id = recordId,
                r_id = recordId,
                set = SkillSet.RandomSkillSet(
                    UnitPassiveTable.GetUnitPassiveRecordId(recordId), false, filter,
                    gaugeMode == CriticalGaugeMode.Unlimited)
            };
            members.EnemySets.Set(0, index, unit);
        }

        var stage = ScriptableObject.CreateInstance<FightInfo>();
        stage.name = stageId;
        stage.ID = stageId;
        stage.EventType = FightEventType.Event;
        stage.FightMembers = members;
        stage.FightMode = FightMode.Rotate;
        // 0 keeps the existing preparation screen's team / rotation choice enabled.
        stage.ArcadeFightMode = 0;
        stage.team2CGMode = gaugeMode;
        stage.stageRefLevel = level;
        // SetUnitLevelByRefLevel operates on UnitsData, not the member dictionary.
        stage.SaveDicToData();
        stage.SetUnitLevelByRefLevel();
        return stage;
    }
}
