using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using mainMenu;
using UnityEngine;

namespace dataAccess
{
    public static partial class Stones
    {
        public static void Add(StoneOfPlayerInfo one)
        {
            DicAdd<string, StoneOfPlayerInfo>.Add(Dic, one.InstanceId, one);
        }
        
        public static async UniTask RenderAll()
        {
            var stoneLoadTasks = new List<UniTask>();
            foreach (var kv in Dic)
            {
                stoneLoadTasks.Add(GenerateStoneModelByAccID(kv.Key));
            }
            await UniTask.WhenAll(stoneLoadTasks);
        }
        
        /// <summary>
        /// 生成账户用技能石图标，生成的模型会加入统一技能石字典作为备用
        /// </summary>
        /// <param name="instanceId">技能石账户id</param>
        static async UniTask GenerateStoneModelByAccID(string instanceId)
        {
            if (RenderModelDic.ContainsKey(instanceId))
            {
                if (RenderModelDic[instanceId] != null)
                    return;
            }
            var info = Get(instanceId);
            var item = await GenerateStoneModel(info.SkillId, true);

            if (item == null)
            {
                Debug.Log("info.SkillId："+ info.SkillId + " skill stone info not found？");
                return;
            }
            
            item.Inherent = info.Born == "true";
            item._SkillConfig = SkillConfigTable.GetSkillConfigByRecordId(Dic[instanceId].SkillId);
            item.gameObject.name = "stone_" + item._SkillConfig.TYPE + "_" + item._SkillConfig.REAL_NAME;
            item.instanceId = instanceId;
            item.gameObject.transform.SetParent(PreScene.target.stonesTempContainer);
            DicAdd<string, SKStoneItem>.Add(RenderModelDic, instanceId, item);
        }

        // 生成展示用技能石（额外模型）
        // 有两种模式，1: “账户技能石” 2 ：纯粹展示用技能石
        public static async UniTask<SKStoneItem> GenerateStoneModel(string skillID, bool openStoneFeature)
        {
            var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(skillID);
            if (skillConfig == null)
            {
                return null;
            }
            
            var ob = await SkillIcon.FindSkillIcon(skillID);
            if (ob == null)
                return null;
            ob.gameObject.name = "skillIcon_" + skillID;
            var item = ob.GetComponent<SKStoneItem>();
            if (item == null)
            {
                item = ob.AddComponent<SKStoneItem>();
            }
            item._SkillConfig = skillConfig;
            item.enabled = openStoneFeature;
            return item;
        }
    }
}