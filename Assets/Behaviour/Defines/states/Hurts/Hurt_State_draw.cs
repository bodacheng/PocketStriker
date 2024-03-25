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
            Vector3 destination()
            {
                var vector3 = newValue.from_weapon_marker.transform.position;
                vector3.y = 0;
                if (vector3.magnitude > BoundaryControlByGod._BattleRingRadius)
                {
                    vector3 = vector3.normalized * BoundaryControlByGod._BattleRingRadius;
                }
                vector3.y = gameObject.transform.position.y;
                return vector3;
            }
            
            _Rigidbody.MovePosition(destination());
        }
    }
}