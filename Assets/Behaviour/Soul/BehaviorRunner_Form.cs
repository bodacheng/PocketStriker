using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Skill;

namespace Soul
{
    public partial class BehaviorRunner : MonoBehaviour
    {
        public readonly IDictionary<string, List<string>> ConditionAndRespond = new Dictionary<string, List<string>>();
        public readonly MultiDic<string, string, int> ConditionAndRespondPriority = new MultiDic<string, string, int>();// 注意，这个字典是value越小代表有限度越高
        public readonly IDictionary<string, string> BehaviourAndStrategicExitCondition = new Dictionary<string, string>();
        public List<string> AllConditionCodes;

        void RegisterConditionToRespond(KeyValuePair<string, string> conditionAndBehaviourCode)//string target_beheviour,string condition_code
        {
            if (ConditionAndRespond.ContainsKey(conditionAndBehaviourCode.Key))
            {
                ConditionAndRespond[conditionAndBehaviourCode.Key].Add(conditionAndBehaviourCode.Value);
            }
            else{
                ConditionAndRespond.Add(conditionAndBehaviourCode.Key,new List<string>() { conditionAndBehaviourCode.Value});
            }
        }

        void AddAITriggerConditionToBehavior(SkillEntity behaviorDefine)
        {
            switch(behaviorDefine.StateType)
            {
                case BehaviorType.MV: //移动状态的触发条件。不添加的话移动状态不触发的。
                    var fdsgfg = new KeyValuePair<string, string>("SpareOption", behaviorDefine.REAL_NAME);
                    RegisterConditionToRespond(fdsgfg);
                    ConditionAndRespondPriority.Set(fdsgfg.Key, fdsgfg.Value, 10);
                    
                    break;
                case BehaviorType.AC:
                    //KeyValuePair<string, string> keyValuePair1 = new KeyValuePair<string, string>("LosingDefendStrength", behavior_define.REAL_NAME);
                    //RegisterConditionToRespond(keyValuePair1);
                    //ConditionAndRespondPriority.Set(keyValuePair1.Key, keyValuePair1.Value, 1);

                    //KeyValuePair<string, string> keyValuePair2 = new KeyValuePair<string, string>("DangerousNearby", behavior_define.REAL_NAME);
                    //RegisterConditionToRespond(keyValuePair2);
                    //ConditionAndRespondPriority.Set(keyValuePair2.Key,keyValuePair2.Value, 3);

                    //KeyValuePair<string, string> keyValuePair3 = new KeyValuePair<string, string>("DangerousClose", behavior_define.REAL_NAME);
                    //RegisterConditionToRespond(keyValuePair3);
                    //ConditionAndRespondPriority.Set(keyValuePair3.Key, keyValuePair3.Value, 3);

                    var keyValuePair4 = new KeyValuePair<string, string>("DangerousVeryClose", behaviorDefine.REAL_NAME);
                    RegisterConditionToRespond(keyValuePair4);
                    ConditionAndRespondPriority.Set(keyValuePair4.Key, keyValuePair4.Value, 3);
                    
                    BehaviourAndStrategicExitCondition.Add(behaviorDefine.REAL_NAME, null);
                    break;
                case BehaviorType.CT:
                    var keyValuePair_ct = new KeyValuePair<string, string>("CT", behaviorDefine.REAL_NAME);
                    RegisterConditionToRespond(keyValuePair_ct);
                    ConditionAndRespondPriority.Set(keyValuePair_ct.Key, keyValuePair_ct.Value, 1);
                    
                    var keyValuePair_ct2 = new KeyValuePair<string, string>("EnemyClose", behaviorDefine.REAL_NAME);
                    RegisterConditionToRespond(keyValuePair_ct2);
                    ConditionAndRespondPriority.Set(keyValuePair_ct2.Key, keyValuePair_ct.Value, 3);
                    
                    BehaviourAndStrategicExitCondition.Add(behaviorDefine.REAL_NAME, null);
                    break;
                case BehaviorType.Def:
                    var keyValuePair_def1 = new KeyValuePair<string, string>("DangerousVeryClose", behaviorDefine.REAL_NAME);
                    RegisterConditionToRespond(keyValuePair_def1);
                    ConditionAndRespondPriority.Set(keyValuePair_def1.Key, keyValuePair_def1.Value, 2);
                    
                    BehaviourAndStrategicExitCondition.Add(behaviorDefine.REAL_NAME, "TimeToRespond");
                    break;
                case BehaviorType.GR:
                    var keyValuePair = new KeyValuePair<string, string>("TimeToAttack", behaviorDefine.REAL_NAME);
                    RegisterConditionToRespond(keyValuePair);
                    ConditionAndRespondPriority.Set(keyValuePair.Key, keyValuePair.Value, 2);

                    KeyValuePair<string, string> eretet = new KeyValuePair<string, string>("TimeToAttack_Reluctant", behaviorDefine.REAL_NAME);
                    RegisterConditionToRespond(eretet);
                    ConditionAndRespondPriority.Set(eretet.Key, eretet.Value, 3);

                    BehaviourAndStrategicExitCondition.Add(behaviorDefine.REAL_NAME, null);
                    break;
                case BehaviorType.GI:
                    var keyValuePairuu = new KeyValuePair<string, string>("TimeToAttack", behaviorDefine.REAL_NAME);
                    RegisterConditionToRespond(keyValuePairuu);
                    ConditionAndRespondPriority.Set(keyValuePairuu.Key, keyValuePairuu.Value, 2);
                    
                    var fgerte = new KeyValuePair<string, string>("TimeToAttack_Reluctant", behaviorDefine.REAL_NAME);
                    RegisterConditionToRespond(fgerte);
                    ConditionAndRespondPriority.Set(fgerte.Key, fgerte.Value, 3);
                    
                    BehaviourAndStrategicExitCondition.Add(behaviorDefine.REAL_NAME, null);
                    break;
                case BehaviorType.GM:
                    var keyValuePairqq = new KeyValuePair<string, string>("TimeToAttack", behaviorDefine.REAL_NAME);
                    RegisterConditionToRespond(keyValuePairqq);
                    ConditionAndRespondPriority.Set(keyValuePairqq.Key, keyValuePairqq.Value, 2);
                    
                    var fdsfr = new KeyValuePair<string, string>("TimeToAttack_Reluctant", behaviorDefine.REAL_NAME);
                    RegisterConditionToRespond(fdsfr);
                    ConditionAndRespondPriority.Set(fdsfr.Key, fdsfr.Value, 3);

                    BehaviourAndStrategicExitCondition.Add(behaviorDefine.REAL_NAME, null);
                    break;
            }
        }

        public void FormFightingSetsByNineAndTwo(SkillSet nineAndTwo)
        {
            nineAndTwo.SortNineAndTwo();
            //这上下两个函数之间存在一个chuanEndCasualT0的问题，从而必须一前一后紧密连接，下次review时候可以看看代码能不能整更利索一些。
            SkillEntityDic = nineAndTwo.GenerateBehaviourSets();
            skillEntityList = nineAndTwo.SkillEntityList();//这一行于本游戏本身已经无用，但该列表牵扯到开发环境下角色技能详细的显示，以及框架本身保存xml战斗脚本的功能。
            _statesIncubator = new BehaviorsIncubator(_emptyState, SkillEntityDic);
            var behaviorDic = _statesIncubator.BehaviorDic; // 理解整个系统的关键
            BehaviourDic.Clear();
            ConditionAndRespondPriority.Clear();
            BehaviourAndStrategicExitCondition.Clear();
            foreach (var s in behaviorDic)
            {
                if (SkillEntityDic.ContainsKey(s.Key))
                {
                    s.Value.StateKey = SkillEntityDic[s.Key].REAL_NAME;
                    s.Value.spLevel = SkillEntityDic[s.Key].SP_LEVEL;
                    s.Value.triggerAttackRangeMin = SkillEntityDic[s.Key].AIAttrs.AI_MIN_DIS;
                    s.Value.triggerAttackRangeMax = SkillEntityDic[s.Key].AIAttrs.AI_MAX_DIS;
                    s.Value.TriggerAttackHeight = SkillEntityDic[s.Key].AIAttrs.height;
                    AddAITriggerConditionToBehavior(SkillEntityDic[s.Key]);
                    BehaviourDic.Add(new KeyValuePair<string, Behavior>(s.Key, s.Value));

                    if (SkillEntityDic[s.Key].SP_LEVEL > 0)
                    {
                        fixedSkillSequence.Add(SkillEntityDic[s.Key]);
                    }
                }
                else
                {
                    //Debug.Log("没用上的key？：" + s.num);
                }
            }
            AllConditionCodes = ConditionAndRespond.Keys.ToList();
            _commandWaitingState = BehaviourDic[nineAndTwo.GetM_STS().REAL_NAME];
        }
        
        public void SetAt(float level)
        {
            foreach (var kv in BehaviourDic)
            {
                if (kv.Value.SkillConfig == null)
                    continue;
                kv.Value.Attack = FightGlobalSetting.ATCal(kv.Value.SkillConfig.ATTACK_WEIGHT, level);
            }
        }
    }
}
