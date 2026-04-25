using UnityEngine;
using HittingDetection;
using DG.Tweening;

namespace Soul
{
    public partial class Hurt_State : Behavior
    {
        void NormalStart(V_Damage newValue)
        {
            LockAttacker(newValue);
            _BasicPhysicSupport.OpenEnemyTouchingDrag(1);
            _Rigidbody.linearVelocity = Vector3.zero;

            if (_BasicPhysicSupport.hiddenMethods.Grounded)
            {
                var attackerCenter = newValue?.attacker?.Center;
                if (attackerCenter == null || attackerCenter.geometryCenter == null)
                    return;

                var attackerPos = attackerCenter.geometryCenter.position;
                attackerPos.y = 0f;
                var mePos = _DATA_CENTER.geometryCenter.position;
                mePos.y = 0f;
                var currentDirection = mePos - attackerPos;

                if (_nearAttacker != null &&
                    BasicPhysicSupport.IsNearParallelOnXZ(_mvDirection, currentDirection) &&
                    !_BasicPhysicSupport.AtRing)
                {
                    var targetPos = attackerPos + _mvDirection *
                        Vector3.Distance(attackerPos, mePos);
                    targetPos.y = _DATA_CENTER.WholeT.position.y;
                    targetPos = _BasicPhysicSupport.ClampPositionToBattleRange(targetPos);

                    _positionTween?.Kill();
                    _positionTween = _DATA_CENTER.WholeT
                        .DOMove(targetPos, FightGlobalSetting.HurtAutoFixPosDuration)
                        .OnComplete(() =>
                        {
                            if (_BasicPhysicSupport.hiddenMethods.Grounded)
                                _Rigidbody.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
                            else
                                _Rigidbody.linearVelocity = Vector3.zero;

                            _positionTween = null;
                        });

                    attackerCenter._BasicPhysicSupport?.hiddenMethods.AutoFixPosWhenAttackNearEnemy(targetPos, _mvDirection);
                }
            }
            else
            {
                _Rigidbody.linearVelocity = Vector3.zero;
            }
        }
    }
}
