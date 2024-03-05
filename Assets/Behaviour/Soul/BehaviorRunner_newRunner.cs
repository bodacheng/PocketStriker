using System.Collections.Generic;
using System.Linq;
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
            if (dreamComboStart)
            {
                dreamComboStart = false;
                return fixedSkillSequence.FirstOrDefault();
            }
            
            var index = fixedSkillSequence.IndexOf(currentSKillEntity);
            if (index == -1)
                return null;
            if (index < fixedSkillSequence.Count - 1)
            {
                var nextSkillOnSequence = fixedSkillSequence[index + 1];
                return nextSkillOnSequence;
            }
            return null;
        }

        bool SequenceFengLiuShuiZhuan()
        {
            var current = fixedSkillSequence.IndexOf(currentSKillEntity);
            if (processingProcessedSequenceIndex == fixedSkillSequence.Count - 1 && processingProcessedSequenceIndex != current)
            {
                processingProcessedSequenceIndex = -1;
                return false;
            }
            
            processingProcessedSequenceIndex = current;
            return processingProcessedSequenceIndex != -1;
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
            var List = new List<SkillEntity>();
            var currentSKillEntity = new SkillEntity();
            
            if (_nowBehavior != null)
            {
                if (_nowBehavior.StateKey == "Empty")
                {
                    SkillEntityDic.TryGetValue("Move", out currentSKillEntity);
                }else
                    SkillEntityDic.TryGetValue(_nowBehavior.StateKey, out currentSKillEntity);
            }
            if (currentSKillEntity == null)
                return List;
            
            foreach (var _Key in currentSKillEntity.CasualTo)
            {
                BehaviourDic.TryGetValue(_Key, out _tryBehavior);
                if (!_tryBehavior.Capacity_enter_condition())
                {
                    continue;
                }
                SkillEntityDic.TryGetValue(_Key, out _tempSKillEntity);
                List.Add(_tempSKillEntity);
            }
            return List;
        }
        
        public (float, float) CalAdviceDistanceFromEnemy()
        {
            float min = 9999f;
            float max = 0f;
            for (var index = 0; index < currentSKillEntity.CasualTo.Length; index++)
            {
                BehaviourDic.TryGetValue(currentSKillEntity.CasualTo[index], out var state);
                
                if (state.StateType == BehaviorType.CT || state.StateType == BehaviorType.GM ||
                    state.StateType == BehaviorType.GI || state.StateType == BehaviorType.GR)
                {
                    if (min > state.triggerAttackRangeMin)
                        min = state.triggerAttackRangeMin;
                    if (max < state.triggerAttackRangeMax)
                        max = state.triggerAttackRangeMax;
                }
            }
            return (min, max);
        }
    }
}
