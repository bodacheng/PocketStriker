using UnityEngine;
using HittingDetection;

namespace Soul
{
    public partial class Hurt_State : Behavior
    {
        bool dropped;
        Vector3 _xz;
        
        void HighDamgeStart(V_Damage newValue)
        {
            dropped = false;
            _Animator.SetFloat("speed", 0f);
            _Rigidbody.velocity = Vector3.zero;
            _usedDizzyTime = FightGlobalSetting.HighHitLastingTime;
            _xz = newValue.attacker.Center.WholeT.forward;
            FightParamsRef.GetKnockOffCount().PlusTimeCounter(0.2f);
            AnimationManger.AnimationTrigger(AnimationManger.GetRandomKnockOffAnim(), true, 0.1f);
        }

        void HighDamageUpdate()
        {
            if (!dropped)
            {
                if (TimeCounter > 0.1f && _BasicPhysicSupport.hiddenMethods.Grounded)
                {
                    dropped = true;
                    _Rigidbody.velocity = Vector3.zero;
                }
                else
                {
                    gameObject.transform.position +=
                    _xz * (FightGlobalSetting.HDamageZAnimationCurve.Evaluate(TimeCounter + Time.fixedDeltaTime) - FightGlobalSetting.HDamageZAnimationCurve.Evaluate(TimeCounter)) +
                    Vector3.up * (FightGlobalSetting.HDamageYAnimationCurve.Evaluate(TimeCounter + Time.fixedDeltaTime) - FightGlobalSetting.HDamageYAnimationCurve.Evaluate(TimeCounter));
                }
            }
        }
    }
}