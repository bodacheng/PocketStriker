using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    public abstract partial class Behavior
    {
        Collider tempCollider1, tempCollider2;

        public bool SpareOption()
        {
            return true;
        }

        public bool LosingDefendStrength() // Dash_Back_State G_Ani_MoveEscape_State 1
        {
            return _AIStateRunner.GetNowState().StateKey == "Defend" && FightParamsRef.Resistance.Value < 2;
        }
        
        public bool DangerousNearby() // Dash_Back_State G_Ani_MoveEscape_State 2
        {
            return Sensor.GetSuddenThreatInRange(0 , 5) != null && FightParamsRef.Resistance.Value == 0;
        }
        
        public bool DangerousClose() //Counter_State 1 2 3
        {
            return Sensor.GetSuddenThreatInRange(0, 3) != null;
        }
        
        public bool CounterComingEnergy()
        {
            var nearestEnemyMeat = Sensor.GetTargetRangeEnemyCollider(0, 5);
            var threat = Sensor.GetSuddenThreatInRange(5, 15);
            return nearestEnemyMeat.Count == 0 && (threat != null);
        }
        
        public bool CT()
        {
            return !OnBuff() && DangerousVeryClose();
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
            //tempCollider2 = Sensor.GetClosestEnemyColliderInSensorRange();
            
            // if (tempCollider2 != null && tempCollider1 != null)
            // {
            //     if (Vector3.Distance(tempCollider2.transform.position, _DATA_CENTER.geometryCenter.position) >  Vector3.Distance(tempCollider1.transform.position, _DATA_CENTER.geometryCenter.position))
            //     {
            //         return true;
            //     }
            // }
            // else
            // {
            //     if (tempCollider1 != null)
            //     {
            //         return true;
            //     }
            // }
            return tempCollider1 != null;
        }

        public bool EnemyClose()
        {
            var colliders = Sensor.GetTargetRangeEnemyCollider(0, 5);
            return colliders.Count > 0;
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
            switch (TriggerAttackHeight)
            {
                case -1:// 只适合砸地
                    return HasLowCollider(targetRangeEnemyColliders);
                case 0:// 只适合中段
                    return HasMidCollider(targetRangeEnemyColliders);
                case 1:// 只适合对空和打脑袋
                    return HasHighCollider(targetRangeEnemyColliders);
                //case 2:// 全高度适合
                default:
                    return targetRangeEnemyColliders.Count > 0;
            }
        }
        
        bool HasLowCollider(List<Collider> inColliders)
        {
            var finds = inColliders.FindAll(x=> x.transform.position.y < 0.5f);
            return finds.Count > 0;
        }
        
        bool HasMidCollider(List<Collider> inColliders)
        {
            var finds = inColliders.FindAll(x=> x.transform.position.y >= 0.8f);
            return finds.Count > 0;
        }
        
        bool HasHighCollider(List<Collider> inColliders)
        {
            var finds = inColliders.FindAll(x=> x.transform.position.y >= 1f);
            return finds.Count > 0;
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
            
            switch (TriggerAttackHeight)
            {
                case -1:// 只适合砸地
                    return HasLowCollider(targetRangeEnemyColliders);
                case 0:// 只适合中段
                    return HasMidCollider(targetRangeEnemyColliders);
                case 1:// 只适合对空和打脑袋
                    return HasHighCollider(targetRangeEnemyColliders);
                //case 2:// 全高度适合
                default:
                    return targetRangeEnemyColliders.Count > 0;
            }
        }

        public bool TimeToRespond()
        {
            Collider threat = Sensor.GetSuddenThreatInRange(0, 5);
            return threat == null;
        }
        
        public bool TimeToStopRunning() //没有意义的条件。 
        {
            Collider nearestEnemyMeat = Sensor.GetClosestEnemyColliderInSensorRange();
            return (nearestEnemyMeat != null && Vector3.Distance(nearestEnemyMeat.transform.position, this._DATA_CENTER.WholeT.position) < 5f) || Sensor.GetSuddenThreatInRange(0,8) != null;
        }
        
        public bool CheckTriggerCondition(string conditionFunctionName)
        {
            var T = typeof(Behavior);
            var theMethod = T.GetMethod(conditionFunctionName); //激活同名函数
            return theMethod != null && (bool)theMethod.Invoke(this, null);
        }

        public bool CheckExitCondition(string stateKey)
        {
            _AIStateRunner.BehaviourAndStrategicExitCondition.TryGetValue(stateKey, out string exitCondition);
            switch (exitCondition)
            {
                case "TimeToRespond":
                    return TimeToRespond();
                case "TimeToStopRunning":
                    return TimeToStopRunning();
                default:
                    return true;
            }
        }
    }
}