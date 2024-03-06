using UnityEngine;
using System.Collections.Generic;
using Skill;
using System.Linq;
using System;
using mainMenu;

public partial class SkillConfigTable
{
    public static List<SkillConfig> RowsToSkillConfigList(List<Row> Rows)
    {
        var skillConfigs = new List<SkillConfig>();
        for (var i = 0; i < Rows.Count; i++)
        {
            var aiRow = SkillAIAttrs.Find_RECORD_ID(Rows[i].RECORD_ID);
            var newConfig = RowToSkillConfig(Rows[i], aiRow);
            if (newConfig != null)
                skillConfigs.Add(newConfig);
                
            if (!LegalStateType(Rows[i].ATTACK_TYPE))
            {
                Debug.Log("崩溃级错误，技能Type有错：RECORDID"+ Rows[i].RECORD_ID);
            }
        }
        return skillConfigs;
    }

    public static Row SkillConfigToRow(SkillConfig skillConfig)
    {
        if (skillConfig == null)
            return null;
        Row row = new Row
        {
            RECORD_ID = skillConfig.RECORD_ID,
            TYPE = skillConfig.TYPE,
            REAL_NAME = skillConfig.REAL_NAME,
            ATTACK_WEIGHT = skillConfig.ATTACK_WEIGHT.ToString(),
            HP_WEIGHT = skillConfig.HP_WEIGHT.ToString()
        };
        
        switch (skillConfig.STATE_TYPE)
        {
            case BehaviorType.GR:
                row.ATTACK_TYPE = "GR";
                break;
            case BehaviorType.GI:
                row.ATTACK_TYPE = "GI";
                break;
            case BehaviorType.GM:
                row.ATTACK_TYPE = "GM";
                break;
            case BehaviorType.CT:
                row.ATTACK_TYPE = "CT";
                break;
            case BehaviorType.NONE:
                row.ATTACK_TYPE = "NONE";
                break;
        }
        
        if (!LegalStateType(row.ATTACK_TYPE))
        {
            Debug.Log("崩溃级错误，技能Type有错：RECORDID"+ skillConfig.RECORD_ID);
        }
                        
        switch (skillConfig.SP_LEVEL)
        {
            case 0:
                row.SP_LEVEL = "0";
                break;
            case 1:
                row.SP_LEVEL = "1";
                break;
            case 2:
                row.SP_LEVEL = "2";
                break;
            case 3:
                row.SP_LEVEL = "3";
                break;
            default:
                row.SP_LEVEL = "-1";
                break;
        }
        row.EVENT_CODE = skillConfig.EVENT_CODE;
        return row;
    }
    
    static SkillConfig RowToSkillConfig(Row row, SkillAIAttrs.Row aiRow)
    {
        try
        {
            var skillConfig = new SkillConfig
            {
                TYPE = row.TYPE,
                RECORD_ID = row.RECORD_ID,
                REAL_NAME = row.REAL_NAME,
                ATTACK_WEIGHT = float.Parse(row.ATTACK_WEIGHT),
                HP_WEIGHT = float.Parse(row.HP_WEIGHT)
            };
            
            switch (row.ATTACK_TYPE)
            {
                case "GR":
                    skillConfig.STATE_TYPE = BehaviorType.GR;
                    break;
                case "GI":
                    skillConfig.STATE_TYPE = BehaviorType.GI;
                    break;
                case "GM":
                    skillConfig.STATE_TYPE = BehaviorType.GM;
                    break;
                case "CT":
                    skillConfig.STATE_TYPE = BehaviorType.CT;
                    break;
                case "NONE":
                    skillConfig.STATE_TYPE = BehaviorType.NONE;
                    break;
            }
            
            if (!LegalStateType(row.ATTACK_TYPE))
            {
                Debug.Log("崩溃级错误，技能Type有错：RECORDID"+ skillConfig.RECORD_ID);
            }
            
            skillConfig.AIAttrs.AI_MIN_DIS = float.Parse(aiRow.TRIGGER_DIS_MIN);
            skillConfig.AIAttrs.AI_MAX_DIS = float.Parse(aiRow.TRIGGER_DIS_MAX);
            skillConfig.AIAttrs.height = int.Parse(aiRow.TRIGGER_HEIGHT);
            
            switch(row.SP_LEVEL)
            {
                case "0":
                    skillConfig.SP_LEVEL = 0;
                    break;
                case "1":
                    skillConfig.SP_LEVEL = 1;
                    break;
                case "2":
                    skillConfig.SP_LEVEL = 2;
                    break;
                case "3":
                    skillConfig.SP_LEVEL = 3;
                    break;
                default:
                    skillConfig.SP_LEVEL = -1;
                    break;
            }
            skillConfig.SHOW_NAME = SkillNameTable.GetSkillName(row.RECORD_ID);
            skillConfig.EVENT_CODE = row.EVENT_CODE;
            return skillConfig;
        }
        catch(Exception e)
        {
            Debug.Log(e);
            Debug.Log(row.REAL_NAME);
            return null;
        }
    }
        
    public static IDictionary<string,string> GetSkillIDAndNameDic(SkillStonesBox.StoneFilterForm filterForm)
    {
        var skillIDAndNameDic = new Dictionary<string, string>();
        var list = GetSkillConfigsOfType(filterForm.Type);
        foreach (var one in list)
        {
            if (filterForm.BType != BehaviorType.NONE && filterForm.BType != one.STATE_TYPE)
                continue;
            if (!SkillConfig.RangeLimit(one.AIAttrs.AI_MIN_DIS, one.AIAttrs.AI_MAX_DIS, filterForm.Close,
                filterForm.Near, filterForm.Far) || !filterForm.ExType.ToList().Contains(one.SP_LEVEL)) 
                continue;
            
            if (!skillIDAndNameDic.ContainsKey(one.RECORD_ID))
            {
                skillIDAndNameDic.Add(one.RECORD_ID, one.REAL_NAME);
            }
            else
            {
                Debug.Log("重复的技能ID？？："+one.RECORD_ID);
            }
        }
        return skillIDAndNameDic;
    }
}
