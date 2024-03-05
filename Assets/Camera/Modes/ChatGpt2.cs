using UnityEngine;

class ChatGptFix2 : CameraMode
{
    public float radius = 20; // 竞技场半径
    public float height = 8; // 相机高度
    public float angle = 45; // 俯视角度
    public float rotationSpeed =100 ; // 旋转速度
    public float panSpeed = 100; // 平移速度

    private Vector3 offset;
    private Vector3 circleCenter;
    private float rotationY;
    
    public ChatGptFix2()
    {
    }
    
    public override void Enter(Camera camera)
    {
        circleCenter = Vector3.zero; // 设置竞技场中心
        offset = new Vector3(0, height, -radius);
        camera.transform.position = circleCenter + offset;
        camera.transform.rotation = Quaternion.Euler(angle, 0, 0);
    }
    
    public override void LocalUpdate(Camera camera)
    {
        if (target != null) // 如果有跟踪目标
        {
            Vector3 targetPosition = new Vector3(target.position.x, 0, target.position.z);
            camera.transform.position = targetPosition + offset;
            circleCenter = targetPosition;
        }
        else
        {
            if (Input.GetMouseButton(1) || (Input.touchCount == 2 && Input.GetTouch(1).phase == TouchPhase.Moved))
            {
                float panX = Input.GetAxis("Mouse X") * panSpeed * Time.deltaTime;
                float panZ = Input.GetAxis("Mouse Y") * panSpeed * Time.deltaTime;

                Vector3 direction = new Vector3(panX, 0, panZ);
                circleCenter = Vector3.ClampMagnitude(circleCenter + direction, radius);
                camera.transform.position = circleCenter + offset;
            }
        }

        if (Input.GetMouseButton(0) || (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Moved))
        {
            rotationY += Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
            Quaternion rotation = Quaternion.Euler(angle, rotationY, 0);
            offset = rotation * new Vector3(0, height, -radius);
            camera.transform.position = circleCenter + offset;
            camera.transform.rotation = rotation;
        }
    }
}