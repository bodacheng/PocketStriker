using System;
using UnityEngine;
using UniRx;
using Cysharp.Threading.Tasks;
using DummyLayerSystem;

namespace FightScene
{
    public partial class UnitsManger : MonoBehaviour
    {
        public ReactiveProperty<Data_Center> RMode_Unit = new ReactiveProperty<Data_Center>();
        Data_Center waitingMember;
        
        public void ToStartPosRotate()
        {
            Data_Center unit = null;
            for (int i = 0; i < 3; i++)
            {
                var dataCenter = teamMembers.Get(0,i);
                if (dataCenter == null)
                {
                    continue;
                }
                if (unit == null)
                    unit = dataCenter;
                dataCenter.WholeT.parent = null;
                dataCenter.WholeT.gameObject.SetActive(true);
            }
            ChangeFightingUnit(unit, true, TeamStandPoints[0]);
        }
        
        void ToNewUnit(int delayInSeconds)
        {
            var disposable = new SerialDisposable();
            disposable.Disposable = Observable.Timer(TimeSpan.FromSeconds(delayInSeconds)).Subscribe((_) =>
                {
                    RandomToAliveUnit();
                    disposable.Dispose();
                }).AddTo(RTFightManager.Target.Disposables);
        }
        
        public void TeamsIniRotate(float teamHpRate, CriticalGaugeMode teamCGMode, AIMode aiMode, int aiDelayFrame, 
            Func<bool> aiTriggerDreamComboRateCondition, bool evolutionMode = false)
        {
            var list = teamMembers.GetValues();
            for (var index = 0; index < list.Count; index++)
            {
                var center = list[index];
                //  时间刷新整备
                RTFightManager.Target.RefreshTimeDic.Add(center, new ReactiveProperty<float>(0));
                if (!evolutionMode)
                    center.Step3Initialize(teamConfig, teamCGMode, aiMode, aiDelayFrame, aiTriggerDreamComboRateCondition, teamHpRate, RTFightManager.Target.UnitInfoRef[center]);
                else
                {
                    float HPRate = index > 2 ? teamHpRate : 1;
                    center.Step3Initialize(teamConfig, teamCGMode, aiMode, aiDelayFrame, aiTriggerDreamComboRateCondition, HPRate, RTFightManager.Target.UnitInfoRef[center]);
                }
                
                center.FightDataRef.IsDead.Subscribe(x =>
                {
                    if (x)
                    {
                        Sensor.AddOrRemoveSharedDeadUnitInfo(center, teamConfig.myTeam, true);
                        Sensor.AddOrRemoveSharedUnitInfo(center, teamConfig.myTeam, false);
                        if (FightLogger.value.GetWinnerTeam() == Team.none)
                        {
                            if (teamConfig.myTeam == Team.player2 && FightLoad.Fight.EvolutionMode)
                            {
                                HitBoxesProcesser.Instance.AllProcessingFade();
                                RTFightManager.Target.team1.RMode_Unit.Value._MyBehaviorRunner.ChangeToWaitingState();
                                var inBattleEvolution = UILayerLoader.Load<InBattleEvolution>();
                                var fightingLayer = FightingStepLayer.Open();
                                fightingLayer.gameObject.SetActive(false);
                                RTFightManager.Target.team1.InputsManager.FocusUnit(null);
                                RTFightManager.Target.EvolutionManager.EvolutionCount++;
                                string bottomText = "";
                                switch (RTFightManager.Target.EvolutionManager.EvolutionCount)
                                {
                                    case 1:
                                        bottomText = Translate.Get("InBattleEvolutionInfo1");
                                        break;
                                    case 2:
                                        bottomText = Translate.Get("InBattleEvolutionInfo2");
                                        break;
                                    case 3:
                                        bottomText = Translate.Get("InBattleEvolutionInfo3");
                                        break;
                                }

                                inBattleEvolution.Setup(RTFightManager.Target.team1.RMode_Unit.Value, () =>
                                    {
                                        HitBoxesProcesser.Instance.AllProcessingFade();
                                        UILayerLoader.Remove<InBattleEvolution>();
                                        ToNewUnit(0);
                                        switch (RTFightManager.Target.EvolutionManager.EvolutionCount)
                                        {
                                            case 1:
                                                RTFightManager.Target.team2.RMode_Unit.Value.FightDataRef
                                                    .CriticalGaugeMode = CriticalGaugeMode.Normal;
                                                break;
                                            case 2:
                                                RTFightManager.Target.team2.RMode_Unit.Value.FightDataRef
                                                    .CriticalGaugeMode = CriticalGaugeMode.DoubleGain;
                                                break;
                                            case 3:
                                                RTFightManager.Target.team2.RMode_Unit.Value.FightDataRef
                                                    .CriticalGaugeMode = CriticalGaugeMode.Unlimited;
                                                break;
                                        }

                                        fightingLayer.gameObject.SetActive(true);
                                        RTFightManager.Target.team1.InputsManager.FocusUnit(RTFightManager.Target.team1
                                            .RMode_Unit.Value);
                                    },
                                    Translate.Get("ChooseYourEvolution"), bottomText);
                            }
                            else
                            {
                                ToNewUnit(2);
                            }
                        }

                        var disposable = new SerialDisposable();

// 假设你有一个 Observable<bool> 的布尔值监控（例如：boolObservable），这个值在变化时会发出事件。
                        var boolObservable = Observable.EveryUpdate()
                            .Where(_ => center._BasicPhysicSupport.AtRing)  // 当 bool 值为 true 时触发
                            .Take(1)  // 只获取第一次触发的事件
                            .Select(_ => Unit.Default);  // 转换为 Unit 类型

                        var timerObservable = Observable.Timer(TimeSpan.FromSeconds(1))
                            .Select(_ => Unit.Default);  // Timer 也转换为 Unit 类型

// 使用 Observable.Amb<Unit>，谁先触发就执行哪个
                        disposable.Disposable = Observable.Amb<Unit>(boolObservable, timerObservable)
                            .Subscribe(async (_) =>
                            {
                                if (center != null)
                                {
                                    await EffectsManager.GenerateEffect(CommonSetting.MemberShiftEffectCode, null,
                                        center.geometryCenter.position, Quaternion.identity, null);
                                    center.WholeT.gameObject.SetActive(false);
                                }

                                disposable.Dispose();
                            }).AddTo(center);
                        RTFightManager.Target.CameraAdjustment(RTFightManager.playerTeam, RTFightManager.Target.team1.TeamMode, FightLoad.Fight.EventType);
                    }
                }).AddTo(gameObject);
            }
        }

        public void TutorialSpecial()
        {
            foreach (var center in teamMembers.GetValues())
            {
                center.StartAutoModeWhenGetHurt();
            }
        }
        
        // 切换队员
        bool ChangeFightingUnit(Data_Center changeTo, bool emptyState = false, Transform iniStandPoint = null)
        {
            if (changeTo == null)
            {
                var returnValue = RMode_Unit.Value != changeTo;
                RMode_Unit.Value = changeTo;
                return returnValue;
            }
            
            if (changeTo.FightDataRef.IsDead.Value)
            {
                return false;
            }
            var unitChanged = false;
            var targetPos = Vector3.zero;
            var targetRot = Quaternion.identity;
            if (iniStandPoint != null)
            {
                targetPos = iniStandPoint.position;
                targetRot = iniStandPoint.rotation;
            }
            else
            {
                if (RMode_Unit.Value != null)
                {
                    targetPos = RMode_Unit.Value.transform.position;
                    targetRot = Quaternion.Euler(new Vector3(1,0,1));
                }
            }
            
            foreach (var dataCenter in teamMembers.GetValues())
            {
                if (dataCenter == null)
                {
                    continue;
                }
                if (changeTo == dataCenter)
                {
                    if (RMode_Unit.Value != null && changeTo != null) //继承hit数
                    {
                        Sensor.AddOrRemoveSharedUnitInfo(RMode_Unit.Value, teamConfig.myTeam, false);
                        changeTo.FightDataRef._comboHitCount.HitCount.Value = RMode_Unit.Value.FightDataRef._comboHitCount.HitCount.Value;
                    }
                    Sensor.AddOrRemoveSharedUnitInfo(changeTo, teamConfig.myTeam, true);
                    RMode_Unit.Value = changeTo;
                    RMode_Unit.Value.WholeT.gameObject.SetActive(true);
                    
                    if (emptyState)
                    {
                        RMode_Unit.Value._MyBehaviorRunner.ChangeState("Empty");
                    }
                    else
                    {
                        RMode_Unit.Value._MyBehaviorRunner.ChangeToWaitingState();
                    }
                    RMode_Unit.Value.WholeT.transform.position = targetPos;
                    RMode_Unit.Value.WholeT.transform.rotation = targetRot;
                    EffectsManager.GenerateEffect(CommonSetting.MemberShiftEffectCode, null, RMode_Unit.Value.WholeT.transform.position, Quaternion.identity, RMode_Unit.Value.geometryCenter).Forget();
                    unitChanged = true;
                }
                else
                {
                    if (dataCenter._MyBehaviorRunner.GetNowState().StateKey != "Empty")
                    {
                        dataCenter._MyBehaviorRunner.ChangeState("Empty");
                    }
                    dataCenter.WholeT.gameObject.SetActive(false);
                }
            }
            
            if (teamConfig.myTeam == RTFightManager.playerTeam && InputsManager != null)
            {
                InputsManager.FocusUnit(RMode_Unit.Value, true);
            }
            
            //Refresh(TeamMembers);
            return unitChanged;
        }

        public void UnitStartOff()
        {
            RMode_Unit.Value?._MyBehaviorRunner?.ChangeToWaitingState();
        }
        
        // 计算时间统计可上场角色，更新上场冷却图标UI
        void WaitUnitChange()
        {
            if (FSceneProcessesRunner.Main.CurrentStep() == SceneStep.Fighting)
            {
                for (var i = 0; i < teamMembers.GetValues().Count; i++)
                {
                    var member = teamMembers.GetValues()[i];
                    if (member == null || !RTFightManager.Target.RefreshTimeDic.ContainsKey(member))
                    {
                        continue;
                    }
                    if (RTFightManager.Target.RefreshTimeDic[member].Value > 0)
                    {
                        RTFightManager.Target.RefreshTimeDic[member].Value -= Time.deltaTime; // 角色切换倒计时;
                    }
                }
            
                if (waitingMember != null &&  RMode_Unit.Value != waitingMember && CanChangeToThisMember(waitingMember))
                {
                    if (RMode_Unit.Value != null && RTFightManager.Target.RefreshTimeDic.ContainsKey(RMode_Unit.Value))
                    {
                        RTFightManager.Target.RefreshTimeDic[RMode_Unit.Value].Value = 10f;
                    }
                    ChangeFightingUnit(waitingMember);
                    waitingMember = null;
                }
            }
            if (FSceneProcessesRunner.Main.CurrentStep() == SceneStep.CountDown)
            {
                if (waitingMember != null && RMode_Unit.Value != waitingMember)
                {
                    ChangeFightingUnit(waitingMember, true, TeamStandPoints[0]);
                }
            }
        }
        
        bool CanChangeToThisMember(Data_Center target)
        {
            if (target == null || target._MyBehaviorRunner == null || target.FightDataRef == null)
            {
                return false;
            }
            if (target == RMode_Unit.Value)
            {
                return false;
            }
            if (target.FightDataRef.IsDead.Value)
            {
                return false;
            }
            if (!RTFightManager.Target.RefreshTimeDic.ContainsKey(target))
            {
                return false;
            }
            if (RTFightManager.Target.RefreshTimeDic[target].Value > 0)
            {
                return false;
            }
            if (target._MyBehaviorRunner.GetNowState().StateType == Skill.BehaviorType.Hit || target._MyBehaviorRunner.GetNowState().StateType == Skill.BehaviorType.KnockOff)
            {
                return false;
            }
            if (target._MyBehaviorRunner.GetNowState().StateType == Skill.BehaviorType.GI || target._MyBehaviorRunner.GetNowState().StateType == Skill.BehaviorType.GM || target._MyBehaviorRunner.GetNowState().StateType == Skill.BehaviorType.GR)
            {
                if (!target._SkillCancelFlag.Cancel_Flag)
                    return true;
            }
            return true;
        }
        
        public void ReadyForNextMember(Data_Center next)
        {
            if (next == null)
            {
                return;
            }
            waitingMember = next;
        }
        
        bool RandomToAliveUnit()
        {
            if (waitingMember != null && waitingMember.FightDataRef.CurrentHp.Value > 0)
            {
                if (!waitingMember.FightDataRef.IsDead.Value)
                {
                    if (ChangeFightingUnit(waitingMember))
                    {
                        return true;
                    }
                }
            }

            foreach (var dataCenter in teamMembers.GetValues())
            {
                if (dataCenter == null)
                {
                    continue;
                }
                if (!dataCenter.FightDataRef.IsDead.Value)
                {
                    if (ChangeFightingUnit(dataCenter))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
    }
}
