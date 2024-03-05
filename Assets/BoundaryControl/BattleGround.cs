using UnityEngine;

public class BattleGround : MonoBehaviour
{
    [SerializeField] Vector3 pos;
    [SerializeField] Quaternion ros;
    [SerializeField] Vector3 scale;

    public void Set()
    {
        transform.position = pos;
        transform.rotation = ros;
        transform.localScale = scale;
    }
}