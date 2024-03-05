using UnityEngine;

public class LerpToCertainDistance : CameraMode
{
    readonly float distancefromtarget;
    public LerpToCertainDistance(float distance,float speed)
    {
        this.distancefromtarget = distance;
        this.speed = speed;
    }

    Vector3 targetcenter;
    public override void LocalUpdate(Camera _camera)
    {
        targetcenter = Vector3.zero;
        for (int i = 0; i < targets.Count;i++)
        {
            targetcenter += targets[i].position;
        }
        targetcenter = targetcenter / targets.Count;
        _camera.transform.position = Vector3.Distance(targetcenter, _camera.transform.position) > distancefromtarget
            ? Vector3.Lerp(_camera.transform.position, targetcenter, speed * Time.deltaTime)
            : Vector3.Lerp(_camera.transform.position, (_camera.transform.position - targetcenter) + _camera.transform.position, speed * Time.deltaTime);
    }
}
