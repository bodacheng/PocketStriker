using UnityEngine;

class GodPlayerCertainY : CameraMode
{
    Vector3 Xi;
    Vector3 center;
    Quaternion ToRotation;

    public GodPlayerCertainY(float XZDis, float YDis)
    {
        this.XZDis = XZDis;
        this.YDis = YDis;
    }

    public override void LocalUpdate(Camera _camera)
    {
        if (targets == null || targets.Count == 0)
        {
            return;
        }
        
        center = Vector3.zero;
        foreach (Transform o in this.targets)
        {
            if (o != null)
            {
                center += o.transform.position;
            }
        }
        center /= targets.Count;

        Xi = center - Vector3.forward * XZDis;
        Xi.y = YDis;

        //_camera.transform.position = Vector3.Lerp(_camera.transform.position, Xi , Time.deltaTime/(1f + Time.deltaTime));
        //_camera.transform.position = Xi;
        //_camera.transform.LookAt(center, Vector3.up);
        ToRotation = Quaternion.LookRotation(center - Xi);
        _camera.transform.position = Vector3.Lerp(_camera.transform.position, Xi, Time.deltaTime / (0.1f + Time.deltaTime));//上下这两部分都是分母里那个附加值越大，变得越慢。
        //如果你想研究战斗系统中角色本身的发抖问题，可以把上面这个附加值给调节的比较小一些
        _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, ToRotation, Time.deltaTime / (2f + Time.deltaTime));
    }
}
