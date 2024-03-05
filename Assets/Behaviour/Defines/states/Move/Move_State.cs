using System.Collections.Generic;
using NoSuchStudio.Common;
using UnityEngine;

namespace Soul
{
    public partial class Move_State : Behavior
    {
        readonly float speed;
        readonly float timeLimit;
        public AIMoveMode AIMoveStyle;
        float _timeCounter;
        Vector3 _useDirection;
        AIMoveDirection _moveDirection;
        Transform _mainCam;
        Quaternion _screenMovementSpace;
        Vector3 _screenMovementForward, _screenMovementRight;
        List<GameObject> _enemiesByDistance = new List<GameObject>();
        
        public enum AIMoveMode
        {
            Test = 0,
            Normal = 1
        }
        enum AIMoveDirection
        {
            Stay,
            TowardsEnemy,
            BackTowardsEnemy,
            TowardsEnemyRight,
            TowardsEnemyLeft,
            RunToBattleGroundCenter
        }
        
        public Move_State(AIMoveMode aiMoveStyle, float speed, float timeLimit)
        {
            AIMoveStyle = aiMoveStyle;
            this.speed = speed;
            this.timeLimit = timeLimit;
        }

        public override bool Capacity_enter_condition()
        {
            return true;
        }

        void CommonEnter()
        {
            _timeCounter = 0f;
            _Animator.applyRootMotion = false;
            _Weapon_Animation_Events.ClearMarkerManagers();
            AnimationManger.PlayLayerAnim(null, true, 0.05f);
            pEvents.CloseAllPersonalityEffects();
            _mainCam = CameraManager._camera.transform;
            _Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            _BasicPhysicSupport.Rigidbody.interpolation = RigidbodyInterpolation.None;
            Sensor.DetectionStart(1, true);
            AnimationManger.CasualFace();
        }

        public override void C_State_enter()
        {
            CommonEnter();
            Sensor.DetectionStart(1, true);
        }

        // 整个enter阶段与状态运行中有关的就是决定use_direction和moveDirection。前者状态运行中会调整。
        public override void AI_State_enter()
        {
            CommonEnter();
            DecideDirection();
        }

        // Process when exit the state 
        public override void AI_State_exit()
        {
            base.AI_State_exit();
            _BasicPhysicSupport.Rigidbody.interpolation = RigidbodyInterpolation.Extrapolate;
        }
        
        bool Finished()
        {
            bool returnValue = false;
            switch (_moveDirection)
            {
                case AIMoveDirection.BackTowardsEnemy:
                    if (_timeCounter > timeLimit / 2)
                        returnValue = true;
                    break;
                case AIMoveDirection.Stay:
                    if (_timeCounter > timeLimit / 2)
                        returnValue = true;
                    break;
                case AIMoveDirection.TowardsEnemy:
                    if (_timeCounter > timeLimit)
                        return true;
                    
                    if (closestEnemy != null && Vector3.Distance(gameObject.transform.position, closestEnemy.transform.position) < FightGlobalSetting._closeDis)
                    {
                        returnValue = true;
                    }
                    break;
                case AIMoveDirection.TowardsEnemyLeft:
                    if (_timeCounter > timeLimit / 3)
                        returnValue = true;
                    break;
                case AIMoveDirection.TowardsEnemyRight:
                    if (_timeCounter > timeLimit / 3)
                        returnValue = true;
                    break;
                case AIMoveDirection.RunToBattleGroundCenter:
                    if (_timeCounter > timeLimit / 3)
                        returnValue = true;
                    break;
            }
            if (returnValue)
                _timeCounter = 0f;
            return returnValue;
        }

        private GameObject closestEnemy;
        void DecideDirection()
        {
            if (AIMoveStyle == AIMoveMode.Test)
            {
                _moveDirection = AIMoveDirection.Stay;
                _useDirection = Vector3.zero;
                return;
            }
            
            if (_BasicPhysicSupport.AtRing)
            {
                _moveDirection = AIMoveDirection.RunToBattleGroundCenter;
                _useDirection = Vector3.zero - gameObject.transform.position;
                _useDirection.y = 0;
                return;
            }
            
            _enemiesByDistance = Sensor.GetEnemiesByDistance(true);
            closestEnemy = _enemiesByDistance.Count > 0 ? _enemiesByDistance[0] : null;
            if (closestEnemy != null)
            {
                var meToEnemyVector = (closestEnemy.transform.position - gameObject.transform.position);
                var nextSkillRange = _AIStateRunner.CalAdviceDistanceFromEnemy();
                
                if (meToEnemyVector.magnitude < nextSkillRange.Item1)
                {
                    //Debug.Log("here we :"+ nextSkillRange.Item1 + " to "+ nextSkillRange.Item2);
                    _moveDirection = AIMoveDirection.BackTowardsEnemy;
                }
                else
                {
                    if (meToEnemyVector.magnitude <= FightGlobalSetting._closeDis)
                    {
                        _moveDirection = AIMoveDirection.Stay;
                    }
                    else
                    {
                        _moveDirection = (new List<AIMoveDirection>()
                        {
                            AIMoveDirection.TowardsEnemy, 
                            AIMoveDirection.TowardsEnemyLeft,
                            AIMoveDirection.TowardsEnemyRight
                        }).Random();
                    }
                }
                
                switch (_moveDirection)
                {
                    case AIMoveDirection.TowardsEnemy:
                        break;
                    case AIMoveDirection.BackTowardsEnemy:
                        break;
                    case AIMoveDirection.TowardsEnemyRight:
                        if (closestEnemy != null)
                            _useDirection = GetVerticalDir(meToEnemyVector) + meToEnemyVector.normalized;
                        break;
                    case AIMoveDirection.TowardsEnemyLeft:
                        if (closestEnemy != null)
                            _useDirection = -GetVerticalDir(meToEnemyVector) + meToEnemyVector.normalized;
                        break;
                    default:
                        _useDirection = Vector3.zero;
                        break;
                }
            }
            else
            {
                _moveDirection = AIMoveDirection.Stay;
                _useDirection = Vector3.zero;
            }
        }
    }
}