using System;
using System.Collections.Generic;
using System.Linq;
using Skill;
using Random = UnityEngine.Random;

// 20200226
// 整个状态迁移系统进行了大翻修，而最初的动机就是想到如果AI系统要具备一定自我改善能力的话，最起码AI系统自身应该更独立。
// 目前AI的特点是根据事前登录好的若干条件组与各个条件组下登录的行为列表进行反应。
// 如果说将来这套AI系统要变的更高级，那么可能条件组并不是固定的，而是能在战斗过程中动态调整，动态自我追加和细化。
// 但目前来说我们还不需要。或许可以制作一个简单的通过logger分析条件组下各个行为有没有效益，并调整优先级的脚本，但这个点到为止。

namespace Soul
{
    public class Controller
    {
        private readonly SSIMultiDictionary _triggered = new SSIMultiDictionary();
        
        public void Decision(BehaviorRunner runner, List<SkillEntity> options, bool auto)
        {
            bool changed = false;
            
            #region 按键触发
            if (runner.InputsManager != null)
            {
                changed = BtnTrigger(runner, options, runner.InputsManager);
            }
            #endregion
            
            if (changed)
            {
                goto A;
            }
            
            #region AI决策
            if (auto)
            {
                changed = AI_RUNs(runner, options, runner.AITriggerDreamComboRateCondition);
            }
            #endregion
            
            A:
            if (!changed)
                AutoReset(runner);
            
            if (changed && runner.InputsManager != null && runner.InputsManager.CurrentFocus.Value != null)
            {
                runner.InputsManager.BtnRefreshFrames();
            }
        }

        public void RunFixedSequence(BehaviorRunner runner, List<SkillEntity> options)
        {
            bool changed = Sequence_RUNs(runner, options.FirstOrDefault());
            
            if (!changed)
                AutoReset(runner);
            
            if (changed && runner.InputsManager != null && runner.InputsManager.CurrentFocus.Value != null)
            {
                runner.InputsManager.BtnRefreshFrames();
            }
        }

        bool BtnTrigger(BehaviorRunner runner, List<SkillEntity> options, MobileInputsManager inputsManager)
        {
            // 主动退出当前状态的控制类条件是否激活
            if (!BehaviourExitInputTrigger(runner.currentSKillEntity, inputsManager))
            {
                return false;
            }

            if (inputsManager.dreamCombo && !runner.OnFixedSequence && runner.SuperComboCondition())
            {
                runner.StartOffSequenceEngine();
                return true;
            }
            
            for (var i = 0; i < options.Count; i++)
            {
                switch (options[i].EnterInput)
                {
                    case InputKey.Attack1:
                    if (inputsManager.attack)
                    {
                        inputsManager.SkillExplosion(options[i].EnterInput, options[i].SP_LEVEL);
                        runner.SingleFightLog.WriteLog(
                            new SingleFightLog.BehaviourFightRecord
                            {
                                AI_Decided = false,
                                stateKey = options[i].REAL_NAME,
                                whyIDidThis = null
                            }
                        );
                        runner.SingleFightLog.AnalysisLog(runner.ConditionAndRespondPriority);
                        runner.ChangeState(options[i].REAL_NAME);
                        return true;
                    }
                    break;
                    case InputKey.Attack2:
                    if (inputsManager.fire1)
                    {
                        inputsManager.SkillExplosion(options[i].EnterInput, options[i].SP_LEVEL);
                        runner.SingleFightLog.WriteLog(
                            new SingleFightLog.BehaviourFightRecord
                            {
                                AI_Decided = false,
                                stateKey = options[i].REAL_NAME,
                                whyIDidThis = null
                            }
                        );
                        runner.SingleFightLog.AnalysisLog(runner.ConditionAndRespondPriority);
                        runner.ChangeState(options[i].REAL_NAME);
                        return true;
                    }
                    break;
                    case InputKey.Attack3:
                    if (inputsManager.fire2)
                    {
                        inputsManager.SkillExplosion(options[i].EnterInput, options[i].SP_LEVEL);
                        runner.SingleFightLog.WriteLog(
                        new SingleFightLog.BehaviourFightRecord
                            {
                                AI_Decided = false,
                                stateKey = options[i].REAL_NAME,
                                whyIDidThis = null
                            }
                        );
                        runner.SingleFightLog.AnalysisLog(runner.ConditionAndRespondPriority);
                        runner.ChangeState(options[i].REAL_NAME);
                        return true;
                    }
                    break;
                    case InputKey.Acc:
                    if (inputsManager.acc)
                    {
                        runner.SingleFightLog.WriteLog(
                        new SingleFightLog.BehaviourFightRecord
                            {
                                AI_Decided = false,
                                stateKey = options[i].REAL_NAME,
                                whyIDidThis = null
                            }
                        );
                        runner.SingleFightLog.AnalysisLog(runner.ConditionAndRespondPriority);
                        runner.ChangeState(options[i].REAL_NAME);
                        return true;
                    }
                    break;
                    case InputKey.Defend:
                    if (inputsManager.defendButtonHover)
                    {
                        runner.SingleFightLog.WriteLog(
                        new SingleFightLog.BehaviourFightRecord
                            {
                                AI_Decided = false,
                                stateKey = options[i].REAL_NAME,
                                whyIDidThis = null
                            }
                        );
                        runner.SingleFightLog.AnalysisLog(runner.ConditionAndRespondPriority);
                        runner.ChangeState(options[i].REAL_NAME);
                        return true;
                    }
                    break;
                    case InputKey.Null:
                        if (!inputsManager.defendButtonHover && !inputsManager.acc && !inputsManager.fire2 && !inputsManager.fire1 && !inputsManager.attack)
                        {
                            runner.ChangeState(options[i].REAL_NAME);
                            return true;
                        }
                        break;
                }
            }
            return false;
        }

        // 状态的退出可以由特定的控制条件来决定时进行的判断。目前全项目只有防御这一种情况
        bool BehaviourExitInputTrigger(SkillEntity current, MobileInputsManager inputsManager)
        {
            switch(current.ExitInput)
            {
                case InputKey.Defend_Cancel:
                    return inputsManager.DefendExitTrigger();
                default:
                    return true;
            }
        }

        string _condition;
        List<(string, string)> _finalConditionStateKeySet = new List<(string, string)>();
        int _decisionDelayCount = 0;
        private int DecisionDelayCount
        {
            set
            {
                _decisionDelayCount = value;
                if (_decisionDelayCount > DecisionDelay)
                {
                    _decisionDelayCount = 0;
                }
            }
            get => _decisionDelayCount;
        }

        public int DecisionDelay
        {
            get;
            set;
        }
        
        bool AI_RUNs(BehaviorRunner behaviorRunner, List<SkillEntity> options, Func<bool> AITriggerDreamComboRateCondition = null) // AI根据目前可作出的行为作出选择
        {
            _triggered.Main.Clear();

            if (behaviorRunner.GetNowState().Strategic_exit_condition())
            {
                // 首先看超级连招的条件是不是满足。
                if (!behaviorRunner.OnFixedSequence)
                {
                    if ((AITriggerDreamComboRateCondition == null || AITriggerDreamComboRateCondition()) 
                        &&  behaviorRunner.SuperComboStrategyCondition() && behaviorRunner.SuperComboCondition())
                    {
                        behaviorRunner.StartOffSequenceEngine();
                        return true;
                    }
                }
                
                for (var y = 0; y < behaviorRunner.AllConditionCodes.Count; y++)
                {
                    _condition = behaviorRunner.AllConditionCodes[y];
                    for (var x = 0; x < options.Count; x++)
                    {
                        if (behaviorRunner.ConditionAndRespond[_condition].Contains(options[x].REAL_NAME))
                        {
                            behaviorRunner.BehaviourDic.TryGetValue(options[x].REAL_NAME, out var tryBehavior);
                            if (tryBehavior.CheckTriggerCondition(_condition))
                            {
                                _triggered.Main.Set(_condition, options[x].REAL_NAME, behaviorRunner.ConditionAndRespondPriority.Get(_condition, options[x].REAL_NAME));
                            }
                        }
                    }
                }
            }
            
            bool Delay()
            {
                if (behaviorRunner.AIMode == AIMode.Aggressive)
                    return true;
                DecisionDelayCount++;
                return DecisionDelayCount == 0;
            }
            
            if (_triggered.Main.GetValues().Count > 0 && Delay())
            {
                _finalConditionStateKeySet = _triggered.GiveOutMin();
                if (_finalConditionStateKeySet.Count > 0)
                {
                    var random = Random.Range(0, _finalConditionStateKeySet.Count);//这里虽然是随机但是毕竟随机的这几个选项在优先级上是相同的。
                    var se = behaviorRunner.SkillEntityDic[_finalConditionStateKeySet[random].Item2];
                    if (se.StateType == BehaviorType.AC || se.StateType == BehaviorType.CT || se.StateType == BehaviorType.Def
                        || se.StateType == BehaviorType.GI || se.StateType == BehaviorType.GM || se.StateType == BehaviorType.GR)
                    {
                        behaviorRunner.SingleFightLog.WriteLog(
                            new SingleFightLog.BehaviourFightRecord
                            {
                                AI_Decided = true,
                                stateKey = se.REAL_NAME,
                                whyIDidThis = _finalConditionStateKeySet[random].Item1
                            }
                        );
                        behaviorRunner.SingleFightLog.AnalysisLog(behaviorRunner.ConditionAndRespondPriority);
                    }
                    behaviorRunner.ChangeState(se.REAL_NAME);
                    behaviorRunner.InputsManager?.SkillExplosion(se.EnterInput, se.SP_LEVEL);
                    return true;
                }
            }
            return false;
        }
        
        bool Sequence_RUNs(BehaviorRunner behaviorRunner, SkillEntity option) // AI根据目前可作出的行为作出选择
        {
            if (option != null)
            {
                var index = behaviorRunner.fixedSkillSequence.IndexOf(option);
                if (index == 0)
                {
                    behaviorRunner.sequenceBeginAct?.Invoke();
                }
                behaviorRunner.ChangeState(option.REAL_NAME);
                behaviorRunner.InputsManager?.SkillExplosion(option.EnterInput, option.SP_LEVEL);
                return true;
            }
            return false;
        }

        void AutoReset(BehaviorRunner behaviorRunner)
        {
            if (behaviorRunner.GetNowState().StateKey != behaviorRunner.CommandWaitingState.StateKey && behaviorRunner.GetNowState().Capacity_Exit_Condition())
            {
                behaviorRunner.ChangeToWaitingState();
            }
        }
    }
}

