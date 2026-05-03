using System.Collections.Generic;
using MCombat.Shared.Behaviour;
using Skill;

namespace Soul
{
    public class BehaviorsIncubator
    {
        public readonly IDictionary<string, Behavior> BehaviorDic;
        // 技能动画列表（不包括基础动画）
        public readonly List<string> SkillTypeKeys;

        public BehaviorsIncubator(Empty_State empty_State, SkillEntity moveState, IDictionary<string, SkillEntity> toFormAttackStateList)
        {
            var result = BehaviorStateDefinitionUtility.BuildBehaviorDictionary(
                empty_State,
                moveState,
                toFormAttackStateList,
                FightGlobalSetting.HasDefend,
                SkillConfigTable.GetSkillConfigByRecordId,
                CommonBehaviorStateFactories.Create());
            BehaviorDic = result.BehaviorDic;
            SkillTypeKeys = result.SkillTypeKeys;
        }
    }
}
