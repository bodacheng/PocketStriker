using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HittingDetection;
using Skill;

namespace Soul
{
    public partial class BehaviorRunner : MonoBehaviour
    {
        #region 初始化相关
        public List<SkillEntity> skillEntityList;
        BehaviorsIncubator _statesIncubator;
        #endregion
        
        #region 辅助模块：技能链接时机判断器
        public SkillCancelFlag _SkillCancelFlag;
        #endregion
        
        #region 运行时活参数
        public readonly SingleFightLog SingleFightLog = new SingleFightLog();
        public IDictionary<string, Behavior> BehaviourDic = new Dictionary<string, Behavior>();
        public IDictionary<string, SkillEntity> SkillEntityDic;//大状态机真正的运行依据，其他内容都是为了生成它而存在的中间变量
        public SkillEntity currentSKillEntity;
        SkillEntity _tempSKillEntity;
        
        public readonly List<SkillEntity> fixedSkillSequence = new List<SkillEntity>();
        public Func<bool> AITriggerDreamComboRateCondition;

        #region 辅助模块：控制器

        private readonly Controller _controller = new Controller();
        public Controller Controller => _controller;
        #endregion

        private readonly Empty_State _emptyState = new Empty_State();
        Behavior _nowBehavior;
        Behavior _lastBehavior;
        Behavior _tryBehavior;
        Behavior _commandWaitingState;//所谓的待机状态。和首发状态分开处理，因为有实际作用的技能肯定要优先释放，没有的话才进行一些移动等等。
        public Behavior CommandWaitingState => _commandWaitingState;
        #endregion

        public AIMode AIMode { get; set; }

        public MobileInputsManager InputsManager
        {
            get;
            set;
        }
        
        public bool BeingControl()
        {
            return InputsManager!= null && InputsManager.Inputting;
        }
        
        void Awake()
        {
            _nowBehavior = _emptyState;   
        }
        
        public bool AI { set; get; }

        public bool IfRunning()
        {
            return _nowBehavior != _emptyState;
        }
        
        public Behavior GetNowState()
        {
            return _nowBehavior;
        }
        public Behavior GetLastState()
        {
            return _lastBehavior;
        }

        public Behavior GetState(string key)
        {
            BehaviourDic.TryGetValue(key, out var state);
            return state;
        }

        public bool OnFixedSequence => onFixedSequence;
        private bool onFixedSequence = false;
        private bool dreamComboStart = false;

        public Func<bool> SuperComboCondition { get; set; }

        public bool SuperComboStrategyCondition()
        {
            var first = fixedSkillSequence.FirstOrDefault();
            if (first == null)
                return false;
            for (var y = 0; y < AllConditionCodes.Count; y++)
            {
                var _condition = AllConditionCodes[y];
                if (ConditionAndRespond[_condition].Contains(first.REAL_NAME))
                {
                    BehaviourDic.TryGetValue(first.REAL_NAME, out var tryBehavior);
                    if (tryBehavior.CheckTriggerCondition(_condition))
                        return true;
                }
            }
            return false;
        }
        
        public Action sequenceBeginAct;
        private Action sequenceEndAct;
        public void RegisterSequenceCommand(Action onStart, Action onEnd)
        {
            this.sequenceBeginAct = onStart;
            this.sequenceEndAct = onEnd;
        }
        
        public void StartOffSequenceEngine()
        {
            var first = fixedSkillSequence.FirstOrDefault();
            if (first != null)
            {
                dreamComboStart = true;
                onFixedSequence = true;
                _SkillCancelFlag.Cancel_Flag = true;
                InputsManager?.SkillExplosion(InputKey.DreamCombo, 3);
            }
        }

        void Update()
        {
            if (IfRunning())
            {
                optionsForButtonRefresh.Clear();
                if (!ForceTransitionEngine())
                {
                    if (!onFixedSequence)
                        BehaviourTransitionEngine(); // 梦幻连段开始的时候应该是有单独的函数和这个并列
                    else
                        BehaviourSequenceEngine();
                }
                
                #region 按钮技能刷新
                InputsManager?.ButtonsFeatureLoad(optionsForButtonRefresh);
                #endregion
                
                #region 决策制定
                if (!onFixedSequence)
                    _controller.Decision(this, _canTranTo, AI && !BeingControl()); // 梦幻连段开始的时候应该是有单独的函数和这个并列
                else
                {
                    _controller.RunFixedSequence(this, _canTranTo);
                }
                #endregion
                
                _nowBehavior?._State_Update();
            }
        }
        
        void FixedUpdate()
        {
            if (IfRunning())
            {
                if (_nowBehavior != null)
                {
                    if (AI && !BeingControl())
                    {
                        _nowBehavior._State_FixedUpdate1();
                        _nowBehavior._State_FixedUpdate2();
                    }
                    else
                    {
                        _nowBehavior._c_State_FixedUpdate1();
                        _nowBehavior._c_State_FixedUpdate2();
                    }
                }
            }
        }
        
        void EndSequence()
        {
            processingProcessedSequenceIndex = -1;
            this.sequenceEndAct?.Invoke();
            onFixedSequence = false;
        }
        
        private int processingProcessedSequenceIndex = -1;
        public void ChangeState(string num)
        {
            _SkillCancelFlag.turn_off_flag();
            BehaviourDic.TryGetValue(num, out _tryBehavior);
            _nowBehavior?.AI_State_exit();

            //注意看changeState环节，上一个状态的exit和下一个状态的enter是同一个帧执行的。
            //从这里我们曾经发现了动画播放模块一个重要问题，就是在特定情况下，
            //比如defend状态的exit里有PlayLayerAnim(_animator_layer_index, null)，防御后接攻击，
            //那么先执行PlayLayerAnim(_animator_layer_index, null) ，同一帧执行PlayLayerAnim(_animator_layer_index, clip_name);
            //就会产生bug：动画器无法正常播放攻击动画，角色会立在那里。这是我们动画模块的一个性质。
            // 我们把defend状态exit中的PlayLayerAnim(_animator_layer_index, null)删除了后就不再产生对应bug。
            // 关于动画模块的“技能动作清空”，我们是把它放在了move状态的开头，从而避免了清空函数与触发动画函数在同一帧执行。
            _lastBehavior = _nowBehavior;
            _nowBehavior = _tryBehavior;
            SkillEntityDic.TryGetValue(_nowBehavior.StateKey, out currentSKillEntity);
            
            if (onFixedSequence)
            {
                if (!SequenceFengLiuShuiZhuan())
                {
                    EndSequence();
                }
            }
            
            if (AI && !BeingControl())
            {
                _nowBehavior.AI_State_enter();
            }
            else
            {
                _nowBehavior.C_State_enter();
            }
        }
        
        public void ChangeState(string num, V_Damage damage)
        {
            BehaviourDic.TryGetValue(num, out _tryBehavior);
            _nowBehavior?.AI_State_exit();
            
            _lastBehavior = _nowBehavior;
            _nowBehavior = _tryBehavior;
            SkillEntityDic.TryGetValue(_nowBehavior.StateKey, out currentSKillEntity);
            
            if (onFixedSequence)
            {
                if (!SequenceFengLiuShuiZhuan())
                {
                    EndSequence();
                }
            }
            
            if (AI && !BeingControl())
                _nowBehavior.AI_State_enter(damage);
            else
                _nowBehavior.C_State_enter(damage);
        }

        public void ChangeToWaitingState()
        {
            BehaviourDic.TryGetValue(_commandWaitingState.StateKey, out _tryBehavior);
            if (_tryBehavior != GetNowState())//避免战斗待机状态重复进入
            {
                ChangeState(_commandWaitingState.StateKey);
            }
        }
        
        public void ChangeToTestMode()
        {
            BehaviourDic.TryGetValue(_commandWaitingState.StateKey, out _tryBehavior);
            var move_State = (Move_State)_tryBehavior;
            move_State.AIMoveStyle = Move_State.AIMoveMode.Test;
            ChangeToWaitingState();
        }
      
        public void INIStates(Data_Center data_Center)
        {
            if (BehaviourDic == null)
            {
                Debug.Log("严重错误");
                return;
            }
            foreach (KeyValuePair<string, Behavior> s in BehaviourDic)
            {
                s.Value._DATA_CENTER = data_Center;
                s.Value.Pre_process_before_enter();
            }

            BehaviourDic.TryGetValue("Empty", out _nowBehavior);
            
            if (!AI)
            {
                _nowBehavior.C_State_enter();
            }
            else
            {
                _nowBehavior.AI_State_enter();
            }
        }       
    }
}