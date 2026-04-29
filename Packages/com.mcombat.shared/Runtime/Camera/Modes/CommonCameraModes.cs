using System.Collections.Generic;
using MCombat.Shared.Camera;
using UnityEngine;

public class LerpToCertainDistance : CameraModeCore
{
    readonly float distanceFromTarget;
    Vector3 targetCenter;

    public LerpToCertainDistance(float distance, float speed)
    {
        distanceFromTarget = distance;
        this.speed = speed;
    }

    public override void LocalUpdate(UnityEngine.Camera camera)
    {
        if (camera == null || !TryGetAveragePosition(targets, out targetCenter))
        {
            return;
        }

        camera.transform.position = Vector3.Distance(targetCenter, camera.transform.position) > distanceFromTarget
            ? Vector3.Lerp(camera.transform.position, targetCenter, speed * Time.deltaTime)
            : Vector3.Lerp(camera.transform.position, (camera.transform.position - targetCenter) + camera.transform.position, speed * Time.deltaTime);
    }
}

public class WatchOverCamera : CameraModeCore
{
    Vector3 direction = Vector3.zero;
    Quaternion toRotation;
    Vector3 center;

    public WatchOverCamera(float distance, float height)
    {
        targets = new List<Transform>();
        XZDis = distance;
        YDis = height;
    }

    public override void LocalUpdate(UnityEngine.Camera camera)
    {
        if (camera == null || targets == null || targets.Count == 0)
        {
            return;
        }

        center = Vector3.zero;
        direction = Vector3.zero;
        var validCount = 0;
        foreach (var oneTarget in targets)
        {
            if (oneTarget != null)
            {
                center += oneTarget.transform.position;
                direction += oneTarget.transform.forward;
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return;
        }

        center /= validCount;
        direction = Quaternion.AngleAxis(XZrosOffset, Vector3.up) * direction;
        var to = center + direction.normalized * XZDis + new Vector3(0, YDis, 0);
        camera.transform.position = Vector3.Lerp(camera.transform.position, to, speed * Time.deltaTime);
        toRotation = Quaternion.LookRotation(center - to);
        camera.transform.rotation = Quaternion.Slerp(camera.transform.rotation, toRotation, Time.deltaTime / (0.2f + Time.deltaTime));
    }
}

public class GodplayerCamera : CameraModeCore
{
    float distanceUse = 1f;
    readonly float distance;
    readonly float zoomRange;
    float x;
    float y;
    public float perspectiveZoomSpeed = 0.5f;
    public float orthoZoomSpeed = 0.5f;
    Vector3 direction = Vector3.forward;
    Vector3 center;

    public GodplayerCamera(float distance, float zoomRange)
    {
        this.distance = distance;
        distanceUse = distance;
        this.zoomRange = zoomRange;
    }

    public override void LocalUpdate(UnityEngine.Camera camera)
    {
        if (camera == null || !TryGetAveragePosition(targets, out center))
        {
            return;
        }

        speed = 1f;
        if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.OSXEditor ||
            Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.OSXPlayer)
        {
            if (Input.GetAxis("Mouse ScrollWheel") < 0)
            {
                if (distanceUse > distance - zoomRange)
                {
                    distanceUse -= 0.1f;
                }
            }

            if (Input.GetAxis("Mouse ScrollWheel") > 0)
            {
                if (distanceUse < distance + zoomRange)
                {
                    distanceUse += 0.1f;
                }
            }

            if (System.Math.Abs(Input.GetAxis("Mouse X")) > 0)
            {
                x = Input.GetAxis("Mouse X") * speed * Time.deltaTime;
                direction = GetDirection(direction, x, 0);
            }

            if (System.Math.Abs(Input.GetAxis("Mouse Y")) > 0)
            {
                y = Input.GetAxis("Mouse Y") * speed * 30 * Time.deltaTime;
                direction = GetDirection(direction, 0, -y);
            }
        }
        else if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
        {
            if (Input.touchCount == 2)
            {
                var touchZero = Input.GetTouch(0);
                var touchOne = Input.GetTouch(1);
                var touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
                var touchOnePrevPos = touchOne.position - touchOne.deltaPosition;
                var prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
                var touchDeltaMag = (touchZero.position - touchOne.position).magnitude;
                var deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

                if (camera.orthographic)
                {
                    camera.orthographicSize += deltaMagnitudeDiff * orthoZoomSpeed;
                    camera.orthographicSize = Mathf.Max(camera.orthographicSize, 0.1f);
                }
                else
                {
                    camera.fieldOfView += deltaMagnitudeDiff * perspectiveZoomSpeed;
                    camera.fieldOfView = Mathf.Clamp(camera.fieldOfView, 5f, 90.9f);
                }
            }

            if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Moved)
            {
                direction = GetDirection(direction, Input.GetTouch(0).deltaPosition.x * 60f / Screen.width, Input.GetTouch(0).deltaPosition.y * 50f / Screen.height);
            }
        }

        if (camera.transform.position != center - direction * distanceUse)
        {
            var to = center - direction * distanceUse;
            camera.transform.position = Vector3.Lerp(camera.transform.position, to, Vector3.Distance(camera.transform.position, to) * speed * Time.deltaTime);
            camera.transform.LookAt(center, Vector3.up);
        }
    }
}

public class GodPlayerCertainY : CameraModeCore
{
    Vector3 xi;
    Vector3 center;
    Quaternion toRotation;

    public GodPlayerCertainY(float xzDis, float yDis)
    {
        XZDis = xzDis;
        YDis = yDis;
    }

    public override void LocalUpdate(UnityEngine.Camera camera)
    {
        if (camera == null || !TryGetAveragePosition(targets, out center))
        {
            return;
        }

        xi = center - Vector3.forward * XZDis;
        xi.y = YDis;
        toRotation = Quaternion.LookRotation(center - xi);
        camera.transform.position = Vector3.Lerp(camera.transform.position, xi, Time.deltaTime / (0.1f + Time.deltaTime));
        camera.transform.rotation = Quaternion.Slerp(camera.transform.rotation, toRotation, Time.deltaTime / (2f + Time.deltaTime));
    }
}

public class ScreenSaverC : CameraModeCore
{
    Vector3 cameraTargetPos;
    Vector3 enemiesCenter;
    Quaternion toRotation;
    Vector3 rotateToDirection;
    Vector2 meScreenPos;
    Vector2 enemyScreenPos;
    Vector3 xzOff = Vector3.forward;
    float h = 0.3f;

    public ScreenSaverC(float xzDis, float yDis)
    {
        XZDis = xzDis;
        YDis = yDis;
    }

    public override void LocalUpdate(UnityEngine.Camera camera)
    {
        if (camera == null || meCenter == null)
        {
            return;
        }

        if (auto)
        {
            if (TryGetAveragePosition(targets, out enemiesCenter))
            {
                enemiesCenter.y = 0;
                enemyScreenPos = camera.WorldToViewportPoint(enemiesCenter);
                meScreenPos = camera.WorldToViewportPoint(meCenter.position);
                if (enemyScreenPos.x < 0.08 || enemyScreenPos.x > 0.92 || enemyScreenPos.y < 0.2 || enemyScreenPos.y > 0.9 ||
                    meScreenPos.x < 0.08 || meScreenPos.x > 0.92 || meScreenPos.y < 0.2 || meScreenPos.y > 0.9)
                {
                    if (XZDis < 15)
                    {
                        XZDis += Time.deltaTime * 1.5f;
                        YDis += Time.deltaTime * 1.5f;
                    }
                }
                else
                {
                    if (enemyScreenPos.x > 0.45 || enemyScreenPos.x < 0.55 || enemyScreenPos.y > 0.45 || enemyScreenPos.y < 0.55 ||
                        meScreenPos.x > 0.45 || meScreenPos.x < 0.55 || meScreenPos.y > 0.45 || meScreenPos.y < 0.55)
                    {
                        if (XZDis > 8.5)
                        {
                            XZDis -= Time.deltaTime;
                            YDis -= Time.deltaTime;
                        }
                    }
                }
            }

            xzOff = Quaternion.AngleAxis(h, Vector3.up) * xzOff;
        }

        cameraTargetPos = (meCenter.position + enemiesCenter) / 2 + xzOff.normalized * XZDis;
        cameraTargetPos += Vector3.up * YDis;
        camera.transform.position = Vector3.Lerp(camera.transform.position, cameraTargetPos, Time.deltaTime / (0.2f + Time.deltaTime));
        rotateToDirection = ((meCenter.position + enemiesCenter) / 2 + Vector3.up * 2f) - cameraTargetPos;
        toRotation = Quaternion.LookRotation(rotateToDirection.normalized);
        camera.transform.rotation = Quaternion.Slerp(camera.transform.rotation, toRotation, Time.deltaTime / (0.2f + Time.deltaTime));
    }
}

public class ChatGptFix2 : CameraModeCore
{
    public float radius = 20;
    public float height = 8;
    public float angle = 45;
    public float rotationSpeed = 100;
    public float panSpeed = 100;
    Vector3 offset;
    Vector3 circleCenter;
    float rotationY;

    public override void Enter(UnityEngine.Camera camera)
    {
        circleCenter = Vector3.zero;
        offset = new Vector3(0, height, -radius);
        camera.transform.position = circleCenter + offset;
        camera.transform.rotation = Quaternion.Euler(angle, 0, 0);
    }

    public override void LocalUpdate(UnityEngine.Camera camera)
    {
        if (target != null)
        {
            var targetPosition = new Vector3(target.position.x, 0, target.position.z);
            camera.transform.position = targetPosition + offset;
            circleCenter = targetPosition;
        }
        else if (Input.GetMouseButton(1) || (Input.touchCount == 2 && Input.GetTouch(1).phase == TouchPhase.Moved))
        {
            var panX = Input.GetAxis("Mouse X") * panSpeed * Time.deltaTime;
            var panZ = Input.GetAxis("Mouse Y") * panSpeed * Time.deltaTime;
            var direction = new Vector3(panX, 0, panZ);
            circleCenter = Vector3.ClampMagnitude(circleCenter + direction, radius);
            camera.transform.position = circleCenter + offset;
        }

        if (Input.GetMouseButton(0) || (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Moved))
        {
            rotationY += Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
            var rotation = Quaternion.Euler(angle, rotationY, 0);
            offset = rotation * new Vector3(0, height, -radius);
            camera.transform.position = circleCenter + offset;
            camera.transform.rotation = rotation;
        }
    }
}
