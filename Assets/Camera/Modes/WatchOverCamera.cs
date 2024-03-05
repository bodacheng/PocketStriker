using System.Collections.Generic;
using UnityEngine;

class WatchOverCamera : CameraMode
{
    Vector3 direction = Vector3.zero;
    Quaternion ToRotation;
    
    public WatchOverCamera(float distance, float height)
    {
        targets = new List<Transform>();
        XZDis = distance;
        YDis = height;
    }
    
    Vector3 center;
    public override void LocalUpdate(Camera _camera)
    {
        if (targets == null || targets.Count == 0)
            return;
            
        center = Vector3.zero;
        direction = Vector3.zero;
        foreach (Transform o in this.targets)
        {
            //amount++;
            if (o != null)
            {
                center += o.transform.position;
                direction += o.transform.forward;
            }
            //average = Quaternion.Slerp(average, o.rotation, 1 / amount); //得到所有对象的平均旋转值。这个我们并没有详细认证过到底对不对。其实如果我们真动用这个模式的话肯定不会指定多个聚焦对象
        }
        center /= targets.Count;
        direction =  Quaternion.AngleAxis(XZrosOffset, Vector3.up) * direction;
        Vector3 to = center + direction.normalized * XZDis + new Vector3(0, YDis, 0);   
        _camera.transform.position = Vector3.Lerp(_camera.transform.position, to, speed * Time.deltaTime);
        ToRotation = Quaternion.LookRotation(center - to);
        _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, ToRotation, Time.deltaTime / (0.2f + Time.deltaTime));
    }
}