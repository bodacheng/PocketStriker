using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class GangV : CameraMode
{
    readonly Vector3 _centerOnGround = Vector3.zero;
    readonly float _distanceOrigin = 25f; // Distance from the target
    readonly float _heightOrigin = 22f; // Height above the target
    float _distance = 25f; // Distance from the target
    float _height = 22f; // Height above the target
    private const float RotationSpeed = 1f; // Speed at which the camera rotates
    private const float ZoomOutSpeed = 5;
    Vector3 _firstPoint;
    Vector3 _secondPoint;
    bool _isRotating = false;
    bool _canTouch;
    CancellationTokenSource _cancelSource;
    
    public GangV(float distance, float height)
    {
        this._distanceOrigin = distance;
        this._heightOrigin = height;
    }

    async UniTask GetBoundaryInsideView(Camera _camera, CancellationToken token)
    {
        _canTouch = false;
        
        _distance = _distanceOrigin;
        _height = _heightOrigin;
        
        var right = _camera.transform.right;
        right.y = 0;
        
        var point1 = _centerOnGround + right * BoundaryControlByGod._BattleRingRadius;
        var point2 = _centerOnGround - right * BoundaryControlByGod._BattleRingRadius;
        
        bool OK(Vector2 screenPoint)
        {
            float posX = (float)((decimal)screenPoint.x / Screen.width);
            return posX is <= 1 and >= 0 ;
        }
        
        var cameraTransform = _camera.transform;
        var tempF = cameraTransform.forward;
        tempF.y = 0;
        
        await UniTask.WaitUntil(
            () => {
                var startPos = _centerOnGround + tempF * _distance + Vector3.up * _height;
                cameraTransform.position = startPos;
                cameraTransform.LookAt(_centerOnGround);
                _distance += (Time.deltaTime * ZoomOutSpeed);
                _height += (Time.deltaTime * ZoomOutSpeed);
                var screenBoundP1 = _camera.WorldToScreenPoint(point1);
                var screenBoundP2 = _camera.WorldToScreenPoint(point2);
                return OK(screenBoundP1) && OK(screenBoundP2);
            },cancellationToken:token
        );
        _canTouch = true;
    }
    
    public override void LocalUpdate(Camera _camera)
    {
        if (!_canTouch)
            return;
        
        var rotateCameraH = UltimateJoystick.GetHorizontalAxis("RotateCamera");
        var rotateCameraV = UltimateJoystick.GetHorizontalAxis("RotateCamera");

        if (Input.touchCount >= 1)
        {
            Touch t1 = Input.GetTouch (0);
            if (t1.phase == TouchPhase.Began) // 触摸开始
            {
                _firstPoint = t1.position;
                _isRotating = true;
            }
            else if (t1.phase == TouchPhase.Moved && _isRotating) // 触摸移动
            {
                if (OnPad())
                    CameraRotate(_camera, _firstPoint, t1.position);
                _firstPoint = t1.position;
            }
            else if (t1.phase == TouchPhase.Ended) // 触摸结束
            {
                _isRotating = false;
            }
        }
        else
        {
            if (Input.GetMouseButtonDown(1))
            {
                _firstPoint = Input.mousePosition;
                _isRotating = true;
            }
            else if (Input.GetMouseButton(1) && _isRotating) // 触摸移动
            {
                if (OnPad())
                    CameraRotate(_camera, _firstPoint, Input.mousePosition);
                _firstPoint = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(1))
            {
                _isRotating = false;
            }
        }

        return;

        bool OnPad()
        {
            return rotateCameraH != 0 || rotateCameraV != 0;
        }
    }
    
    public override void Enter(Camera _camera)
    {
        _cancelSource?.Cancel();
        _cancelSource = new CancellationTokenSource();
        GetBoundaryInsideView(_camera, _cancelSource.Token).Forget();
    }
    
    void CameraRotate(Camera camera, Vector3 _firstPoint, Vector3 _secondPoint)
    {
        float deltaX = _secondPoint.x - _firstPoint.x;
        float rotationAngle = deltaX * RotationSpeed;
        camera.transform.RotateAround(_centerOnGround, Vector3.up, rotationAngle);
    }
}
