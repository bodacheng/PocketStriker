using UnityEngine;
using HittingDetection;
using UniRx;
using DG.Tweening;

namespace Soul
{
    public partial class Hurt_State : Behavior
    {
        void PushToMidStart(V_Damage newValue, float dis, bool Grounded, bool push = true)
        {
            _usedDizzyTime = FightGlobalSetting.HeavyHitLastingTime;
            Vector3 midDistanceFromMe = newValue.attacker.Center.geometryCenter.transform.position + 
                                        (push ? 1f : -0.5f) * newValue.attacker.Center.WholeT.transform.forward * dis;
            if (Grounded)
            {
                PlayHurtAnim(newValue);
                midDistanceFromMe.y = 0;
            }
            else
            {
                AnimationManger.AnimationTrigger(AnimationManger.GetRandomKnockOffAnim(), true, 0.1f);
            }
            _tween = gameObject.transform.DOMove(midDistanceFromMe, 0.3f);
            _physicMissionDisposable = new SingleAssignmentDisposable();
            _physicMissionDisposable.Disposable = Observable.EveryUpdate().Subscribe(_ =>
                {
                    if (gameObject == null)
                    {
                        _physicMissionDisposable.Dispose();
                        return;
                    }
                    
                    if (Vector3.Distance(midDistanceFromMe, gameObject.transform.position) < 0.3f || _BasicPhysicSupport.AtRing)
                    {
                        _tween.Kill(false);
                        _Rigidbody.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
                        _physicMissionDisposable.Dispose();
                    }
                }
            ).AddTo(gameObject);
        }
    }
}