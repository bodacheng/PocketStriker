using dataAccess;
using System.Collections.Generic;
using UnityEngine;
using System;
using Skill;

[Serializable]
public class UnitInfo
{
    public string id;
    public string r_id;
    public float level = 1;
    public SkillSet set = new SkillSet();
    
    public UnitInfo Clone()
    {
        return (UnitInfo)MemberwiseClone();
    }

    public UnitInfo DeepCopy()
    {
        var copy = this.Clone();
        copy.set = copy.set.DeepCopy();
        return copy;
    }

    public UnitInfo()
    {
    }
    
    public UnitInfo(string instanceID, string r_id, SkillSet skillSet)
    {
        id = instanceID;
        this.r_id = r_id;
        set = skillSet;
    }
    
    public static UnitInfo GetUnitInfo(UnitInfo info)
    {
        try
        {
            var unitInfo = new UnitInfo
            {
                r_id = info.r_id,
                id = info.id
            };
            
            var targets = Stones.GetEquippingStones(info.id);
            var set = new SkillSet();
            var unitConfigInfo = Units.RowToUnitConfigInfo(Units.Find_RECORD_ID(info.r_id));
            if (unitConfigInfo == null)
            {
                Debug.Log("角色定义信息错误。r_id：" + info.r_id);
                return null;
            }

            bool IsCompatibleSkill(string skillId, out SkillConfig skillConfig)
            {
                skillConfig = SkillConfigTable.GetSkillConfigByRecordId(skillId);
                if (skillConfig == null)
                {
                    return false;
                }
                if (skillConfig.TYPE != unitConfigInfo.TYPE)
                {
                    Debug.LogWarning($"[TeamCompat] Skip skill {skillId} for unit {info.r_id}: type mismatch {skillConfig.TYPE} != {unitConfigInfo.TYPE}");
                    return false;
                }
                var animationKey = unitConfigInfo.TYPE + "/skill/" + skillConfig.REAL_NAME + ".anim";
                if (AddressablesLogic.HasIndexedTag("skill_anim") &&
                    !AddressablesLogic.CheckKeyExist("skill_anim", animationKey))
                {
                    Debug.LogWarning($"[TeamCompat] Skip skill {skillId} for unit {info.r_id}: missing animation {animationKey}");
                    return false;
                }
                return true;
            }

            var levels = new List<float>();
            foreach (var t in targets)
            {
                if (!IsCompatibleSkill(t.SkillId, out var sc))
                {
                    continue;
                }
                switch (t.slot)
                {
                    case "1":
                        set.a1 = t.SkillId;
                        break;
                    case "2":
                        set.a2 = t.SkillId;
                        break;
                    case "3":
                        set.a3 = t.SkillId;
                        break;
                    case "4":
                        set.b1 = t.SkillId;
                        break;
                    case "5":
                        set.b2 = t.SkillId;
                        break;
                    case "6":
                        set.b3 = t.SkillId;
                        break;
                    case "7":
                        set.c1 = t.SkillId;
                        break;
                    case "8":
                        set.c2 = t.SkillId;
                        break;
                    case "9":
                        set.c3 = t.SkillId;
                        break;
                }
                if (t.Born == "true" && sc.EVENT_CODE == "Born")
                {
                }
                else
                {
                    levels.Add(t.Level);
                }
            }
            
            unitInfo.level = set.GetAerLevel(levels);
            set.SetPassive(unitConfigInfo.DEFENDABLE_FLAG, unitConfigInfo.MoveType, unitConfigInfo.RushType);
            unitInfo.set = set;
            unitInfo.set.SortNineAndTwo();
            return unitInfo;
        }
        catch (Exception e)
        {
            Debug.Log("数据库信息有错误:" + e);
            return null;
        }
    }

    // 这个是从角色存档来读取
    public int GetNineSlotWholePointOfMonster(string unitInstanceID)
    {
        var equipments = Stones.GetEquippingStones(unitInstanceID);
        string a1 = null, a2 = null, a3 = null, b1 = null, b2 = null, b3 = null, c1 = null, c2 = null, c3 = null;
        for (var i = 0; i < equipments.Count; i++)
        {
            switch (equipments[i].slot)
            {
                case "1":
                    a1 = equipments[i].SkillId;
                    break;
                case "2":
                    a2 = equipments[i].SkillId;
                    break;
                case "3":
                    a3 = equipments[i].SkillId;
                    break;
                case "4":
                    b1 = equipments[i].SkillId;
                    break;
                case "5":
                    b2 = equipments[i].SkillId;
                    break;
                case "6":
                    b3 = equipments[i].SkillId;
                    break;
                case "7":
                    c1 = equipments[i].SkillId;
                    break;
                case "8":
                    c2 = equipments[i].SkillId;
                    break;
                case "9":
                    c3 = equipments[i].SkillId;
                    break;
            }
        }
        int wholePoint = SkillSet.SkillBalancePoint(a1, a2, a3, b1, b2, b3, c1, c2, c3);
        return wholePoint;
    }
}
