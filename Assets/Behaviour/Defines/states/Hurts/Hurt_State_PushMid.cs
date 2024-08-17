using UnityEngine;
using HittingDetection;
using UniRx;
using DG.Tweening;

namespace Soul
{
    public partial class Hurt_State : Behavior
    {
        void PushToMidStart(V_Damage newValue, float dis)
        {
            Vector3 midDistanceFromMe = newValue.attacker.Center.geometryCenter.transform.position + 
                                        newValue.attacker.Center.WholeT.transform.forward * dis;
            float originY = _DATA_CENTER.WholeT.position.y;
            Vector3 temp = midDistanceFromMe;
            temp.y = 0;
            if (temp.magnitude > BoundaryControlByGod._BattleRingRadius)
            {
                midDistanceFromMe = temp.normalized * BoundaryControlByGod._BattleRingRadius;
                midDistanceFromMe.y = originY;
            }
            if (originY < 0)
            {
                midDistanceFromMe.y = 0;
            }
            
            mySequence.Append(_DATA_CENTER.WholeT.DOMove(midDistanceFromMe, 0.3f).OnUpdate(
                () =>
                {
                    if (gameObject == null)
                    {
                        mySequence.Kill();
                        return;
                    }
                    if (_BasicPhysicSupport.AtRing)
                    {
                        mySequence.Kill();
                        _Rigidbody.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
                    }
                }) 
            );
        }
    }
}