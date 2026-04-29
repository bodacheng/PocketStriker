using UnityEngine;

namespace Soul
{
    public partial class Move_State : Behavior
    {
        void _f_State_Update_SP()
        {
            _timeCounter += Time.fixedDeltaTime;
            if (_BasicPhysicSupport.NearRing)
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

        private float h, v;
        void _c_State_Update_SP()
        {
            //get movement axis relative to camera
            _screenMovementSpace = Quaternion.Euler(0, _mainCam.eulerAngles.y, 0);
            _screenMovementForward = _screenMovementSpace * Vector3.forward;
            _screenMovementRight = _screenMovementSpace * Vector3.right;
            //get movement input, set direction to move in

            if (!CommonSetting.PcMode)
            {
                h = (Input.GetKey(AppSetting.Value.LeftKeyCode) ? -1f : 0f) +
                        (Input.GetKey(AppSetting.Value.RightKeyCode) ? 1f : 0f) +
                        UltimateJoystick.GetHorizontalAxis("joystick");
                v = (Input.GetKey(AppSetting.Value.UpKeyCode) ? 1f : 0f) +
                        (Input.GetKey(AppSetting.Value.DownKeyCode) ? -1f : 0f) +
                        UltimateJoystick.GetVerticalAxis("joystick");
            }
            else
            {
                h = (Input.GetKey(AppSetting.Value.LeftKeyCode) ? -1f : 0f) +
                    (Input.GetKey(AppSetting.Value.RightKeyCode) ? 1f : 0f);
                v = (Input.GetKey(AppSetting.Value.UpKeyCode) ? 1f : 0f) +
                    (Input.GetKey(AppSetting.Value.DownKeyCode) ? -1f : 0f);
            }

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
            ApplyMovementIntent(_useDirection, speed, 20f);

            PreventUnitOverlap();
        }

        public override void _State_FixedUpdate1()
        {
            _f_State_Update_SP();
            ApplyMovementIntent(_useDirection, speed, 20f);
        }
    }
}
