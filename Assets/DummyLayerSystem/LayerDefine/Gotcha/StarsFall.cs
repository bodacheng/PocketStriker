using DG.Tweening;
using UnityEngine;

public class StarsFall : MonoBehaviour
{
    public enum GachaType {
        Normal,
        Super
    }
    
    [SerializeField] Camera _camera;
    [SerializeField] Camera _eCamera;
    [SerializeField] Transform cameraStartPoint;
    [SerializeField] float skySphereRadius = 650;
    [SerializeField] GameObject dmGotchaBackground;
    [SerializeField] GameObject gdGotchaBackground;

    [SerializeField] private float starShinePointFromDoom = 5;
        
    public static StarsFall target;

    public Camera Camera => _camera;
    public Camera ECamera => _eCamera;
    
    void Awake()
    {
        target = this;
        Turn(false);
    }

    public void Turn(bool on)
    {
        target.gameObject.SetActive(on);
        _camera.gameObject.SetActive(on);
    }

    public void LookReset()
    {
        _camera.transform.DOMove(cameraStartPoint.position, 1);
    }

    private GachaType type;
    public void TriggerHoleEffect(GachaType type)
    {
        this.type = type;
        dmGotchaBackground.SetActive(type == GachaType.Super);
        gdGotchaBackground.SetActive(type == GachaType.Normal);
    }
    
    public Vector3 GetRandomStarPos(bool center = false)
    {
        if (center)
        {
            var temp = dmGotchaBackground.transform.position;
            temp.y = dmGotchaBackground.transform.position.y - starShinePointFromDoom;
            return temp;
        }
        else
        {
            var xzDisFromCenter = Random.Range(skySphereRadius * 0.5f, skySphereRadius);
            var temp = dmGotchaBackground.transform.position + (Vector3.forward * Random.Range(-100, 100) + Vector3.right * Random.Range(-100, 100)).normalized * xzDisFromCenter;
            temp.y = dmGotchaBackground.transform.position.y - starShinePointFromDoom;
            return temp;
        }
    }

    public Vector3 GetRandomStarPosCameraLookPos(Vector3 starPos)
    {
        return new Vector3(starPos.x, cameraStartPoint.position.y, starPos.z);
    }
}
