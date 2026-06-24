using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using MCombat.Shared.Behaviour;

public enum Weight
{
    normal,
    heavy
}

public class BasicPhysicSupport : MonoBehaviour
{
    public Data_Center _DATA_CENTER;
    public Animator animator;
    public Rigidbody Rigidbody;

    [SerializeField] private Weight weight = Weight.normal;
    public Weight Weight => weight;

    public HiddenMethods hiddenMethods;

    private Vector3 pos;
    private float pushIntoRingSpeed = 1;
    public bool AtRing;
    public bool NearRing;

    [Header("Contact Stabilizer")]
    [SerializeField] private float contactStabilizeSmoothTime = 0.08f;
    [SerializeField] private float contactJitterThreshold = 0.15f;
    [SerializeField] private float contactStabilizeMaxSpeed = 10f;
    [SerializeField] private float contactVelocityThreshold = 2f;

    [Header("Skill Motion Coordination")]
    [SerializeField] private float skillMotionContactBlockSeconds = 0.08f;
    [SerializeField] private float drawFollowMaxSpeed = 30f;
    [SerializeField] private float rootMotionRecoverMaxDelta = 0.75f;

    private Vector3 contactStabilizedXZ;
    private Vector3 contactStabilizeVelocity;
    private bool contactStabilizerInitialized;
    private int coordinatedDisplacementCount;
    private float contactCorrectionBlockedUntil;

    bool atRing
    {
        get
        {
            var maxLimbDisFromCenter = _DATA_CENTER.GetFarthestPositionFromZero();
            var originY = _DATA_CENTER.WholeT.position.y;
            pos = _DATA_CENTER.WholeT.position;
            pos.y = 0;

            var atRing = maxLimbDisFromCenter.magnitude > BoundaryControlByGod._BattleRingRadius;
            NearRing = maxLimbDisFromCenter.magnitude + 2 > BoundaryControlByGod._BattleRingRadius;
            if (atRing)
            {
                var sa = maxLimbDisFromCenter - maxLimbDisFromCenter.normalized * BoundaryControlByGod._BattleRingRadius;
                pos = pos - sa;
                pos.y = originY;
                SetRootAndRigidbodyPosition(
                    Vector3.Lerp(_DATA_CENTER.WholeT.position, pos, pushIntoRingSpeed * Time.deltaTime),
                    false);
            }

            if (originY < 0)
            {
                pos.y = 0f;
                SetRootAndRigidbodyPosition(pos, true);
            }
            return atRing;
        }
    }

    public static bool TryGetHorizontalDirection(Vector3 vector, out Vector3 direction)
    {
        vector.y = 0f;
        if (vector.sqrMagnitude <= Mathf.Epsilon)
        {
            direction = Vector3.zero;
            return false;
        }

        direction = vector.normalized;
        return true;
    }

    public static bool IsNearParallelOnXZ(Vector3 dir1, Vector3 dir2)
    {
        if (!TryGetHorizontalDirection(dir1, out var n1) || !TryGetHorizontalDirection(dir2, out var n2))
            return false;

        return Vector3.Cross(n1, n2).sqrMagnitude < FightGlobalSetting.HurtAutoFixPosCrossLimit;
    }

    public static Vector3 FindClosestPointOnLine(Vector3 linePoint, Vector3 point, Vector3 lineDirection)
    {
        if (!TryGetHorizontalDirection(lineDirection, out var normalizedDirection))
            return point;

        var toPoint = point - linePoint;
        var projectionLength = Vector3.Dot(toPoint, normalizedDirection);
        return linePoint + normalizedDirection * projectionLength;
    }

    public Vector3 ClampPositionToBattleRange(Vector3 targetPosition)
    {
        return BehaviorMotionUtility.ClampPositionToBattleRange(
            targetPosition,
            FightGlobalSetting.SceneStep == 1,
            BoundaryControlByGod._BattleRingRadius);
    }

    public void SetPositionBySkill(Vector3 targetPosition)
    {
        ApplySkillPosition(targetPosition, true);
    }

    public void ApplySkillPosition(Vector3 targetPosition, bool resetVelocity)
    {
        BlockContactCorrection(skillMotionContactBlockSeconds);
        SetRootAndRigidbodyPosition(targetPosition, resetVelocity);
    }

    public void AddPositionBySkill(Vector3 delta, bool resetVelocity)
    {
        if (delta.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        ApplySkillPosition(CurrentRootPosition() + delta, resetVelocity);
    }

    public void FollowSkillPosition(Vector3 targetPosition)
    {
        targetPosition = ClampPositionToBattleRange(targetPosition);
        var nextPosition = BehaviorMotionUtility.MoveTowardsWithMaxSpeed(
            CurrentRootPosition(),
            targetPosition,
            drawFollowMaxSpeed,
            Time.fixedDeltaTime);
        ApplySkillPosition(nextPosition, true);
    }

    public Tweener MoveRootToPositionBySkill(Vector3 targetPosition, float duration, Ease ease = Ease.Linear, TweenCallback onFinished = null)
    {
        var root = RootTransform();
        if (root == null)
        {
            return null;
        }

        targetPosition = ClampPositionToBattleRange(targetPosition);
        if (duration <= 0f)
        {
            ApplySkillPosition(targetPosition, true);
            onFinished?.Invoke();
            return null;
        }

        BeginCoordinatedDisplacement(duration);
        SetRigidbodyVelocity(Vector3.zero, Vector3.zero);

        var ended = false;
        void EndOnce()
        {
            if (ended)
            {
                return;
            }

            ended = true;
            EndCoordinatedDisplacement();
            onFinished?.Invoke();
        }

        return DOTween.To(CurrentRootPosition, value => SetRootAndRigidbodyPosition(value, true, false), targetPosition, duration)
            .SetEase(ease)
            .SetTarget(root)
            .SetLink(root.gameObject)
            .OnComplete(EndOnce)
            .OnKill(EndOnce);
    }

    public void RecoverRootMotionDelta(Vector3 delta)
    {
        var hasRigidbody = Rigidbody != null;
        var velocity = hasRigidbody ? Rigidbody.linearVelocity : Vector3.zero;
        if (!BehaviorMotionUtility.ShouldApplyRootMotionRecovery(
                HasActiveCoordinatedDisplacement(),
                hiddenMethods.TouchingEnemy(),
                hasRigidbody,
                velocity,
                delta))
        {
            return;
        }

        AddPositionBySkill(BehaviorMotionUtility.ClampDeltaMagnitude(delta, rootMotionRecoverMaxDelta), false);
    }

    void BeginCoordinatedDisplacement(float expectedDuration)
    {
        coordinatedDisplacementCount++;
        BlockContactCorrection(expectedDuration + skillMotionContactBlockSeconds);
        ResetContactStabilizer();
    }

    void EndCoordinatedDisplacement()
    {
        coordinatedDisplacementCount = Mathf.Max(0, coordinatedDisplacementCount - 1);
        BlockContactCorrection(skillMotionContactBlockSeconds);
        ResetContactStabilizer();
    }

    bool HasActiveCoordinatedDisplacement()
    {
        return coordinatedDisplacementCount > 0;
    }

    bool IsContactCorrectionBlocked()
    {
        return coordinatedDisplacementCount > 0 || Time.time < contactCorrectionBlockedUntil;
    }

    void BlockContactCorrection(float seconds)
    {
        if (seconds <= 0f)
        {
            return;
        }

        contactCorrectionBlockedUntil = Mathf.Max(contactCorrectionBlockedUntil, Time.time + seconds);
        ResetContactStabilizer();
    }

    Transform RootTransform()
    {
        return _DATA_CENTER != null && _DATA_CENTER.WholeT != null ? _DATA_CENTER.WholeT : transform;
    }

    Vector3 CurrentRootPosition()
    {
        var root = RootTransform();
        return root != null ? root.position : transform.position;
    }

    void SetRootAndRigidbodyPosition(Vector3 targetPosition, bool resetVelocity, bool resetContactStabilizerState = true)
    {
        targetPosition = ClampPositionToBattleRange(targetPosition);

        if (Rigidbody != null)
        {
            if (resetVelocity)
            {
                SetRigidbodyVelocity(Vector3.zero, Vector3.zero);
            }

            Rigidbody.position = targetPosition;
        }

        var root = RootTransform();
        if (root != null)
        {
            root.position = targetPosition;
        }
        else
        {
            transform.position = targetPosition;
        }

        if (resetContactStabilizerState)
        {
            ResetContactStabilizer();
        }
    }

    void SetRigidbodyVelocity(Vector3 linearVelocity, Vector3 angularVelocity)
    {
        if (Rigidbody == null)
        {
            return;
        }

        Rigidbody.linearVelocity = linearVelocity;
        Rigidbody.angularVelocity = angularVelocity;
    }

    public class HiddenMethods
    {
        readonly BasicPhysicSupport _BasicPhysicSupport;
        public bool EnemyTouchingDrag;
        Tween _attackPosFixTween;

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

        public Vector3 GetCenterOfTouchingEnemies()
        {
            if (_touchingEnemyCs.Count == 0)
                return Vector3.zero;

            Vector3 sum = Vector3.zero;
            foreach (var col in _touchingEnemyCs)
            {
                sum += col.bounds.center;
            }

            sum.y = 0;
            return sum / _touchingEnemyCs.Count;
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
                _BasicPhysicSupport.Rigidbody.linearDamping = OverrideOnEnemyDrag >= 0 ?
                    OverrideOnEnemyDrag : FightGlobalSetting.OnTouchEnemyBodyRigidDrag;
            }
        }
        public void RemoveTouchedEnemyBody(Collider c)
        {
            if (_touchingEnemyCs.Contains(c))
                _touchingEnemyCs.Remove(c);
            if (_touchingEnemyCs.Count == 0)
            {
                _BasicPhysicSupport.Rigidbody.linearDamping = 0f;
            }
        }

        public void ClearTouchedEnemyBody()
        {
            _touchingEnemyCs.Clear();
            _BasicPhysicSupport.Rigidbody.linearDamping = 0f;
        }

        public int OverrideOnEnemyDrag = -1;

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
            var dataCenter = _BasicPhysicSupport._DATA_CENTER;
            if (dataCenter == null || dataCenter.AnimationManger == null || dataCenter.AnimationManger.AnimatorRef == null)
            {
                return;
            }

            _BasicPhysicSupport.RecoverRootMotionDelta(dataCenter.AnimationManger.AnimatorRef.deltaPosition);
        }

        public void LockPos()
        {
            _BasicPhysicSupport.SetUsingGravity(false);
            _BasicPhysicSupport.Rigidbody.constraints = RigidbodyConstraints.FreezeAll;
            _BasicPhysicSupport.Rigidbody.linearVelocity = Vector3.zero;
        }

        public void AutoFixPosWhenAttackNearEnemy(Vector3 enemyPos, Vector3 mvDirection)
        {
            var dataCenter = _BasicPhysicSupport._DATA_CENTER;
            if (dataCenter == null || dataCenter.geometryCenter == null || dataCenter.WholeT == null)
                return;

            var mePos = dataCenter.geometryCenter.position;
            mePos.y = 0f;
            enemyPos.y = 0f;

            if (!BasicPhysicSupport.IsNearParallelOnXZ(mvDirection, enemyPos - mePos))
                return;

            var targetPos = BasicPhysicSupport.FindClosestPointOnLine(enemyPos, mePos, mvDirection);
            targetPos.y = dataCenter.WholeT.position.y;
            targetPos = _BasicPhysicSupport.ClampPositionToBattleRange(targetPos);

            _attackPosFixTween?.Kill();
            _attackPosFixTween = _BasicPhysicSupport.MoveRootToPositionBySkill(
                targetPos,
                FightGlobalSetting.HurtAutoFixPosDuration,
                Ease.Linear,
                () =>
                {
                    _attackPosFixTween = null;
                });
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
            AtRing = atRing;

            float fps = 1.0f / Time.deltaTime;
            if (fps < 45f && Rigidbody.collisionDetectionMode != CollisionDetectionMode.Discrete)
            {
                Rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }
            else if (fps > 55f && Rigidbody.collisionDetectionMode != CollisionDetectionMode.Continuous)
            {
                Rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
        }
    }

    void LateUpdate()
    {
        if (FightGlobalSetting.SceneStep != 1)
        {
            ResetContactStabilizer();
            return;
        }
        ApplyContactStabilizer();
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

    private Tweener rotateTween;
    public Tweener RotateToTarget_Tween(Vector3 target, float duration)
    {
        rotateTween?.Kill();
        rotateTween = _DATA_CENTER.WholeT.DOLookAt(target, duration, AxisConstraint.Y, Vector3.up);
        return rotateTween;
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
            hiddenMethods.OverrideOnEnemyDrag = -1;
        }
    }

    public void SetOverrideOnEnemyDrag(AnimationEvent e)
    {
        hiddenMethods.OverrideOnEnemyDrag = e.intParameter;
    }

    bool ShouldSkipEnemyContactCorrection()
    {
        return BehaviorMotionUtility.ShouldSkipContactCorrection(
            IsContactCorrectionBlocked(),
            _DATA_CENTER != null
            && _DATA_CENTER.FightDataRef != null
            && _DATA_CENTER.FightDataRef.GettingDamage);
    }

    // Smooth out small root jitter when rubbing against other fighters.
    void ApplyContactStabilizer()
    {
        if (ShouldSkipEnemyContactCorrection())
        {
            ResetContactStabilizer();
            return;
        }

        if (!hiddenMethods.TouchingEnemy())
        {
            ResetContactStabilizer();
            return;
        }

        if (Rigidbody.linearVelocity.sqrMagnitude > contactVelocityThreshold * contactVelocityThreshold)
        {
            ResetContactStabilizer();
            return;
        }

        var currentPos = _DATA_CENTER.WholeT.position;
        var currentXZ = new Vector3(currentPos.x, 0f, currentPos.z);

        if (!contactStabilizerInitialized)
        {
            contactStabilizerInitialized = true;
            contactStabilizedXZ = currentXZ;
            contactStabilizeVelocity = Vector3.zero;
        }

        float displacement = Vector3.Distance(currentXZ, contactStabilizedXZ);
        if (displacement > contactJitterThreshold)
        {
            contactStabilizedXZ = currentXZ;
            contactStabilizeVelocity = Vector3.zero;
            return;
        }

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        contactStabilizedXZ = Vector3.SmoothDamp(
            contactStabilizedXZ,
            currentXZ,
            ref contactStabilizeVelocity,
            contactStabilizeSmoothTime,
            contactStabilizeMaxSpeed,
            deltaTime
        );

        var stabilisedPos = currentPos;
        stabilisedPos.x = contactStabilizedXZ.x;
        stabilisedPos.z = contactStabilizedXZ.z;
        SetRootAndRigidbodyPosition(stabilisedPos, false, false);
    }

    void ResetContactStabilizer()
    {
        contactStabilizerInitialized = false;
        contactStabilizeVelocity = Vector3.zero;
    }

    void ResolveContactPenetration(Collision collision)
    {
        if (_DATA_CENTER == null || _DATA_CENTER.WholeT == null)
            return;
        if (ShouldSkipEnemyContactCorrection())
            return;
        if (!hiddenMethods.IfStepOnEnemy(collision.collider))
            return;
        if (Rigidbody.linearVelocity.sqrMagnitude > contactVelocityThreshold * contactVelocityThreshold)
            return;

        var correction = Vector3.zero;
        var contacts = collision.contacts;
        for (int i = 0; i < contacts.Length; i++)
        {
            var contact = contacts[i];
            float penetration = Mathf.Max(0f, -contact.separation);
            if (penetration <= Mathf.Epsilon)
                continue;

            var normal = contact.normal;
            normal.y = 0f;
            if (normal.sqrMagnitude < 1e-6f)
                continue;

            correction += normal.normalized * penetration;
        }

        if (correction == Vector3.zero)
            return;

        correction = Vector3.ClampMagnitude(correction, contactJitterThreshold);

        var pos = _DATA_CENTER.WholeT.position;
        pos += correction;
        SetRootAndRigidbodyPosition(pos, false, false);
        contactStabilizedXZ = new Vector3(pos.x, 0f, pos.z);
        contactStabilizerInitialized = true;
        contactStabilizeVelocity = Vector3.zero;
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

    void OnCollisionStay(Collision collision)
    {
        if (_DATA_CENTER != null && _DATA_CENTER._MyBehaviorRunner.IfRunning())
        {
            if (ShouldSkipEnemyContactCorrection())
                return;
            ResolveContactPenetration(collision);
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
