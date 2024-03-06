using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PosKeySet
{
    [System.Serializable]
    public class OneSet : ICloneable
    {
        public string instanceID;
        public int posNum;

        public OneSet()
        {
        }

        public OneSet(int posNum, string instanceId)
        {
            this.posNum = posNum;
            this.instanceID = instanceId;
        }
        
        public object Clone()
        {
            return base.MemberwiseClone();
        }
    }

    public PosKeySet Clone()
    {
        var posKeySet = new PosKeySet();
        var sets = new List<OneSet>();
        foreach (var p in PosNumsWithLocalKeys)
        {
            sets.Add((PosKeySet.OneSet)p.Clone());
        }
        posKeySet.PosNumsWithLocalKeys = sets.ToArray();
        return posKeySet;
    }

    public OneSet[] PosNumsWithLocalKeys = {};
    
    public PosKeySet()
    {
        var list = new List<OneSet>();
        for (var i = 0; i <= 20; i++) // 这里要重新想
        {
            list.Add(new OneSet(i, null));
        }
        PosNumsWithLocalKeys = list.ToArray();
        // PosNumsWithLocalKeys = new[] { new OneSet(0, null), new OneSet(1, null), new OneSet(2, null) };
    }
    
    public MultiDic<int, int, UnitInfo> LoadTeamDic()
    {
        var multiDictionary = new MultiDic<int, int, UnitInfo>();
        for (var i = 0; i < PosNumsWithLocalKeys.Length; i++)
        {
            if (PosNumsWithLocalKeys[i].instanceID != null)
            {
                var unitInfoOrigin = dataAccess.Units.Get(PosNumsWithLocalKeys[i].instanceID);
                if (unitInfoOrigin != null)
                {
                    var unitInfoAdvanced = UnitInfo.GetUnitInfo(unitInfoOrigin);
                    multiDictionary.Set(0, PosNumsWithLocalKeys[i].posNum, unitInfoAdvanced);
                }
            }
        }
        return multiDictionary;
    }
    
    public void SetPosMemInfoByInstanceID(int posNum, string instanceID)
    {
        for (var i = 0; i < PosNumsWithLocalKeys.Length; i++)
        {
            if (PosNumsWithLocalKeys[i].posNum == posNum)
            {
                PosNumsWithLocalKeys[i].instanceID = instanceID;
                return;
            }
        }
        Debug.Log("posNum not exists：" + posNum);
    }
    
    public List<OneSet> SetPosUnitByInstanceID(int targetPos, string instanceID)// 返回长度为2时，第一个元素是目标位置，第二个元素是被替换位置
    {
        if (targetPos == -1)
            return new List<OneSet>();
        bool inTeamMemberChange = false;
        
        foreach (var set in PosNumsWithLocalKeys)
        {
            if (set.instanceID == instanceID && set.instanceID != null)
            {
                if (set.posNum != targetPos)
                {
                    inTeamMemberChange = true;
                    ChangePosition(targetPos, set.posNum);
                    return new List<OneSet> {GetPosMemInfo(targetPos), set};
                }
                else
                {
                    //那其实也就是点击了下原位置角色的头像
                }
            }
        }
        if (!inTeamMemberChange)
        {
            SetPosMemInfoByInstanceID(targetPos, instanceID);
            return new List<OneSet> { GetPosMemInfo(targetPos) };
        }
        return new List<OneSet>();
    }
    
    public OneSet GetPosMemInfo(int posNum)
    {
        for (var i = 0; i < PosNumsWithLocalKeys.Length; i++)
        {
            if (PosNumsWithLocalKeys[i].posNum == posNum)
                return PosNumsWithLocalKeys[i];
        }
        return null;
    }
    
    public string GetInstanceIdOnPos(int posNum)
    {
        return GetPosMemInfo(posNum) != null ? GetPosMemInfo(posNum).instanceID ?? null : null;
    }
    
    public void ChangePosition(int position1, int position2)
    {
        var posNumWithLocalKey1 = GetPosMemInfo(position1);
        var posNumWithLocalKey2 = GetPosMemInfo(position2);
        
        (posNumWithLocalKey2.instanceID, posNumWithLocalKey1.instanceID) = (posNumWithLocalKey1.instanceID, posNumWithLocalKey2.instanceID);
    }
    
    // 暂时不再使用。最初是selfFight模式下队员要求不重复的队员指定模式
    public static void ChangePositionBetweenDifferentTeamSets(int position1, PosKeySet team1, int position2, PosKeySet team2)
    {
        var posNumWithLocalKey1 = team1.GetPosMemInfo(position1);
        var posNumWithLocalKey2 = team2.GetPosMemInfo(position2);
        (posNumWithLocalKey2.instanceID, posNumWithLocalKey1.instanceID) = (posNumWithLocalKey1.instanceID, posNumWithLocalKey2.instanceID);
    }
    
    // 暂时不再使用。最初是selfFight模式下队员要求不重复的队员指定模式
    public List<string> GetAllOnSetMonsterOfPlayerIds()
    {
        var onsetMonsterOfPlayerIds = new List<string>();
        foreach (var _Set in PosNumsWithLocalKeys)
        {
            if (_Set.instanceID != null)
                onsetMonsterOfPlayerIds.Add(_Set.instanceID);
        }
        return onsetMonsterOfPlayerIds;
    }
    
    // 暂时不再使用。最初是SelfFight模式下队员要求不重复的队员指定模式
    public OneSet GetPosMemInfoByLocalID(string localID)
    {
        if (PosNumsWithLocalKeys == null)
            return null;
        foreach (OneSet _set in this.PosNumsWithLocalKeys)
        {
            if (_set.instanceID != null)
            {
                if (_set.instanceID == localID)
                    return _set;
            }
        }
        return null;
    }
}