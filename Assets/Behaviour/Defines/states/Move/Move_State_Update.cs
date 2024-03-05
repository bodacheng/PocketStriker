using UnityEngine;

namespace Soul
{
    public partial class Move_State : Behavior
    {
        void _f_State_Update_SP()
        {
            _timeCounter += Time.fixedDeltaTime;
            if (_BasicPhysicSupport.AtRing)
            {
                DecideDirection();
            }
            
            if (Finished())
            {
                DecideDirection();
            }
            
            switch (_moveDirection)
            {
                case AIMoveDirection.Stay:
                    _useDirection = Vector3.zero;
                    break;
                case AIMoveDirection.BackTowardsEnemy:
                    if (closestEnemy != null)
                        _useDirection = gameObject.transform.position -  closestEnemy.transform.position;
                    else
                        _useDirection = Vector3.zero;
                    break;
                case AIMoveDirection.TowardsEnemy:
                    if (closestEnemy != null)
                        _useDirection = closestEnemy.transform.position - gameObject.transform.position;
                    else
                        _useDirection = Vector3.zero;
                    // 其实use_direction的计算非常恶心，因为实时算朝向特定敌人的话会产生个抖动问题，上面的结果效果差强人意，但比底下这些强。
                    // 底下这些是一些失败的例子
                    //use_direction = Quaternion.Euler(0, angle * Time.fixedDeltaTime / (Time.fixedDeltaTime + 1f), 0) * use_direction;
                    //use_direction = Vector3.Lerp(use_direction, newDir, (angle / 45) * Time.deltaTime / (Time.deltaTime + 1f));
                    //use_direction = (EnemiesByDistance[0].transform.position - gameObject.transform.position).normalized;
                    break;
            }
            
            var enemyAndTeammateBetweenMeAndEnemy = Sensor.EnemyAndTeammateBetweenMeAndEnemy();
            if (enemyAndTeammateBetweenMeAndEnemy != null)
            {
                var temp = (enemyAndTeammateBetweenMeAndEnemy[1].transform.position - this.gameObject.transform.position).normalized +
                           (gameObject.transform.position - enemyAndTeammateBetweenMeAndEnemy[0].transform.position).normalized;
                temp.y = 0;
                _useDirection = Vector3.RotateTowards(_useDirection, temp, 10 * Time.fixedDeltaTime, 0).normalized;//里面的参数都是些很微妙的东西
            }
            
            _useDirection.y = 0;
            _useDirection = _useDirection.normalized;
        }
        
        void _c_State_Update_SP()
        {
            //get movement axis relative to camera
            _screenMovementSpace = Quaternion.Euler(0, _mainCam.eulerAngles.y, 0);
            _screenMovementForward = _screenMovementSpace * Vector3.forward;
            _screenMovementRight = _screenMovementSpace * Vector3.right;
            //get movement input, set direction to move in
                
            var h = Input.GetAxis("Horizontal") + UltimateJoystick.GetHorizontalAxis("joystick");
            var v = Input.GetAxis("Vertical") + UltimateJoystick.GetVerticalAxis("joystick");
            _useDirection = (_screenMovementForward * v) + (_screenMovementRight * h);
            _useDirection.y = 0;
            _useDirection = _useDirection.normalized;
        }
        
        public override void _c_State_FixedUpdate1()
        {
            _c_State_Update_SP();
            if (!_AIStateRunner.BeingControl())
            {
                _useDirection = Vector3.zero;
            }
            if (_useDirection.magnitude > 0f)
            {
                _Animator.SetFloat("speed", 10f);
                Move(_useDirection, speed, true);
                RotateToDirection(_useDirection, 20f, true);
            }
            else
            {
                _Animator.SetFloat("speed", 0f);
                _Rigidbody.velocity = Vector3.zero;
            }
        }
        
        public override void _State_FixedUpdate1()
        {
            _f_State_Update_SP();
            if (_useDirection.magnitude > 0f)
            {
                _Animator.SetFloat("speed", 10f);
                Move(_useDirection, speed, true);
                RotateToDirection(_useDirection, 20f, true);
            }
            else
            {
                _Animator.SetFloat("speed", 0f);
                _Rigidbody.velocity = Vector3.zero;
            }
        }
    }
}