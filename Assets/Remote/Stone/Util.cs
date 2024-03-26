using mainMenu;
using System.Collections.Generic;
using UnityEngine;
using Skill;
using System.Linq;
using NoSuchStudio.Common;

// 配置文件属于资源信息，不是账户信息，应该分离开处理。
namespace dataAccess
{
    public static partial class Stones
    {
        #region 技能石模型相关
        // 把所有技能的等级显示出来
        public static void ShowAllStonesLevel(bool on)
        {
            foreach (var keyValuePair in RenderModelDic)
            {
                keyValuePair.Value.RenderStoneLevel(on);
            }
        }
        
        public static void ShowAllStonesLevel()
        {
            foreach (var keyValuePair in RenderModelDic)
            {
                keyValuePair.Value.RenderStoneLevel();
            }
        }
        #endregion

        #region 财产数据相关
        // 用于过滤显示在技能石盒内的技能石
        public static List<string> TargetStonesFromAccount(SkillStonesBox.StoneFilterForm filterForm, string skillEditFocusing)
        {
            var skillStonesOfTypeAndExType = new List<string>(); // instanceId list
            var passiveSkill = UnitPassiveTable.GetPassiveSKillRecordIds();
            foreach (var pair in Dic)
            {
                if (pair.Value.Born == "true" || passiveSkill.Contains(pair.Value.SkillId))
                {
                    continue; //原生技能不显示在技能石盒子内
                }
                var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(pair.Value.SkillId);
                if (skillConfig == null)
                {
                    Debug.Log("????"+ pair.Value.SkillId);
                    continue;
                }

                if (Units.Get(pair.Value.unitInstanceId) != null && skillEditFocusing != null && pair.Value.unitInstanceId != skillEditFocusing)
                {
                    continue;
                }
                
                var exs = filterForm.ExType.ToList();
                if (skillConfig.TYPE == filterForm.Type 
                    && exs.Contains(skillConfig.SP_LEVEL) 
                    && SkillConfig.RangeLimit(skillConfig.AIAttrs.AI_MIN_DIS, skillConfig.AIAttrs.AI_MAX_DIS, filterForm.Close, filterForm.Near, filterForm.Far))
                {
                    skillStonesOfTypeAndExType.Add(pair.Value.InstanceId);
                }
            }
            return skillStonesOfTypeAndExType;
        }
        
        // exceptList ： 除了这些 技能石账户ID
        // extraList ：额外添加这些 技能石账户ID
        public static List<string> TargetStonesFromAccount_except(string skillEditFocusing, SkillStonesBox.StoneFilterForm filterForm, List<string> exceptList, List<string> extraList, bool notUsing)
        {
            var filteredList = TargetStonesFromAccount(filterForm, skillEditFocusing);
            var returnValue = new List<string>();
            for (var i = 0; i < filteredList.Count; i++)
            {
                if (extraList != null && extraList.Contains(filteredList[i]))
                {
                    returnValue.Add(filteredList[i]);
                    continue;
                }
                var infoModel = Get(filteredList[i]);
                if (notUsing)
                {
                    if (Units.Get(infoModel.unitInstanceId) != null && infoModel.unitInstanceId != skillEditFocusing)
                    {
                        continue;
                    }
                }

                if ((exceptList == null || !exceptList.Contains(infoModel.InstanceId)))
                {
                    returnValue.Add(filteredList[i]);
                }
            }
            return returnValue;
        }
        
        // 从账户随机抽取符合要求的技能石
        // exceptSkIDs : 除了这些技能ID。切记是技能ID
        public static StoneOfPlayerInfo SearchStoneForRandomSetFromAccount(SkillStonesBox.StoneFilterForm filterForm, List<string> exceptSkIds)
        {
            var exceptStones = new List<string>();
            for (var i = 0; i < exceptSkIds.Count; i++)
            {
                var exceptAccIds = GetMyStonesBySkillID(exceptSkIds[i]);
                exceptStones.AddRange(exceptAccIds);
            }
            
            string focusingUnitInstanceId = null;
            if (ProcessesRunner.Main.currentProcess.Step == MainSceneStep.UnitSkillEdit)
                focusingUnitInstanceId = PreScene.target.Focusing.id;
            
            var stoneAccIDs = TargetStonesFromAccount_except(focusingUnitInstanceId, filterForm, exceptStones, null, true);
            if (stoneAccIDs.Count == 0)
                return null;
            var stoneAccID = stoneAccIDs.Random();
            var infoModel = Get(stoneAccID);
            return infoModel;
        }

        // 获取某个角色装备中的技能石列表应该是在已经读取了玩家所有技能石之后，这个过程从本地内存读就可以。我们只需要确保读取技能石，和下面这个函数总实质是一前一后。
        public static List<StoneOfPlayerInfo> GetEquippingStones(string instanceId)
        {
            if (Units.Get(instanceId) == null)
                return new List<StoneOfPlayerInfo>();
            
            var targetStones = new List<StoneOfPlayerInfo>();
            foreach(var pair in Dic)
            {
                if (pair.Value.unitInstanceId == instanceId)
                {
                    targetStones.Add(pair.Value);
                }
            }
            return targetStones;
        }
        
        // 获取一个角色的原生技能的对应技能石信息
        public static StoneOfPlayerInfo GetOriginSkillOfUnit(string instanceId)
        {
            StoneOfPlayerInfo targetStone = null;
            foreach(var keyValuePair in Dic)
            {
                if (keyValuePair.Value.unitInstanceId == instanceId && keyValuePair.Value.Born == "true")
                {
                    targetStone = keyValuePair.Value;
                }
            }
            return targetStone;
        }
        #endregion
    }
}