using System.Collections.Generic;
using UnityEngine;
using mainMenu;
using System.Linq;
using dataAccess;
using MCombat.Shared.Behaviour;

public partial class SkillSet
{
    // exceptSkIDs : 除了这些技能ID。切记是技能ID
    public static string RandomSkillIDOfStone(SkillStonesBox.StoneFilterForm filterForm, List<string> exceptSkIDs = null)
    {
        var skillIDsAndNames = SkillConfigTable.GetSkillIDAndNameDic(filterForm);
        var stoneSkillIDs = skillIDsAndNames.Keys.ToList();
        if (stoneSkillIDs.Count == 0)
        {
            return null;
        }
        
        if (exceptSkIDs != null)
        {
            for (int i = 0; i < exceptSkIDs.Count; i++)
            {
                if (stoneSkillIDs.Contains(exceptSkIDs[i]))
                    stoneSkillIDs.Remove(exceptSkIDs[i]);
            }
        }
        
        int ranDom = Random.Range(0, stoneSkillIDs.Count);
        return stoneSkillIDs.Count > 0 && ranDom < stoneSkillIDs.Count ? stoneSkillIDs[ranDom] : null;
    }
    
    public static List<int> RemainSlotSPLevelCal(SkillSet current)
    {
        var currentPoint = SkillBalancePoint(current.a1, current.a2, current.a3, current.b1, current.b2, current.b3, current.c1, current.c2, current.c3);
        return SkillSetSlotUtility.RemainSlotSpLevels(
            current.SkillIDList().Count,
            currentPoint,
            GetSkillSetValidationCosts());
    }

    /// <summary>
    /// check account
    /// </summary>
    /// <param name="current"></param>
    /// <param name="focusingUnitInstanceId"></param>
    /// <param name="usingStoneInstanceIds"></param>
    /// <returns></returns>
    public static List<int> RemainSlotSPLevelCal(SkillSet current, string focusingUnitInstanceId, List<string> usingStoneInstanceIds)
    {
        var currentPoint = SkillBalancePoint(current.a1, current.a2, current.a3, current.b1, current.b2, current.b3, current.c1, current.c2, current.c3);

        bool checkHasStone(int spLevel)
        {
            var stoneAccIDs = 
                Stones.TargetStonesFromAccount_except(
                    focusingUnitInstanceId, 
                    new SkillStonesBox.StoneFilterForm
                    {
                        Type = "human",
                        ExType = new int[1] { spLevel },
                        Close = false,
                        Near = false,
                        Far = false
                    }, usingStoneInstanceIds, null, true);
            return stoneAccIDs.Count > 0;
        }

        return SkillSetSlotUtility.RemainSlotSpLevels(
            current.SkillIDList().Count,
            currentPoint,
            GetSkillSetValidationCosts(),
            checkHasStone);
    }
}
