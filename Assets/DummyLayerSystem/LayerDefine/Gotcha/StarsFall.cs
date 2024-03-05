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
    [SerializeField] Transform center;
    [SerializeField] GameObject lookTarget;
    [SerializeField] float skySphereRadius = 650;
    [SerializeField] ParticleSystem normalGachaEffect;
    [SerializeField] ParticleSystem normalGachaExplode;
    [SerializeField] ParticleSystem superGachaEffect;
    [SerializeField] ParticleSystem superGachaExplode;
    
    public static StarsFall target;

    public Camera Camera => _camera;
    public Camera ECamera => _eCamera;

    public Vector3 GetEffectCenter()
    {
        return normalGachaEffect.transform.position;
    }

    void Awake()
    {
        target = this;
        target.gameObject.SetActive(false);
        
        superGachaEffect.Stop();
        superGachaExplode.Stop();
        normalGachaEffect.Stop();
        normalGachaExplode.Stop();
    }

    public void LookReset()
    {
        _camera.transform.DOLookAt(lookTarget.transform.position, 1f);
    }

    private GachaType type;
    public void TriggerHoleEffect(GachaType type)
    {
        this.type = type;
        switch (type)
        {
            case GachaType.Normal:
                superGachaEffect.Stop();
                normalGachaEffect.Play();
                break;
            case GachaType.Super:
                normalGachaEffect.Stop();
                superGachaEffect.Play();
                break;
        }
    }

    public void StartGachaEffect(bool on)
    {
        switch (type)
        {
            case GachaType.Normal:
                if (on)
                    normalGachaExplode.Play();
                else
                    normalGachaExplode.Stop();
                break;
            case GachaType.Super:
                if (on)
                    superGachaExplode.Play();
                else
                    superGachaExplode.Stop();
                break;
        }
    }
    
    public Vector3 GetRandomStarPos()
    {
        var xzDisFromCenter = Random.Range(skySphereRadius * 0.5f, skySphereRadius * 0.7f);
        var temp = center.transform.position + (Vector3.forward * Random.Range(-100, 100) + Vector3.right * Random.Range(-100, 100)).normalized * xzDisFromCenter;
        var height = Mathf.Sqrt(Mathf.Pow(skySphereRadius, 2) - Mathf.Pow(xzDisFromCenter, 2));
        var finalPos = temp + (int)(height - 10) * Vector3.up;
        return finalPos;
    }
}
