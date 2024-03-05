using UnityEngine;
using DG.Tweening;

//本相机控制模式也是基于圆形战场，并且相机的朝向固定为Vector3.foward，偏朝下俯视。
//这里的相机实际的活动范围是一个半径小于战场的圆内，并且由于相机在观察战场的时候的角度关系，
//这个更小的圆形区域是向屏幕方向正外偏离战场的圆心。
//进一步的改修可以考虑以相机的活动范围为参考制定手指划过屏幕时的相机移动范围

public class TouchTopDownCamera : CameraMode
{
    float startPosSetDuration = 0.3f;
    float height;
    readonly float battlefieldDiameter;
    Vector3 firstPoint;
    Vector3 secondPoint;
    Vector3 startFromPointWhenDrag;
    Sequence mainSequence;
    bool canTouch;
    float groundHeight = 0f;
    private float followTargetSpeed = 10f;
    float rotationSpeed = 0.5f;
    private bool isRotating = false;
    private float disAwayFromFront = 15.5f;
    private float zoomScreenDis = 10;
    private float zoomSpeed = 20;
    private float Height
    {
        get => height;
        set => height = Mathf.Clamp(value, 10, 20);
    }
    float fieldOfView;
    
    public TouchTopDownCamera(float height, float battlefieldDiameter, float fieldOfView)// height == 9
    {
        this.height = height;
        this.battlefieldDiameter = battlefieldDiameter;
        this.fieldOfView = fieldOfView;
    }
    
    Vector3 sameHeightCenter;
    public override void Enter(Camera _camera)
    {
        _camera.fieldOfView = this.fieldOfView;
        CameraManager._subCamera.fieldOfView = this.fieldOfView;
        
        Vector3 temp = Vector3.zero;
        Height = cameraManager.TopDownModeEndRef.position.y;
        mainSequence = DOTween.Sequence().OnStart(() =>
        {
            canTouch = false;
            sameHeightCenter = new Vector3(0,height,0);
            temp = _camera.transform.forward;
            temp.y = 0;
        }).Append(_camera.transform.DOMove(cameraManager.TopDownModeEndRef.position, startPosSetDuration)).
        Join(_camera.transform.DORotateQuaternion(cameraManager.TopDownModeEndRef.rotation, startPosSetDuration)).
        AppendCallback(() =>
        {
            zoomScreenDis = Screen.width / 7;
            canTouch = true;
        });
        mainSequence.Play();
    }
    
    private float backDist = 0.0f;
    float startCameraHeight;
    public override void LocalUpdate(Camera camera)
    {
        if (!canTouch)
        {
            return;
        }
        
        var RotateCameraH = UltimateJoystick.GetHorizontalAxis("RotateCamera");
        var RotateCameraV = UltimateJoystick.GetHorizontalAxis("RotateCamera");

        bool OnPad()
        {
            return RotateCameraH != 0 || RotateCameraV != 0;
        }
        
        if (Input.touchCount >= 2)
        {
            // タッチしている２点を取得
            Touch t1 = Input.GetTouch (0);
            Touch t2 = Input.GetTouch (1);
            if (t2.phase == TouchPhase.Began)
            {
                //2点タッチ開始時の距離を記憶
                backDist = Vector2.Distance (t1.position, t2.position);
                firstPoint = t2.position;
                startFromPointWhenDrag = camera.transform.position;
                startCameraHeight = height;
            }
            else if ((t1.phase == TouchPhase.Moved || t2.phase == TouchPhase.Moved) && (t1.phase != TouchPhase.Ended && t2.phase != TouchPhase.Ended))
            {
                var afterDist = Vector2.Distance (t1.position, t2.position);
                if (Mathf.Abs(afterDist - backDist) >  zoomScreenDis)
                {
                    float deltaHeight;
                    if (afterDist > backDist)
                    {
                        deltaHeight = - (afterDist - backDist - zoomScreenDis);
                    }
                    else
                    {
                        deltaHeight = backDist - afterDist - zoomScreenDis;
                    }
                    Height = startCameraHeight + (deltaHeight / Screen.height) * zoomSpeed;
                    camera.transform.position = new Vector3(camera.transform.position.x,Height, camera.transform.position.z);
                }
                else
                {
                    //move
                    secondPoint = t2.position;
                    if (meCenter == null)
                    {
                        CameraDrag(camera, startFromPointWhenDrag, firstPoint, secondPoint);
                    }
                }
            }
            else if (t1.phase == TouchPhase.Ended || t2.phase == TouchPhase.Ended)
            {
            }
        }
        else if (Input.touchCount >= 1)
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
                    CameraRotate(camera, firstPoint, t1.position);
                firstPoint = t1.position;
            }
            else if (t1.phase == TouchPhase.Ended) // 触摸结束
            {
                isRotating = false;
            }
        }
        else // editor
        {
            float scrollWheelValue = Input.GetAxis("Mouse ScrollWheel");
            if (scrollWheelValue != 0)
            {
                Height += scrollWheelValue;
                camera.transform.position = new Vector3(camera.transform.position.x,Height, camera.transform.position.z);
            }
            else if (Input.GetMouseButtonDown(0) || Input.GetMouseButton(0))
            {
                if (Input.GetMouseButtonDown(0))
                {
                    firstPoint = Input.mousePosition;
                    startFromPointWhenDrag = camera.transform.position;
                }
                if (Input.GetMouseButton(0))
                {
                    secondPoint = Input.mousePosition;
                    if (meCenter == null)
                    {
                        CameraDrag(camera, startFromPointWhenDrag, firstPoint, secondPoint);
                    }
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
                        CameraRotate(camera, firstPoint, Input.mousePosition);
                    firstPoint = Input.mousePosition;
                }
                else if (Input.GetMouseButtonUp(1))
                {
                    isRotating = false;
                }
            }
        }

        if (meCenter != null)
        {
            var mePosOnGround= meCenter.position;
            mePosOnGround.y = groundHeight;
            camera.transform.position = Vector3.Lerp(camera.transform.position, mePosOnGround + (camera.transform.position - GetCenterScreenGroundPoint(camera)), Time.deltaTime * followTargetSpeed);
        }
        
        //ClampHeightPos(camera);
        
        var cameraFront = camera.transform.forward;
        cameraFront.y = 0;
        cameraFront = cameraFront.normalized;
        sameHeightCenter = new Vector3(0, camera.transform.position.y, 0) - cameraFront * disAwayFromFront;
        if (Vector3.Distance(camera.transform.position, sameHeightCenter) > battlefieldDiameter / 2)
        {
            camera.transform.position = sameHeightCenter +
                                        (camera.transform.position - sameHeightCenter).normalized *
                                        battlefieldDiameter / 2;
        }
    }
    
    void CameraDrag(Camera camera, Vector3 startPoint, Vector3 _firstPoint, Vector3 _secondPoint)
    {
        var transform = camera.transform;
        var Right = transform.right;
        var Front = transform.forward;
        Front.y = 0;
        Front = Front.normalized;
        
        var rightDirectionMove = battlefieldDiameter * (-(_secondPoint.x - _firstPoint.x) / Screen.width)  * Right;
        var forwardDirectionMove = battlefieldDiameter * (-(_secondPoint.y - _firstPoint.y) / Screen.height)  * Front;
        var position = startPoint + rightDirectionMove + forwardDirectionMove;
        transform.position = position;
    }
    
    void CameraRotate(Camera camera, Vector3 _firstPoint, Vector3 _secondPoint)
    {
        float deltaX = _secondPoint.x - _firstPoint.x;
        float rotationAngle = deltaX * rotationSpeed;
        Vector3 pivotPoint = GetCenterScreenGroundPoint(camera);
        camera.transform.RotateAround(pivotPoint, Vector3.up, rotationAngle);
    }
    
    private Vector3 GetCenterScreenGroundPoint(Camera camera)
    {
        // 获取摄像机的位置和角度
        Vector3 cameraPosition = camera.transform.position;
        Vector3 cameraForward = camera.transform.forward;
        
        // 计算摄像机到地面的高度差
        float heightDifference = cameraPosition.y - groundHeight;

        // 计算摄像机forward在xz平面的投影
        Vector3 cameraForwardXZ = new Vector3(cameraForward.x, 0, cameraForward.z);

        // 计算夹角theta
        float theta = Vector3.Angle(cameraForward, cameraForwardXZ);
        float thetaRad = Mathf.Deg2Rad * theta;

        // 使用相似三角形计算距离
        float distanceToGround = heightDifference / Mathf.Tan(thetaRad);
        // 计算投射点的位置
        Vector3 groundPoint = cameraPosition + cameraForwardXZ.normalized * distanceToGround;
        groundPoint.y = groundHeight;
        return groundPoint;
    }
}
