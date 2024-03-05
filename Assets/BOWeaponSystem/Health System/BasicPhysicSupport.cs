using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class BasicPhysicSupport : MonoBehaviour
{
    public Data_Center _DATA_CENTER;
    public Animator animator;
    public Rigidbody Rigidbody;
    public HiddenMethods hiddenMethods;
    
    public bool AtRing
    {
        get;
        set;
    }

    public class HiddenMethods
    {
        readonly BasicPhysicSupport _BasicPhysicSupport;
        public bool EnemyTouchingDrag;
        
        public HiddenMethods(BasicPhysicSupport _BasicPhysicSupport)
        {
            this._BasicPhysicSupport = _BasicPhysicSupport;
        }
        public bool IfStepOnEnemy(Collider box)
        {
            if (box == null)
                return false;
            if (box.isTrigger)
                return false;
            if (_BasicPhysicSupport._DATA_CENTER == null)
                return false;
            if (_BasicPhysicSupport._DATA_CENTER._TeamConfig == null)
                return false;
                
            return _BasicPhysicSupport._DATA_CENTER._TeamConfig.enemyLayerMask == (_BasicPhysicSupport._DATA_CENTER._TeamConfig.enemyLayerMask | (1 << box.gameObject.layer))
                ||
                (_BasicPhysicSupport._DATA_CENTER._TeamConfig.enemyShieldLayerMask & (1 << box.gameObject.layer)) != 0;
        }
        
        public bool IfStepOnFriendCharacter(Collider box)
        {
            return _BasicPhysicSupport._DATA_CENTER == null || _BasicPhysicSupport._DATA_CENTER._TeamConfig != null
                && (_BasicPhysicSupport._DATA_CENTER._TeamConfig.mylayer == box.gameObject.layer) || _BasicPhysicSupport._DATA_CENTER._TeamConfig.myShieldLayer == box.gameObject.layer;
        }
        
        // 与敌人的接触摩操功能
        private readonly List<Collider> _touchingEnemyCs = new List<Collider>();
        
        public bool TouchingEnemy()
        {
            return _touchingEnemyCs.Count > 0;
        }
        
        //弃用
        private Vector3 keptEnemyPoint;
        private Vector3 keptMePoint;
        public Vector3 ClampPosBetweenMeAndE(Vector3 pos)
        {
            pos.y = 0;
            float temp = Vector3.Dot(pos - keptMePoint, keptEnemyPoint - keptMePoint);
            temp = Mathf.Clamp( temp, temp,0 );
            pos = keptMePoint + ( keptEnemyPoint- keptMePoint).normalized * temp;
            return pos;
        }
        
        public void AddTouchedEnemyBody(Collider c)
        {
            if (!_touchingEnemyCs.Contains(c))
                _touchingEnemyCs.Add(c);
            if (_touchingEnemyCs.Count > 0)
            {
                _BasicPhysicSupport.Rigidbody.drag = overrideOnEnemyDrag >= 0 ?
                    overrideOnEnemyDrag : FightGlobalSetting.OnTouchEnemyBodyRigidDrag;
            }
        }
        public void RemoveTouchedEnemyBody(Collider c)
        {
            if (_touchingEnemyCs.Contains(c))
                _touchingEnemyCs.Remove(c);
            if (_touchingEnemyCs.Count == 0)
            {
                _BasicPhysicSupport.Rigidbody.drag = 0f;
            }
        }
        
        public void ClearTouchedEnemyBody()
        {
            _touchingEnemyCs.Clear();
            _BasicPhysicSupport.Rigidbody.drag = 0f;
        }
        
        public int overrideOnEnemyDrag = -1;
        
        public bool Grounded => _BasicPhysicSupport._DATA_CENTER.WholeT.position.y <= floorY;

        readonly float floorY = 0f;
        public void AutoSwitchGravity()
        {
            // foreach (var check in _BasicPhysicSupport.floorCheckers)
            // {
            //     if (floorY >= check.transform.position.y)
            //     {
            //         Grounded = true;
            //         _BasicPhysicSupport.Rigidbody.useGravity = false;
            //         return;
            //     }
            // }

            if (Grounded)
            {
                _BasicPhysicSupport.Rigidbody.useGravity = false;
                return;
            }
            _BasicPhysicSupport.Rigidbody.useGravity = _BasicPhysicSupport.usingGravity;
        }
        
        public void RecoverRootPosChange( )
        {
            if (!TouchingEnemy() && _BasicPhysicSupport.Rigidbody.velocity == Vector3.zero)
                _BasicPhysicSupport._DATA_CENTER.WholeT.transform.position += _BasicPhysicSupport._DATA_CENTER.AnimationManger.AnimatorRef.deltaPosition;
        }
        
        public void LockPos()
        {
            _BasicPhysicSupport.SetUsingGravity(false);
            _BasicPhysicSupport.Rigidbody.constraints = RigidbodyConstraints.FreezeAll;
            _BasicPhysicSupport.Rigidbody.velocity = Vector3.zero;
        }
    }
    
    void Awake()
    {
        hiddenMethods = new HiddenMethods(this);
    }
    
    void Update()
    {
        if (FightGlobalSetting.SceneStep == 1)
        {
            hiddenMethods.AutoSwitchGravity();
            LimitTargetToRange();
        }
    }

    public float ToNearestEnemyXZ()
    {
        var enemies = _DATA_CENTER.Sensor.GetEnemiesByDistance(false);
        if (enemies.Count > 0)
        {
            var nearest = enemies[0];
            var p2 = nearest.transform.position;
            p2.y = 0;
            var p1 = _DATA_CENTER.WholeT.position;
            p1.y = 0;
            return Vector3.Distance(p1, p2);
        }
        else
        {
            return Mathf.Infinity;
        }
    }
    
    private Vector3 pos;
    private float pushIntoRingSpeed = 1;
    void LimitTargetToRange()
    {
        var maxLimbDisFromCenter = _DATA_CENTER.GetFarthestPositionFromZero();
        var originY = _DATA_CENTER.WholeT.position.y;
        pos = _DATA_CENTER.WholeT.position;
        pos.y = 0;
        var disFromCenter = maxLimbDisFromCenter.magnitude;
        if (disFromCenter > BoundaryControlByGod._BattleRingRadius)
        {
            var sa = maxLimbDisFromCenter - maxLimbDisFromCenter.normalized * BoundaryControlByGod._BattleRingRadius;
            pos = pos - sa;
            pos.y = originY;
            _DATA_CENTER.WholeT.position = Vector3.Lerp(_DATA_CENTER.WholeT.position, pos, pushIntoRingSpeed * Time.deltaTime);
            AtRing = true;
        }
        else
        {
            AtRing = false;
        }
        
        if (originY < 0)
        {
            pos.y = 0f;
            _DATA_CENTER.WholeT.position = pos;
        }
    }
    
    private Tweener rotateTween;
    public void RotateToTarget_Tween(Vector3 target, float duration)
    {
        if (rotateTween != null)
            rotateTween.Kill();
        rotateTween = _DATA_CENTER.WholeT.DOLookAt(target, duration, AxisConstraint.Y, Vector3.up);
    }

    private bool usingGravity;
    public void SetUsingGravity(bool _on)
    {
        usingGravity = _on;
    }

    public void OpenEnemyTouchingDrag(int open)
    {
        hiddenMethods.EnemyTouchingDrag = open != 0;
        if (!hiddenMethods.EnemyTouchingDrag)
            hiddenMethods.ClearTouchedEnemyBody();

        if (open == 0)
        {
            hiddenMethods.overrideOnEnemyDrag = -1;
        }
    }
    
    public void SetOverrideOnEnemyDrag(AnimationEvent e)
    {
        hiddenMethods.overrideOnEnemyDrag = e.intParameter;
    }
    
    void OnCollisionEnter(Collision collision)
    {
        if (!hiddenMethods.EnemyTouchingDrag) return;
        if (_DATA_CENTER._MyBehaviorRunner.IfRunning())
        {
            if (hiddenMethods.IfStepOnEnemy(collision.collider))
            {
                hiddenMethods.AddTouchedEnemyBody(collision.collider);
            }
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (!hiddenMethods.EnemyTouchingDrag) return;
        if (_DATA_CENTER._MyBehaviorRunner.IfRunning())
        {
            if (hiddenMethods.IfStepOnEnemy(collision.collider))
            {
                hiddenMethods.RemoveTouchedEnemyBody(collision.collider);
            }
        }
    }
}