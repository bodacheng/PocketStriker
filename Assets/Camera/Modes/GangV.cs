using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DG.Tweening;
public class GangV : CameraMode
{
    readonly Vector3 centerOnGround = Vector3.zero;
    float distance = 25f; // Distance from the target
    float height = 22f; // Height above the target
    readonly float rotationSpeed = 1f; // Speed at which the camera rotates
    private float zoomOutSpeed = 5;
    Vector3 firstPoint;
    Vector3 secondPoint;
    bool isRotating = false;
    bool canTouch;
    CancellationTokenSource cancelSource;
    
    public GangV(float distance, float height)
    {
        this.distance = distance;
        this.height = height;
    }

    async UniTask GetBoundaryInsideView(Camera _camera, CancellationToken token)
    {
        canTouch = false;
        var right = _camera.transform.right;
        right.y = 0;
        
        var point1 = centerOnGround + right * BoundaryControlByGod._BattleRingRadius;
        var point2 = centerOnGround - right * BoundaryControlByGod._BattleRingRadius;
        
        bool OK(Vector2 screenPoint)
        {
            float posX = (float)((decimal)screenPoint.x / Screen.width);
            return (posX <= 1 &&  posX >= 0) ;
        }
        
        var transform = _camera.transform;
        var tempF = transform.forward;
        tempF.y = 0;
        
        await UniTask.WaitUntil(
            () => {
                var startPos = centerOnGround + tempF * distance + Vector3.up * height;
                transform.position = startPos;
                _camera.transform.LookAt(centerOnGround);
                distance += (Time.deltaTime * zoomOutSpeed);
                height += (Time.deltaTime * zoomOutSpeed);
                var screenBoundP1 = _camera.WorldToScreenPoint(point1);
                var screenBoundP2 = _camera.WorldToScreenPoint(point2);
                return OK(screenBoundP1) && OK(screenBoundP2);
            },cancellationToken:token
        );
        canTouch = true;
    }
    
    public override void LocalUpdate(Camera _camera)
    {
        if (!canTouch)
            return;
        
        var RotateCameraH = UltimateJoystick.GetHorizontalAxis("RotateCamera");
        var RotateCameraV = UltimateJoystick.GetHorizontalAxis("RotateCamera");

        bool OnPad()
        {
            return RotateCameraH != 0 || RotateCameraV != 0;
        }
        
        if (Input.touchCount >= 1)
        {
            Touch t1 = Input.GetTouch (0);
            if (t1.phase == TouchPhase.Began) // 触摸开始
            {
                firstPoint = t1.position;
                isRotating = true;
            }
            else if (t1.phase == TouchPhase.Moved && isRotating) // 触摸移动
            {
                if (OnPad())
                    CameraRotate(_camera, firstPoint, t1.position);
                firstPoint = t1.position;
            }
            else if (t1.phase == TouchPhase.Ended) // 触摸结束
            {
                isRotating = false;
            }
        }
        else
        {
            if (Input.GetMouseButtonDown(1))
            {
                firstPoint = Input.mousePosition;
                isRotating = true;
            }
            else if (Input.GetMouseButton(1) && isRotating) // 触摸移动
            {
                if (OnPad())
                    CameraRotate(_camera, firstPoint, Input.mousePosition);
                firstPoint = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(1))
            {
                isRotating = false;
            }
        }

    }
    
    public override void Enter(Camera _camera)
    {
        cancelSource?.Cancel();
        cancelSource = new CancellationTokenSource();
        GetBoundaryInsideView(_camera, cancelSource.Token).Forget();
    }
    
    void CameraRotate(Camera camera, Vector3 _firstPoint, Vector3 _secondPoint)
    {
        float deltaX = _secondPoint.x - _firstPoint.x;
        float rotationAngle = deltaX * rotationSpeed;
        camera.transform.RotateAround(centerOnGround, Vector3.up, rotationAngle);
    }
}
