using DG.Tweening;
using UnityEngine;
using HittingDetection;
using UniRx;

namespace Soul
{
    public partial class Hurt_State : Behavior
    {
        void HeavyStart(V_Damage newValue)
        {
            var startPos = gameObject.transform.position;
            var pushDir = CalFixPushVector(newValue, startPos);
            var targetPos = CalcFixedPlanarMoveTarget(startPos, pushDir, FightGlobalSetting.NormalAttackPosFixingTime);
            _fixedDisplacementTweener?.Kill();
            _fixedDisplacementTweener = StartFixedPlanarMoveTween(_DATA_CENTER.WholeT, _Rigidbody, targetPos,
                FightGlobalSetting.NormalAttackPosFixingTime);
            
            _physicMissionDisposable = new SingleAssignmentDisposable();
            _physicMissionDisposable.Disposable = Observable.EveryUpdate().Subscribe(_ =>
                {
                    if (TimeCounter > FightGlobalSetting.NormalAttackPosFixingTime)
                    {
                        if (_BasicPhysicSupport.hiddenMethods.Grounded)
                            _Rigidbody.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
                        else
                        {
                            _Rigidbody.linearVelocity = Vector3.zero;
                        }
                        _physicMissionDisposable.Dispose();
                    }
                }
            ).AddTo(gameObject);
        }
    }
}
