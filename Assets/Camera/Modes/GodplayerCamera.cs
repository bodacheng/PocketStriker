using UnityEngine;

class GodplayerCamera : CameraMode
{
    private float distance_use = 1f;
    private readonly float distance;
    private readonly float zoom_range;
    float x, y;
    public float perspectiveZoomSpeed = 0.5f;        // The rate of change of the field of view in perspective mode.
    public float orthoZoomSpeed = 0.5f;        // The rate of change of the orthographic size in orthographic mode
    Vector3 direction = Vector3.forward;

    public GodplayerCamera(float distance, float zoom_range)
    {
        this.distance = distance;
        this.distance_use = distance;
        this.zoom_range = zoom_range;
    }

    Vector3 center;
    public override void LocalUpdate(Camera _camera)
    {
        if (targets == null)
        {
            //Debug.Log ("GOdPlayer观看模式无目标");
            return;
        }
        if (targets.Count == 0)
        {
            //Debug.Log ("GOdPlayer观看模式无目标");
            return;
        }

        center = Vector3.zero;
        foreach (Transform o in this.targets)
        {
            if (o != null)
                center += o.transform.position;
        }
        center /= targets.Count;

        speed = 1f;
        if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.OSXEditor ||
            Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.OSXPlayer)
        {
            if (UnityEngine.Input.GetAxis("Mouse ScrollWheel") < 0)
            {
                if (distance_use > distance - zoom_range)
                    distance_use -= 0.1f;
            }
            if (UnityEngine.Input.GetAxis("Mouse ScrollWheel") > 0)
            {
                if (distance_use < distance + zoom_range)
                    distance_use += 0.1f;
            }

            if (System.Math.Abs(UnityEngine.Input.GetAxis("Mouse X")) > 0)
            {
                x = UnityEngine.Input.GetAxis("Mouse X") * speed * Time.deltaTime;
                direction = GetDirection(direction, x, 0);
            }
            if (System.Math.Abs(UnityEngine.Input.GetAxis("Mouse Y")) > 0)
            {
                y = UnityEngine.Input.GetAxis("Mouse Y") * speed * 30 * Time.deltaTime;
                direction = GetDirection(direction, 0, -y);
            }
        }
        else if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
        {
            if (UnityEngine.Input.touchCount == 2)
            {
                // Store both touches.
                Touch touchZero = UnityEngine.Input.GetTouch(0);
                Touch touchOne = UnityEngine.Input.GetTouch(1);

                // Find the position in the previous frame of each touch.
                Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
                Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

                // Find the magnitude of the vector (the distance) between the touches in each frame.
                float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
                float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

                // Find the difference in the distances between each frame.
                float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

                // If the camera is orthographic...
                if (_camera.orthographic)
                {
                    // ... change the orthographic size based on the change in distance between the touches.
                    _camera.orthographicSize += deltaMagnitudeDiff * orthoZoomSpeed;

                    // Make sure the orthographic size never drops below zero.
                    _camera.orthographicSize = Mathf.Max(_camera.orthographicSize, 0.1f);
                }
                else
                {
                    // Otherwise change the field of view based on the change in distance between the touches.
                    _camera.fieldOfView += deltaMagnitudeDiff * perspectiveZoomSpeed;

                    // Clamp the field of view to make sure it's between 0 and 180.
                    _camera.fieldOfView = Mathf.Clamp(_camera.fieldOfView, 5f, 90.9f);
                }
            }
            if (UnityEngine.Input.touchCount == 1)
            {
                //_camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation,Quaternion.LookRotation(center - _camera.transform.position,Vector3.up),Time.deltaTime);
                if (UnityEngine.Input.GetTouch(0).phase == TouchPhase.Moved)
                {
                    direction = GetDirection(direction, UnityEngine.Input.GetTouch(0).deltaPosition.x * 60f / Screen.width, UnityEngine.Input.GetTouch(0).deltaPosition.y * 50f / Screen.height);
                }
            }
        }

        if (_camera.transform.position != center - direction * distance_use)
        {
            Vector3 to = center - direction * distance_use;
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, to, Vector3.Distance(_camera.transform.position, to) * speed * Time.deltaTime);
            _camera.transform.LookAt(center, Vector3.up);
        }
    }
}