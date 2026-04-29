using UnityEngine;
using HittingDetection;
using DG.Tweening;

namespace Soul
{
    public partial class Hurt_State : Behavior
    {
        void PushToMidStart(V_Damage newValue, float dis)
        {
            _BasicPhysicSupport.OpenEnemyTouchingDrag(1);
            Vector3 midDistanceFromMe = newValue.attacker.Center.geometryCenter.transform.position +
                                        newValue.attacker.Center.WholeT.transform.forward * dis;
            float originY = _DATA_CENTER.WholeT.position.y;
            Vector3 temp = midDistanceFromMe;
            temp.y = 0;
            if (temp.magnitude > BoundaryControlByGod._BattleRingRadius)
            {
                midDistanceFromMe = temp.normalized * BoundaryControlByGod._BattleRingRadius;
            }
            if (originY < 0)
            {
                midDistanceFromMe.y = 0;
            }
            else
            {
                midDistanceFromMe.y = originY;
            }

            var targetPos = ClampPositionToBattleRing(midDistanceFromMe);
            mySequence.Append(StartFixedPlanarMoveTween(_DATA_CENTER.WholeT, _Rigidbody, targetPos, 0.3f));
        }
    }
}
