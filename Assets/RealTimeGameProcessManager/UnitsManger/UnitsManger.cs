using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

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

                    if (autoChanged && aiChanged && dataCenter.FightDataRef != null && !dataCenter.FightDataRef.IsDead.Value)
                    {
                        dataCenter._MyBehaviorRunner.ChangeToWaitingState();
                    }
                }

                Debug.Log(
                    $"[UnitsManger.Auto] team={teamConfig?.myTeam} mode={TeamMode} auto={_auto} focus={InputsManager?.CurrentFocus?.Value?.name ?? "null"}",
                    this
                );
            }
            get => _auto;
        }
        
        public async UniTask _UnitsLoad(MultiDic<int, int, UnitInfo> membersSets, IDictionary<Data_Center, UnitInfo> unitInfoRef)
        {
            async UniTask LoadOneUnit(int key1, int key2, UnitInfo info)
            {
                var center = teamMembers.Get(key1, key2);
                if (center == null)
                {
                    center = await UnitCreator.CreateUnit(info, 1);
                }
                teamMembers.Set(key1, key2, center);
                DicAdd<Data_Center, UnitInfo>.Add(unitInfoRef, center, info);
            }
            var tasks = new List<UniTask>();
            foreach (var kv in membersSets.mDict)
            {
                tasks.Add(LoadOneUnit(kv.Key.Item1, kv.Key.Item2, kv.Value));
            }
            await UniTask.WhenAll(tasks);
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
            if (dataCenter == null || dataCenter.WholeT == null)
            {
                return;
            }

            if (dataCenter.geometryCenter == null)
            {
                dataCenter.WholeT.SetPositionAndRotation(targetGeometryCenterPosition, targetRotation);
                return;
            }

            // geometryCenter is the battle-space anchor used by camera, sensor, and hit logic.
            var rootScale = dataCenter.WholeT.lossyScale;
            var localCenterPoint = dataCenter.WholeT.InverseTransformPoint(dataCenter.geometryCenter.position);
            var centerOffset = Vector3.Scale(localCenterPoint, rootScale);
            dataCenter.WholeT.SetPositionAndRotation(targetGeometryCenterPosition - targetRotation * centerOffset, targetRotation);
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
