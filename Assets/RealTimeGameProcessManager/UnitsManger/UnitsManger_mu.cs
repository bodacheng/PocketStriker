using System;
using System.Collections.Generic;
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
            var authored = new List<Pose>();
            if (TeamStandPoints != null)
                foreach (var point in TeamStandPoints)
                    if (point != null) authored.Add(new Pose(point.position, point.rotation));
            if (authored.Count == 0) authored.Add(new Pose(transform.position, transform.rotation));
            int capacity = teamMembers.mDict.Count;
            foreach (var key in teamMembers.mDict.Keys) capacity = Mathf.Max(capacity, key.Item2 + 1);
            bool expanded = capacity > authored.Count;
            float spacing = 1f;
            if (expanded)
                foreach (var center in teamMembers.mDict.Values)
                    if (BattleFormationPlacement.TryGetBodyBounds(center, out var bounds))
                        spacing = Mathf.Max(spacing, Mathf.Max(bounds.size.x, bounds.size.z) + 0.2f);
            var formation = BattleFormationPlacement.Build(authored, capacity, spacing);
            int ordinal = 0;
            foreach (var kv in teamMembers.mDict)
            {
                var dataCenter = teamMembers.Get(kv.Key.Item1, kv.Key.Item2);
                if (dataCenter == null)
                {
                    continue;
                }

                int slot = expanded ? ordinal : kv.Key.Item2 >= 0 ? kv.Key.Item2 : ordinal;
                var pose = formation[Mathf.Clamp(slot, 0, formation.Length - 1)];
                // Preserve each original indexed point for normal/sparse teams.
                if (!expanded && TeamStandPoints != null && slot < TeamStandPoints.Length && TeamStandPoints[slot] != null)
                    pose = new Pose(TeamStandPoints[slot].position, TeamStandPoints[slot].rotation);
                dataCenter.WholeT.parent = null;
                PlaceUnitByGeometryCenter(dataCenter, pose.position, pose.rotation);
                dataCenter.WholeT.gameObject.SetActive(true);
                ordinal++;
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
                        if (FightLoad.Fight.IsGroupBattle)
                        {
                            FightingStepLayer.Open()?.SetSensor();
                        }

                        var fight = FightLoad.Fight;
                        var corpse = center.WholeT;
                        var corpseGeometry = center.geometryCenter;
                        bool CanHideCorpse() => this != null && ReferenceEquals(FightLoad.Fight, fight)
                            && center != null && corpse != null && corpseGeometry != null
                            && center.WholeT == corpse && center.geometryCenter == corpseGeometry
                            && center.FightDataRef != null && center.FightDataRef.IsDead.Value;
                        var disposable = new SerialDisposable();

                        // 这里假设你有一个 Observable<bool> 的布尔值监控（例如：boolObservable），这个值在变化时会发出事件。
                        var boolObservable = Observable.EveryUpdate()
                            .Where(_ => CanHideCorpse() && center._BasicPhysicSupport != null && center._BasicPhysicSupport.AtRing)
                            .Take(1)
                            .Select(_ => Unit.Default); // 将布尔值转为 Unit 类型

                        var timerObservable =
                            Observable.Timer(TimeSpan.FromSeconds(1)).Select(_ => Unit.Default); // Timer 也转换为 Unit 类型
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
                                    if (InputsManager != null && InputsManager.CurrentFocus.Value == center)
                                    {
                                        InputsManager.FocusUnit(null);
                                    }
                                }
                                catch (OperationCanceledException) when (!CanHideCorpse()) { }
                                finally { disposable.Dispose(); }
                            }).AddTo(center);
                    }
                });
            }
        }
    }
}
