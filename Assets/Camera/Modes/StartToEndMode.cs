using UnityEngine;
using DG.Tweening;

public class StartToEndMode : CameraMode
{
    Vector3 obj_position;
    
    public void SetObjPosAndRotAndSpeed(Vector3 obj_position, float duration, float fieldOfView)
    {
        this.obj_position = obj_position;
        this.duration = duration;
        this.fieldOfView = fieldOfView;
    }
    
    public override void Enter(Camera _camera)
    {
        _camera.DOFieldOfView(fieldOfView, duration);
    }
    
    Quaternion obj_quaternion;
    public override void LocalUpdate(Camera _camera)
    {
        if (target == null)
            return;
        _camera.transform.position = Vector3.Lerp(_camera.transform.position, obj_position , Time.deltaTime * 5f);
        obj_quaternion = Quaternion.LookRotation(target.position -  obj_position , Vector3.up);
        _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, obj_quaternion, Time.deltaTime * 5f);
    }
}