using DummyLayerSystem;
using UnityEngine;

public class ExBarBurn : MonoBehaviour
{
    public ParticleSystem explosionFigure;
    RectTransform rectTransform;
    Camera cachedFxCamera;

    void Awake()
    {
        rectTransform = transform as RectTransform;
        OnLoad();
    }
    
    async void OnLoad()
    {
        explosionFigure = await AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/ui_exbarburn");
        var layer = UILayerLoader.Get<FightingStepLayer>();
        if (layer != null)
            explosionFigure.transform.SetParent(layer.transform);
    }

    private void OnDestroy()
    {
        if (explosionFigure != null)
        {
            Destroy(explosionFigure.gameObject);
        }
    }

    void OnDisable()
    {
        Burn();
    }

    void Burn()
    {
        if (explosionFigure != null)
        {
            if (!TryResolveFxCamera(out var fxCamera))
            {
                return;
            }

            explosionFigure.transform.position = PosCal.GetWorldPos(fxCamera,
                rectTransform != null ? rectTransform : transform.GetComponent<RectTransform>(), 3);
            explosionFigure.Play();
        }
    }

    bool TryResolveFxCamera(out Camera camera)
    {
        if (cachedFxCamera != null)
        {
            camera = cachedFxCamera;
            return true;
        }

        cachedFxCamera = null;
        if (FightScene.FightScene.target != null && FightScene.FightScene.target.fxCamera != null)
        {
            cachedFxCamera = FightScene.FightScene.target.fxCamera;
            camera = cachedFxCamera;
            return true;
        }

        if (Camera.main != null)
        {
            camera = Camera.main;
            return true;
        }

        camera = null;
        return false;
    }
}
