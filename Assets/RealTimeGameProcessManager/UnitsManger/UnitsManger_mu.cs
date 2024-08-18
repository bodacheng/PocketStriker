using System;
using UnityEngine;
using UniRx;

namespace FightScene
{
    public partial class UnitsManger : MonoBehaviour
    {
        public void AllUnitsStartOff(bool testMode = false)
        {
            foreach (var member in teamMembers.GetValues())
            {
                Sensor.AddOrRemoveSharedUnitInfo(member, teamConfig.myTeam, true);
                if (!testMode)
                    member._MyBehaviorRunner.ChangeToWaitingState();
                else
                {
                    member._MyBehaviorRunner.ChangeToTestMode();
                }
            }
        }

        public void ToStartPosMulti()
        {
            Data_Center unit = null;
            foreach (var kv in teamMembers.mDict)
            {
                var dataCenter = teamMembers.Get(kv.Key.Item1, kv.Key.Item2);
                if (dataCenter == null)
                {
                    continue;
                }

                if (unit == null)
                    unit = kv.Value;
                if (TeamStandPoints[kv.Key.Item2] != null)
                {
                    dataCenter.WholeT.transform.position = TeamStandPoints[kv.Key.Item2].position;
                    dataCenter.WholeT.transform.rotation = TeamStandPoints[kv.Key.Item2].rotation;
                    dataCenter.WholeT.parent = null;
                    dataCenter.WholeT.gameObject.SetActive(true);
                }
                else
                {
                    Debug.Log("站位逻辑错误。出现了系统未安排的站位点");
                }
            }
        }

        public void InitializeMulti(float teamHpRate, CriticalGaugeMode teamCGMode, AIMode aiMode, int aiDelayFrame,
            Func<bool> AITriggerDreamComboRateCondition)
        {
            foreach (var center in teamMembers.GetValues())
            {
                center.Step3Initialize(teamConfig, teamCGMode, aiMode, aiDelayFrame, AITriggerDreamComboRateCondition,
                    teamHpRate, RTFightManager.Target.UnitInfoRef[center]);
                center.FightDataRef.IsDead.Subscribe(x =>
                {
                    if (x)
                    {
                        Sensor.AddOrRemoveSharedDeadUnitInfo(center, teamConfig.myTeam, true);
                        Sensor.AddOrRemoveSharedUnitInfo(center, teamConfig.myTeam, false);

                        var disposable = new SerialDisposable();

                        // 这里假设你有一个 Observable<bool> 的布尔值监控（例如：boolObservable），这个值在变化时会发出事件。
                        var boolObservable = Observable.EveryUpdate()
                            .Where(_ => center._BasicPhysicSupport.AtRing)
                            .Take(1)
                            .Select(_ => Unit.Default); // 将布尔值转为 Unit 类型

                        var timerObservable =
                            Observable.Timer(TimeSpan.FromSeconds(1)).Select(_ => Unit.Default); // Timer 也转换为 Unit 类型
                        // 使用 Observable.Amb<Unit>，谁先触发就执行哪个
                        disposable.Disposable = Observable.Amb<Unit>(boolObservable, timerObservable)
                            .Subscribe(async (_) =>
                            {
                                if (center != null)
                                {
                                    await EffectsManager.GenerateEffect(CommonSetting.MemberShiftEffectCode, null,
                                        center.geometryCenter.position, Quaternion.identity, null);
                                    center.WholeT.gameObject.SetActive(false);
                                    if (InputsManager.CurrentFocus.Value == center)
                                    {
                                        InputsManager.FocusUnit(null);
                                    }
                                }
                                disposable.Dispose();
                            }).AddTo(center);
                    }
                });
            }
        }
    }
}