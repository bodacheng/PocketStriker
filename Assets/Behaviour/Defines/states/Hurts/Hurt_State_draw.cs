using UnityEngine;
using HittingDetection;

namespace Soul
{
    public partial class Hurt_State : Behavior
    {
        void DrawDamageStart(V_Damage newValue)
        {
            _Rigidbody.mass = FightGlobalSetting.FighterRigidMass / 2;
            _usedDizzyTime = FightGlobalSetting.HeavyHitLastingTime;
        }

        void DrawDamageUpdate(V_Damage newValue)
        {
            if (newValue.from_weapon.weaponHP > 0 && newValue.from_weapon.CurrentHP <= 0)
                return;

            Vector3 Destination()
            {
                var vector3 = newValue.from_weapon_marker.transform.position;
                vector3.y = gameObject.transform.position.y;
                return vector3;
            }
            _Rigidbody.MovePosition(Destination());
        }
    }
}