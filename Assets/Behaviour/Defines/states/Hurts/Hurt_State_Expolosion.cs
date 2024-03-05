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

            _Rigidbody.velocity = CalFixPushVector(newValue.DamageEffectPoint, newValue.attacker.Center.WholeT.position,
                gameObject.transform.position,
                newValue.from_weapon.damage_type, newValue.from_weapon._WeaponMode);
        }
    }
}