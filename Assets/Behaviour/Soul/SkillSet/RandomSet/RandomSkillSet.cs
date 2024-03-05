using System.Collections.Generic;
using UnityEngine;
using mainMenu;
using System.Linq;

public partial class SkillSet
{
    // exceptSkIDs : 除了这些技能ID。切记是技能ID
    static string RandomSkillIDOfStone(SkillStonesBox.StoneFilterForm filterForm, List<string> exceptSkIDs)
    {
        var skillIDsAndNames = SkillConfigTable.GetSkillIDAndNameDic(filterForm);
        var stoneSkillIDs = skillIDsAndNames.Keys.ToList();
        if (stoneSkillIDs.Count == 0)
        {
            return null;
        }
        
        for (int i = 0; i < exceptSkIDs.Count; i++)
        {
            if (stoneSkillIDs.Contains(exceptSkIDs[i]))
                stoneSkillIDs.Remove(exceptSkIDs[i]);
        }
        
        int ranDom = Random.Range(0, stoneSkillIDs.Count);
        return stoneSkillIDs[ranDom];
    }

    public static List<int> RemainSlotSPLevelCal(SkillSet current)
    {
        var remainSlotCount = 9 - current.SkillIDList().Count;
        var currentPoint = SkillBalancePoint(current.a1, current.a2, current.a3, current.b1, current.b2, current.b3, current.c1, current.c2, current.c3);
        var point = currentPoint + (remainSlotCount - 1) * 10;
        var returnValue = new List<int>();
        if (point >= 30)
            returnValue.Add(3);
        if (point >= 20)
            returnValue.Add(2);
        if (point >= 10)
            returnValue.Add(1);
        returnValue.Add(0);
        return returnValue;
    }
}
