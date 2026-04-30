using System.Collections.Generic;
using MCombat.Shared.Behaviour;
using UnityEngine;
using Skill;

namespace Soul
{
    public partial class BehaviorRunner : MonoBehaviour
    {
        public List<SkillEntity> optionsForButtonRefresh = new List<SkillEntity>();
        readonly List<SkillEntity> _canTranTo = new List<SkillEntity>(); //可以启动的技能的列表
        readonly List<string> _forcedTransitions = new List<string>();
        
        bool ForceTransitionEngine()
        {
            _forcedTransitions.Clear();
            if (currentSKillEntity.ForcedTransitions != null)
            {
                for (var i = 0; i < currentSKillEntity.ForcedTransitions.Length; i++)
                {
                    BehaviourDic.TryGetValue(currentSKillEntity.ForcedTransitions[i], out _tryBehavior);
                    if (_tryBehavior.Force_enter_condition())
                    {
                        _forcedTransitions.Add(currentSKillEntity.ForcedTransitions[i]);
                    }
                }
            }
            if (_forcedTransitions.Count > 0)
            {
                ChangeState(_forcedTransitions[0]);
                return true; // Once a state is forced to trigger, there is no need for the rest of codes to run at this frame
            }
            return false;
        }
        
        void BehaviourTransitionEngine()
        {
            _canTranTo.Clear();
            #region 查找已经可以触发的后续技能
            foreach (var key in currentSKillEntity.CasualTo)
            {
                BehaviourDic.TryGetValue(key, out _tryBehavior);
                if (!_tryBehavior.Capacity_enter_condition())
                {
                    continue;
                }
                SkillEntityDic.TryGetValue(key, out _tempSKillEntity);
                optionsForButtonRefresh.Add(_tempSKillEntity);
                if ((_tempSKillEntity.CAN_BE_CANCELLED_TO && _SkillCancelFlag.Cancel_Flag) || _nowBehavior.Capacity_Exit_Condition())
                {
                    _canTranTo.Add(_tempSKillEntity);
                }
            }
            #endregion
        }
        
        SkillEntity GetNextSkillEntityOnSequence()
        {
            if (_sequenceIndex + 1 < fixedSkillSequence.Count)
            {
                var nextSkillOnSequence = fixedSkillSequence[_sequenceIndex + 1];
                return nextSkillOnSequence;
            }
            return null;
        }
        
        void BehaviourSequenceEngine()
        {
            _canTranTo.Clear();
            var nextSkillOnSequence = GetNextSkillEntityOnSequence();
            if (nextSkillOnSequence != null)
            {
                optionsForButtonRefresh.Add(nextSkillOnSequence);
                if ((nextSkillOnSequence.CAN_BE_CANCELLED_TO && _SkillCancelFlag.Cancel_Flag) || _nowBehavior.Capacity_Exit_Condition())
                {
                    _canTranTo.Add(nextSkillOnSequence);
                }
            }
        }
        
        // 获取接下来等待释放的技能，并非是真正可触发技能，但反应了是否够气
        public List<SkillEntity> GetNextSkills()
        {
            SkillEntity currentSKillEntity = null;
            if (_nowBehavior != null)
            {
                if (_nowBehavior.StateKey == "Empty")
                {
                    SkillEntityDic.TryGetValue("Move", out currentSKillEntity);
                }else
                    SkillEntityDic.TryGetValue(_nowBehavior.StateKey, out currentSKillEntity);
            }

            return BehaviorTransitionQueryUtility.GetAvailableNextSkills(
                currentSKillEntity,
                SkillEntityDic.TryGetValue,
                CanEnterBehavior);
        }
        
        public (float, float) CalAdviceDistanceFromEnemy()
        {
            return BehaviorTransitionQueryUtility.CalculateAdviceDistance(currentSKillEntity, TryResolveBehaviorRange);
        }

        bool CanEnterBehavior(string key)
        {
            return BehaviourDic.TryGetValue(key, out var behavior) && behavior.Capacity_enter_condition();
        }

        bool TryResolveBehaviorRange(string key, out BehaviorRangeInfo rangeInfo)
        {
            if (BehaviourDic.TryGetValue(key, out var state))
            {
                rangeInfo = new BehaviorRangeInfo(
                    state.StateType,
                    state.triggerAttackRangeMin,
                    state.triggerAttackRangeMax);
                return true;
            }

            rangeInfo = default;
            return false;
        }
    }
}
