using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using MCombat.Shared.Combat;

namespace FightScene
{
    public partial class UnitsManger : MonoBehaviour
    {
        public MultiDic<int, int, Data_Center> teamMembers;
        
        public TeamMode TeamMode;
        public TeamConfig teamConfig;
        public Transform[] TeamStandPoints;
        
        public MobileInputsManager InputsManager
        {
            set;
            get;
        }
        
        private bool _auto;
        public bool Auto
        {
            set
            {
                var autoChanged = _auto != value;
                _auto = value;
                foreach (var dataCenter in teamMembers.GetValues())
                {
                    if (dataCenter == null || dataCenter._MyBehaviorRunner == null)
                    {
                        continue;
                    }

                    bool targetAIState;
                    if (TeamMode == TeamMode.Rotation)
                    {
                        targetAIState = _auto;
                    }
                    else if (TeamMode == TeamMode.MultiRaid)
                    {
                        if (this.teamConfig.myTeam == RTFightManager.playerTeam)
                        {
                            if (InputsManager != null && InputsManager.CurrentFocus != null && InputsManager.CurrentFocus.Value == dataCenter)
                            {
                                targetAIState = _auto;
                            }
                            else
                            {
                                targetAIState = true;
                            }
                        }
                        else
                        {
                            targetAIState = _auto;
                        }
                    }
                    else
                    {
                        targetAIState = dataCenter._MyBehaviorRunner.AI;
                    }

                    var aiChanged = dataCenter._MyBehaviorRunner.AI != targetAIState;
                    dataCenter._MyBehaviorRunner.AI = targetAIState;

                    // Preparing configures control before Step3 and the countdown.
                    // Only an in-fight toggle may restart a fighter's behavior.
                    if (autoChanged && aiChanged && FSceneProcessesRunner.Main.currentProcess is FightingProcess
                        && dataCenter.FightDataRef != null && !dataCenter.FightDataRef.IsDead.Value)
                    {
                        dataCenter._MyBehaviorRunner.ChangeToWaitingState();
                    }
                }

            }
            get => _auto;
        }
        
        public async UniTask _UnitsLoad(MultiDic<int, int, UnitInfo> membersSets, IDictionary<Data_Center, UnitInfo> unitInfoRef,
            Action<float> onUnitProgressDelta = null, int maxConcurrentLoads = 1)
        {
            async UniTask LoadOneUnit(int key1, int key2, UnitInfo info, int preloadCount, Vector3 stagingPosition)
            {
                float unitProgress = 0f;
                void ReportProgress(float progress)
                {
                    var delta = Mathf.Clamp01(progress) - unitProgress;
                    if (delta <= 0f)
                    {
                        return;
                    }
                    unitProgress += delta;
                    onUnitProgressDelta?.Invoke(delta);
                }

                var center = teamMembers.Get(key1, key2);
                if (center == null)
                {
                    center = await UnitCreator.CreateUnit(info, preloadCount, ReportProgress, stagingPosition);
                }
                else
                {
                    ReportProgress(1f);
                }
                if (center == null)
                    throw new InvalidOperationException($"Could not prepare unit: {info.r_id}");
                teamMembers.Set(key1, key2, center);
                DicAdd<Data_Center, UnitInfo>.Add(unitInfoRef, center, info);
            }
            var sameUnitCounts = new Dictionary<(string rId, float level), int>();
            foreach (var kv in membersSets.mDict)
            {
                var unitKey = (kv.Value.r_id, kv.Value.level);
                if (sameUnitCounts.TryGetValue(unitKey, out var count))
                {
                    sameUnitCounts[unitKey] = count + 1;
                }
                else
                {
                    sameUnitCounts.Add(unitKey, 1);
                }
            }

            maxConcurrentLoads = Mathf.Max(1, maxConcurrentLoads);
            var tasks = new List<UniTask>(maxConcurrentLoads);
            bool secondTeam = RTFightManager.Target != null && RTFightManager.Target.team2 == this;
            int stagingOrdinal = 0;
            foreach (var kv in membersSets.mDict)
            {
                var unitKey = (kv.Value.r_id, kv.Value.level);
                // 这个统计相同种类角色的逻辑并不精确。如果一个队伍里有两个同masterid角色，等级还一样，问题就出来了，但现在我们的代码构造造成没有别的做法。
                tasks.Add(LoadOneUnit(kv.Key.Item1, kv.Key.Item2, kv.Value, sameUnitCounts[unitKey],
                    PreparationStagingPosition(stagingOrdinal++, secondTeam)));
                if (tasks.Count < maxConcurrentLoads)
                {
                    continue;
                }

                await UniTask.WhenAll(tasks);
                tasks.Clear();
            }

            if (tasks.Count > 0)
            {
                await UniTask.WhenAll(tasks);
            }
        }

        static Vector3 PreparationStagingPosition(int ordinal, bool secondTeam)
        {
            // Models must remain active while their animation overrides are prepared.
            // Spawning every collider at the origin makes large teams spend their
            // loading frames in quadratic contact simulation. Separate both teams
            // from the arena and one another until the normal starting placement.
            const int columns = 16;
            const float spacing = 64f;
            return new Vector3(4096f + (secondTeam ? 2048f : 0f) + (ordinal % columns) * spacing,
                64f, 4096f + (ordinal / columns) * spacing);
        }

        public bool IfAllUnitsPreparedForBattle()
        {
            foreach (var oneMember in teamMembers.GetValues())
            {
                if (!oneMember.IfPreparedForBattle())
                    return false;
            }
            return true;
        }
        
        public void LocalUpdate()
        {
            switch (TeamMode)
            {
                case TeamMode.MultiRaid:
                    break;
                case TeamMode.Rotation:
                    WaitUnitChange();
                    break;
            }
        }
        
        public List<Transform> GetFightingUnitTs()
        {
            var transforms = new List<Transform>();
            switch (TeamMode)
            {
                case TeamMode.MultiRaid:
                    foreach (var unit in teamMembers.GetValues())
                    {
                        if (unit._MyBehaviorRunner.GetNowState().StateKey != "Death")
                        {
                            transforms.Add(unit.geometryCenter);
                        }
                    }
                    return transforms;
                case TeamMode.Rotation:
                    if (RMode_Unit.Value != null && RMode_Unit.Value._MyBehaviorRunner.GetNowState().StateKey != "Death")
                    {
                        transforms = new List<Transform>
                        {
                            RMode_Unit.Value.geometryCenter
                        };
                    }
                    return transforms;
            }
            return transforms;
        }

        public Transform GetRModeUnitT()
        {
            if (RMode_Unit.Value != null)
            {
                return RMode_Unit.Value.geometryCenter;
            }
            return null;
        }

        void PlaceUnitAtStandPoint(Data_Center dataCenter, Transform standPoint)
        {
            if (standPoint == null)
            {
                return;
            }

            PlaceUnitByGeometryCenter(dataCenter, standPoint.position, standPoint.rotation);
        }

        void PlaceUnitByGeometryCenter(Data_Center dataCenter, Vector3 targetGeometryCenterPosition, Quaternion targetRotation)
        {
            StopPlacementTweens(dataCenter);
            CombatPlacementUtility.PlaceRootByGeometryCenter(
                dataCenter?.WholeT,
                dataCenter?._BasicPhysicSupport?.Rigidbody,
                dataCenter?.geometryCenter,
                targetGeometryCenterPosition,
                targetRotation);
            var body = dataCenter?._BasicPhysicSupport?.Rigidbody;
            if (body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        public void FacePreparedUnitsToward(UnitsManger opponent)
        {
            if (opponent == null)
            {
                return;
            }

            CombatPlacementUtility.FaceRootsTowards(
                teamMembers.GetValues(),
                opponent.teamMembers.GetValues(),
                IsPreparedFacingUnit,
                center => center?.WholeT,
                center => center?.geometryCenter,
                center => center?._BasicPhysicSupport?.Rigidbody,
                StopPlacementTweens);
        }

        static bool IsPreparedFacingUnit(Data_Center dataCenter)
        {
            return dataCenter != null
                   && dataCenter.WholeT != null
                   && dataCenter.WholeT.gameObject.activeSelf
                   && dataCenter.FightDataRef != null
                   && !dataCenter.FightDataRef.IsDead.Value;
        }

        static void StopPlacementTweens(Data_Center dataCenter)
        {
            if (dataCenter?.WholeT == null)
            {
                return;
            }

            DG.Tweening.DOTween.Kill(dataCenter.WholeT);
        }
        
        // 全队无敌
        public void TurnAllUnitsInvincible(bool _Invincible)
        {
            foreach (var center in teamMembers.GetValues())
            {
                center.FightDataRef.Invincible = _Invincible;
            }
        }
        
        public void Clear()
        {
            foreach (var one in teamMembers.GetValues())
            {
                Destroy(one.WholeT.gameObject);
            }
            teamMembers.Clear();
        }
    }
}
