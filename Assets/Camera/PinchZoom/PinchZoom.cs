using UnityEngine;

public class PinchZoom
{
    public Camera camera;
    readonly float _perspectiveZoomSpeed = 0.5f;        // The rate of change of the field of view in perspective mode.
    readonly float _orthoZoomSpeed = 0.5f;        // The rate of change of the orthographic size in orthographic mode.    
    Touch _touchZero;
    Touch _touchOne;
    float _touchZeroScreenPosX;
    float _touchZeroScreenPosY;
    Vector2 _touchZeroPrevPos;
    Vector2 _touchOnePrevPos;
    float _prevTouchDeltaMag;
    float _touchDeltaMag;
    float _deltaMagnitudeDiff;
    
    public void LocalUpdate()
    {
        // If there are two touches on the device...
        if (Input.touchCount == 2)
        {
            // Store both touches.
            _touchZero = UnityEngine.Input.GetTouch(0);
            _touchOne = UnityEngine.Input.GetTouch(1);
            _touchZeroScreenPosX = _touchZero.position.x / Screen.width;
            _touchZeroScreenPosY = _touchZero.position.y / Screen.height;
            
            if (_touchZeroScreenPosX < 0.1f || _touchZeroScreenPosX > 0.5f || _touchZeroScreenPosY < 0.1f || _touchZeroScreenPosY > 0.8f)
            {
                return;// 点击位置太靠近屏幕边缘。只有在画面左边的手指操作才能zoom相机。
            }

            // Find the position in the previous frame of each touch.
            _touchZeroPrevPos = _touchZero.position - _touchZero.deltaPosition;
            _touchOnePrevPos = _touchOne.position - _touchOne.deltaPosition;

            // Find the magnitude of the vector (the distance) between the touches in each frame.
            _prevTouchDeltaMag = (_touchZeroPrevPos - _touchOnePrevPos).magnitude;
            _touchDeltaMag = (_touchZero.position - _touchOne.position).magnitude;

            // Find the difference in the distances between each frame.
            _deltaMagnitudeDiff = _prevTouchDeltaMag - _touchDeltaMag;

            // If the camera is orthographic...
            if (camera.orthographic)
            {
                // ... change the orthographic size based on the change in distance between the touches.
                camera.orthographicSize += _deltaMagnitudeDiff * _orthoZoomSpeed;
                // Make sure the orthographic size never drops below zero.
                camera.orthographicSize = Mathf.Max(camera.orthographicSize, 0.1f);
            }
            else
            {
                // Otherwise change the field of view based on the change in distance between the touches.
                camera.fieldOfView += _deltaMagnitudeDiff * _perspectiveZoomSpeed;
                // Clamp the field of view to make sure it's between 0 and 180.
                camera.fieldOfView = Mathf.Clamp(camera.fieldOfView, 15f, 25f);
            }
        }
    }
}
