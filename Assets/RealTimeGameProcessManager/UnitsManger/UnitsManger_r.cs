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
            var unit = ResolveInitialRotationUnit();
            foreach (var dataCenter in teamMembers.GetValues())
            {
                if (dataCenter == null)
                {
                    continue;
                }
                dataCenter.WholeT.parent = null;
                dataCenter.WholeT.gameObject.SetActive(true);
            }
            ChangeFightingUnit(unit, true, InitialStandPoint());
        }

        Data_Center ResolveInitialRotationUnit()
        {
            Data_Center fallback = null;
            int fallbackKey1 = int.MaxValue;
            int fallbackKey2 = int.MaxValue;

            for (var i = 0; i < 3; i++)
            {
                var dataCenter = teamMembers.Get(0, i);
                if (IsAvailableRotationUnit(dataCenter))
                {
                    return dataCenter;
                }
            }

            foreach (var pair in teamMembers.mDict)
            {
                var dataCenter = pair.Value;
                if (!IsAvailableRotationUnit(dataCenter))
                {
                    continue;
                }

                var key1 = pair.Key.Item1;
                var key2 = pair.Key.Item2;
                if (fallback == null || key1 < fallbackKey1 || (key1 == fallbackKey1 && key2 < fallbackKey2))
                {
                    fallback = dataCenter;
                    fallbackKey1 = key1;
                    fallbackKey2 = key2;
                }
            }

            return fallback;
        }

        bool IsAvailableRotationUnit(Data_Center dataCenter)
        {
            return dataCenter != null
                   && dataCenter.FightDataRef != null
                   && !dataCenter.FightDataRef.IsDead.Value;
        }

        Transform InitialStandPoint()
        {
            return TeamStandPoints != null && TeamStandPoints.Length > 0 ? TeamStandPoints[0] : null;
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
                float hpRate = AdventureModeRules.GetRotationMemberHpRate(teamHpRate, evolutionMode, index);
                center.Step3Initialize(teamConfig, teamCGMode, aiMode, aiDelayFrame, aiTriggerDreamComboRateCondition, hpRate, RTFightManager.Target.UnitInfoRef[center]);
                
                center.FightDataRef.IsDead.Subscribe(x =>
                {
                    if (x)
                    {
                        Sensor.AddOrRemoveSharedDeadUnitInfo(center, teamConfig.myTeam, true);
                        Sensor.AddOrRemoveSharedUnitInfo(center, teamConfig.myTeam, false);
                        if (teamConfig.myTeam == Team.player2 && FightLoad.Fight.EvolutionMode)
                        {
                            // Heal as soon as an opponent is defeated, including the final
                            // opponent whose death skips the next evolution choice.
                            var hero = RTFightManager.Target.team1.RMode_Unit.Value;
                            if (hero != null && !hero.FightDataRef.IsDead.Value
                                             && hero.FightDataRef.CurrentHp.Value > 0)
                                hero.FightDataRef.CurrentHp.Value = hero.FightDataRef.MaxHp;
                        }
                        if (FightLogger.value.GetWinnerTeam() == Team.none)
                        {
                            if (teamConfig.myTeam == Team.player2 && FightLoad.Fight.EvolutionMode)
                            {
                                BeginEvolutionChoice();
                            }
                            else
                            {
                                ToNewUnit(2);
                            }
                        }

                        var fight = FightLoad.Fight;
                        var corpse = center.WholeT;
                        var corpseGeometry = center.geometryCenter;
                        bool CanHideCorpse() => this != null && ReferenceEquals(FightLoad.Fight, fight)
                            && center != null && corpse != null && corpseGeometry != null
                            && center.WholeT == corpse && center.geometryCenter == corpseGeometry
                            && center.FightDataRef != null && center.FightDataRef.IsDead.Value;
                        var disposable = new SerialDisposable();

// 假设你有一个 Observable<bool> 的布尔值监控（例如：boolObservable），这个值在变化时会发出事件。
                        var boolObservable = Observable.EveryUpdate()
                            .Where(_ => CanHideCorpse() && center._BasicPhysicSupport != null && center._BasicPhysicSupport.AtRing)
                            .Take(1)  // 只获取第一次触发的事件
                            .Select(_ => Unit.Default);  // 转换为 Unit 类型

                        var timerObservable = Observable.Timer(TimeSpan.FromSeconds(1))
                            .Select(_ => Unit.Default);  // Timer 也转换为 Unit 类型

// 使用 Observable.Amb<Unit>，谁先触发就执行哪个
                        disposable.Disposable = Observable.Amb<Unit>(boolObservable, timerObservable)
                            .Subscribe(async (_) =>
                            {
                                try
                                {
                                    if (!CanHideCorpse()) return;
                                    await EffectsManager.GenerateEffect(CommonSetting.MemberShiftEffectCode, null,
                                        corpseGeometry.position, Quaternion.identity, null);
                                    if (!CanHideCorpse()) return;
                                    corpse.gameObject.SetActive(false);
                                }
                                catch (OperationCanceledException) when (!CanHideCorpse()) { }
                                finally { disposable.Dispose(); }
                            }).AddTo(center);
                        RTFightManager.Target.CameraAdjustment(RTFightManager.playerTeam, RTFightManager.Target.team1.TeamMode, FightLoad.Fight.EventType);
                    }
                }).AddTo(gameObject);
            }
        }

        void BeginEvolutionChoice()
        {
            var manager = RTFightManager.Target;
            var hero = manager.team1.RMode_Unit.Value;
            var fightingLayer = FightingStepLayer.Open();
            BattleEffectLifetime.InvalidateAll(true);
            hero._MyBehaviorRunner.ChangeToWaitingState();
            foreach (var stick in fightingLayer.GetComponentsInChildren<UltimateJoystick>(true))
                stick.UpdatePositioning();
            manager.team1.InputsManager.FocusUnit(null);
            fightingLayer.gameObject.SetActive(false);

            // Lock controls immediately, leaving a real-time beat for the defeat
            // before the skill choices appear. Fight exit disposes this timer.
            Observable.Timer(TimeSpan.FromSeconds(1), Scheduler.MainThreadIgnoreTimeScale)
                .Subscribe(_ =>
                {
                    if (manager == null || manager != RTFightManager.Target || hero == null
                        || fightingLayer == null || hero.FightDataRef.IsDead.Value
                        || FightLogger.value.GameOver.Value
                        || FSceneProcessesRunner.Main.currentProcess is not FightingProcess)
                        return;

                    BattleEffectLifetime.InvalidateAll(true);
                    manager.EvolutionManager.EvolutionCount++;
                    string bottomText = "";
                    switch (manager.EvolutionManager.EvolutionCount)
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

                    var inBattleEvolution = UILayerLoader.Load<InBattleEvolution>();
                    inBattleEvolution.Setup(hero, () =>
                        {
                            BattleEffectLifetime.InvalidateAll();
                            UILayerLoader.Remove<InBattleEvolution>();
                            ToNewUnit(0);
                            switch (manager.EvolutionManager.EvolutionCount)
                            {
                                case 1:
                                    manager.team2.RMode_Unit.Value.FightDataRef.CriticalGaugeMode = CriticalGaugeMode.Normal;
                                    break;
                                case 2:
                                    manager.team2.RMode_Unit.Value.FightDataRef.CriticalGaugeMode = CriticalGaugeMode.DoubleGain;
                                    break;
                                case 3:
                                    manager.team2.RMode_Unit.Value.FightDataRef.CriticalGaugeMode = CriticalGaugeMode.Unlimited;
                                    break;
                            }

                            fightingLayer.gameObject.SetActive(true);
                            manager.team1.InputsManager.FocusUnit(hero);
                            var battleCamera = manager._CameraManager.CurrentBattleCamera;
                            if (battleCamera != null) battleCamera.CanSetH = true;
                        }, Translate.Get("ChooseYourEvolution"), bottomText);
                }).AddTo(manager.Disposables);
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
                    targetPos = RMode_Unit.Value.geometryCenter != null
                        ? RMode_Unit.Value.geometryCenter.position
                        : RMode_Unit.Value.WholeT.position;
                    targetRot = RMode_Unit.Value.WholeT.rotation;
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
                    PlaceUnitByGeometryCenter(RMode_Unit.Value, targetPos, targetRot);
                    
                    if (emptyState)
                    {
                        RMode_Unit.Value._MyBehaviorRunner.ChangeState("Empty");
                    }
                    else
                    {
                        RMode_Unit.Value._MyBehaviorRunner.ChangeToWaitingState();
                    }
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

            if (emptyState)
            {
                RTFightManager.Target?.FacePreparedTeamsTowardEachOther();
            }
            
            //Refresh(TeamMembers);
            return unitChanged;
        }

        public void UnitStartOff()
        {
            if (RMode_Unit.Value == null)
            {
                ChangeFightingUnit(ResolveInitialRotationUnit(), true, InitialStandPoint());
            }

            RMode_Unit.Value?._MyBehaviorRunner?.ChangeToWaitingState();

            if (teamConfig.myTeam == RTFightManager.playerTeam && InputsManager != null && RMode_Unit.Value != null)
            {
                InputsManager.FocusUnit(RMode_Unit.Value, true);
            }
        }
        
        // 计算时间统计可上场角色，更新上场冷却图标UI
        void WaitUnitChange()
        {
            if (FSceneProcessesRunner.Main.CurrentStep() == SceneStep.Fighting)
            {
                var refreshTimes = RTFightManager.Target.RefreshTimeDic;
                foreach (var member in teamMembers.mDict.Values)
                {
                    if (member == null || !refreshTimes.TryGetValue(member, out var refreshTime))
                        continue;
                    if (refreshTime.Value > 0)
                        refreshTime.Value -= Time.deltaTime;
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
                    ChangeFightingUnit(waitingMember, true, InitialStandPoint());
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
