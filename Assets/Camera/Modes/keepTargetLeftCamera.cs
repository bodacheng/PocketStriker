using UnityEngine;
using DG.Tweening;

class keepTargetLeftCamera : CameraMode
{
    Vector3 center = new Vector3(0, 0, 0);
    Quaternion torotation;

    public override void Enter(Camera _camera)
    {
        _camera.DOOrthoSize(4f,3f);
    }

    public override void LocalUpdate(Camera _camera)
    {
        if (this.targets == null || this.targets.Count == 0)
            return;

        center = Vector3.zero;
        foreach (Transform o in this.targets)
        {
            if (o != null)
                center += o.transform.position;
        }
        center /= targets.Count;
        _camera.transform.position = Vector3.Lerp(_camera.transform.position, center + new Vector3(-7f,1f,7f), 2* Time.deltaTime/(0.01f + Time.deltaTime));
        torotation = Quaternion.LookRotation(center - Vector3.right * 3 + Vector3.up * 1f - _camera.transform.position, Vector3.up);
        _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, torotation, 2* Time.deltaTime / (0.01f + Time.deltaTime));
        //_camera.transform.LookAt(center - Vector3.right * 6 + Vector3.up  * 1f, Vector3.up);
    }
}
