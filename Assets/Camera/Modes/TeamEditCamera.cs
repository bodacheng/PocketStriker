using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class TeamEditCamera : CameraMode
{
    public float distance, height;
    Vector3 direction = Vector3.zero;

    public TeamEditCamera(float distance, float height)
    {
        targets = new List<Transform>();
        this.distance = distance;
        this.height = height;
    }

    public override void LocalUpdate(Camera _camera)
    {
        if (this.targets == null)
        {
            //Debug.Log ("观看模式无目标");
            return;
        }
        if (this.targets.Count == 0)
        {
            //Debug.Log ("观看模式无目标");
            return;
        }

        Vector3 center = new Vector3(0, 0, 0);
        //Quaternion average = new Quaternion(0, 0, 0, 0);
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
        float speed = 1f;
        if (_camera.transform.position != center - direction * distance + new Vector3(0, height, 0))
        {
            Vector3 to = center - direction.normalized * distance + new Vector3(0, height, 0);
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, to, speed * Time.deltaTime);
        }
        Vector3 directionLook = center - _camera.transform.position;
        Quaternion toRotation = Quaternion.FromToRotation(_camera.transform.forward, directionLook);
        _camera.transform.rotation = Quaternion.Lerp(_camera.transform.rotation, toRotation, speed * Time.deltaTime);
    }
}