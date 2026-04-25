using UnityEngine;
using HittingDetection;
using UniRx;
using DG.Tweening;

namespace Soul
{
    public partial class Hurt_State : Behavior
    {
        void PushToMidStart(V_Damage newValue, float dis, bool grounded, bool push = true)
        {
            var attackerCenter = newValue.attacker.Center;
            var direction = attackerCenter.WholeT.transform.forward;
            if (!BasicPhysicSupport.TryGetHorizontalDirection(direction, out direction))
                direction = Vector3.forward;

            var destination = attackerCenter.geometryCenter.transform.position + (push ? 1f : -1f) * direction * dis;
            if (grounded)
            {
                destination.y = 0f;
            }
            else
            {
                AnimationManger.AnimationTrigger(AnimationManger.GetRandomKnockOffAnim(), true, 0.1f);
            }

            destination = _BasicPhysicSupport.ClampPositionToBattleRange(destination);

            void FinishPushToMid()
            {
                if (_BasicPhysicSupport.hiddenMethods.Grounded)
                    _Rigidbody.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
                else
                    _Rigidbody.linearVelocity = Vector3.zero;

                _positionTween = null;
                _physicMissionDisposable?.Dispose();
            }

            _positionTween?.Kill();
            _positionTween = _DATA_CENTER.WholeT.DOMove(destination, 0.3f).OnComplete(FinishPushToMid);

            _physicMissionDisposable = new SingleAssignmentDisposable();
            _physicMissionDisposable.Disposable = Observable.EveryUpdate().Subscribe(_ =>
                {
                    if (gameObject == null)
                    {
                        _positionTween?.Kill(false);
                        _positionTween = null;
                        _physicMissionDisposable.Dispose();
                        return;
                    }

                    if (Vector3.Distance(destination, gameObject.transform.position) < 0.3f || _BasicPhysicSupport.AtRing)
                    {
                        _positionTween?.Kill(false);
                        FinishPushToMid();
                    }
                }
            ).AddTo(gameObject);
        }
    }
}
