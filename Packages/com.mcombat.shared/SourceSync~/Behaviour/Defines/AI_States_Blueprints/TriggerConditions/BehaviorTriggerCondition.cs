using System;
using System.Collections.Generic;
using MCombat.Shared.Behaviour;
using UnityEngine;

namespace Soul
{
    public abstract partial class Behavior
    {
        Collider tempCollider1;

        public bool SpareOption()
        {
            return true;
        }

        public bool LosingDefendStrength() // Dash_Back_State G_Ani_MoveEscape_State 1
        {
            return BehaviorTriggerConditionUtility.IsLosingDefendStrength(
                _AIStateRunner.GetNowState().StateKey == "Defend",
                FightParamsRef.Resistance.Value);
        }

        public bool DangerousNearby() // Dash_Back_State G_Ani_MoveEscape_State 2
        {
            return BehaviorTriggerConditionUtility.IsDangerousNearby(
                Sensor.GetSuddenThreatInRange(0, 5) != null,
                FightParamsRef.Resistance.Value);
        }

        public bool DangerousClose() //Counter_State 1 2 3
        {
            return Sensor.GetSuddenThreatInRange(0, 3) != null;
        }

        public bool CounterComingEnergy()
        {
            var nearestEnemyMeat = Sensor.GetTargetRangeEnemyCollider(0, 5);
            var threat = Sensor.GetSuddenThreatInRange(5, 15);
            return BehaviorTriggerConditionUtility.IsCounterComingEnergy(
                nearestEnemyMeat.Count,
                threat != null);
        }

        public bool CT()
        {
            return BehaviorTriggerConditionUtility.CanCounter(OnBuff(), DangerousVeryClose());
        }

        public bool OnBuff()
        {
            return _DATA_CENTER.buffsRunner.MySubMissions.Count > 0;
        }


        public bool DangerousVeryClose() //CT
        {
            if (FightParamsRef.Resistance.Value > 0)
            {
                return false;
            }
            tempCollider1 = Sensor.GetSuddenThreatInRange(0, 5);
            return BehaviorTriggerConditionUtility.IsDangerousVeryClose(
                FightParamsRef.Resistance.Value,
                tempCollider1 != null);
        }

        public bool EnemyClose()
        {
            var colliders = Sensor.GetTargetRangeEnemyCollider(0, 5);
            return BehaviorTriggerConditionUtility.HasEnemyClose(colliders.Count);
        }

        public bool TimeToAttack()
        {
            // temp
            // if (Sensor.EnemyAndTeammateBetweenMeAndEnemy() != null)
            // {
            //     return false;
            // }

            // 从移动状态到攻击的话技能释放范围要求精准，但连招情况明明敌人在眼前但因为按技能最好范围而言“不够远”而不释放的话，会很奇怪
            //if (_AIStateRunner.GetNowState() == _AIStateRunner.commandWaitingState)
                var targetRangeEnemyColliders = Sensor.GetTargetRangeEnemyCollider(triggerAttackRangeMin, triggerAttackRangeMax);
            //else
                //tar = Sensor.GetTargetRangeEnemyCollider(Mathf.Clamp(triggerAtttackRangeMin - 3f, 0, triggerAtttackRangeMin - 3f), triggerAtttackRangeMax);
            return BehaviorTriggerConditionUtility.HasColliderForAttackHeight(
                targetRangeEnemyColliders,
                TriggerAttackHeight);
        }

        public bool TimeToAttack_Reluctant()
        {
            // if (Sensor.EnemyAndTeammateBetweenMeAndEnemy() != null)
            // {
            //     return false;
            // }

            // 从移动状态到攻击的话技能释放范围要求精准，但连招情况明明敌人在眼前但因为按技能最好范围而言“不够远”而不释放的话，会很奇怪
            //if (_AIStateRunner.GetNowState() == _AIStateRunner.commandWaitingState)
            var targetRangeEnemyColliders = Sensor.GetTargetRangeEnemyCollider(0, triggerAttackRangeMax);
            //else
                //tar = Sensor.GetTargetRangeEnemyCollider(Mathf.Clamp(triggerAtttackRangeMin - 3f, 0, triggerAtttackRangeMin - 3f), triggerAtttackRangeMax);

            return BehaviorTriggerConditionUtility.HasColliderForAttackHeight(
                targetRangeEnemyColliders,
                TriggerAttackHeight);
        }

        public bool TimeToRespond()
        {
            Collider threat = Sensor.GetSuddenThreatInRange(0, 5);
            return BehaviorTriggerConditionUtility.TimeToRespond(threat != null);
        }

        public bool TimeToStopRunning() //没有意义的条件。
        {
            Collider nearestEnemyMeat = Sensor.GetClosestEnemyColliderInSensorRange();
            return BehaviorTriggerConditionUtility.ShouldStopRunning(
                nearestEnemyMeat != null,
                nearestEnemyMeat != null
                    ? Vector3.Distance(nearestEnemyMeat.transform.position, this._DATA_CENTER.WholeT.position)
                    : 0f,
                Sensor.GetSuddenThreatInRange(0, 8) != null);
        }

        // 缓存方法名与委托的映射
        private static readonly Dictionary<string, Func<Behavior, bool>> _methodCache = new Dictionary<string, Func<Behavior, bool>>();
        public bool CheckTriggerCondition(string conditionFunctionName)
        {
            return BehaviorTriggerConditionUtility.InvokeCondition(this, _methodCache, conditionFunctionName);
        }

        public bool CheckExitCondition(string stateKey)
        {
            _AIStateRunner.BehaviourAndStrategicExitCondition.TryGetValue(stateKey, out string exitCondition);
            return BehaviorTriggerConditionUtility.CheckExitCondition(
                exitCondition,
                TimeToRespond,
                TimeToStopRunning);
        }
    }
}
