using DG.Tweening;
using UnityEngine;
using HittingDetection;
using UniRx;

namespace Soul
{
    public partial class Hurt_State : Behavior
    {
        void ExplosionDamageStart(V_Damage newValue)
        {
            _usedDizzyTime = FightGlobalSetting.HeavyHitLastingTime;
            _physicMissionDisposable = new SingleAssignmentDisposable();
            _physicMissionDisposable.Disposable = Observable.EveryUpdate().Subscribe(_ =>
                {
                    if (TimeCounter > FightGlobalSetting.NormalAttackPosFixingTime)
                    {
                        _Rigidbody.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
                        _physicMissionDisposable.Dispose();
                    }
                }
            ).AddTo(gameObject);

            var startPos = gameObject.transform.position;
            var pushDir = CalFixPushVector(newValue, startPos);
            var targetPos = CalcFixedPlanarMoveTarget(startPos, pushDir, FightGlobalSetting.NormalAttackPosFixingTime);
            _fixedDisplacementTweener?.Kill();
            _fixedDisplacementTweener = StartFixedPlanarMoveTween(_DATA_CENTER.WholeT, _Rigidbody, targetPos,
                FightGlobalSetting.NormalAttackPosFixingTime);
        }
    }
}
