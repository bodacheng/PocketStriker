using UnityEngine;
using HittingDetection;
using DG.Tweening;

namespace Soul
{
    public partial class Hurt_State : Behavior
    {
        void NormalStart(V_Damage newValue)
        {
            LockAttacker(newValue);
            Vector3 attackerGPos = newValue.attacker.Center.geometryCenter.position;
            
            // 计算当前连线方向，仅保留水平分量
            Vector3 currentDirection = this._DATA_CENTER.geometryCenter.position - attackerGPos;
            currentDirection.y = 0;
            currentDirection.Normalize();
            
            _BasicPhysicSupport.OpenEnemyTouchingDrag(1);
            if (_BasicPhysicSupport.hiddenMethods.Grounded)
            {
                //_Rigidbody.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
                // 检查当前方向是否与初始方向平行
                if (_nearAttacker != null && IsNearParallel(_mvDirection, currentDirection) && !this._BasicPhysicSupport.AtRing)
                {
                    // 如果不平行，调整B的位置
                    // 计算B应该在的新位置，沿着初始方向从A出发
                    Vector3 newPosition = attackerGPos + _mvDirection * 
                        Vector3.Distance(attackerGPos, this._DATA_CENTER.geometryCenter.position);
                    newPosition.y = this._DATA_CENTER.WholeT.position.y;
                    _tween = this._DATA_CENTER.WholeT.DOMove(newPosition, FightGlobalSetting.HurtAutoFixPosDuration).OnComplete(
                        () =>
                        {
                            if (_BasicPhysicSupport.hiddenMethods.Grounded)
                                _Rigidbody.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
                            else
                            {
                                _Rigidbody.velocity = Vector3.zero;
                            }
                            _tween = null;
                        });
                    newValue.attacker.Center._BasicPhysicSupport.hiddenMethods.AutoFixPosWhenAttackNearEnemy(newPosition, _mvDirection);
                }
                else
                {
                    _Rigidbody.velocity = Vector3.zero;
                }
            }
            else
            {
                _Rigidbody.velocity = Vector3.zero;
            }
        }
        
        private bool IsNearParallel(Vector3 dir1, Vector3 dir2)
        {
            // 检查两个向量的叉积是否为零（零向量）来判断是否平行
            return Vector3.Cross(dir1, dir2).sqrMagnitude < FightGlobalSetting.HurtAutoFixPosCrossLimit;
        }
    }
}