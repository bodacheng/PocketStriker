using System;
using DummyLayerSystem;
using UnityEngine;

public class ExBarBurn : MonoBehaviour
{
    public ParticleSystem explosionFigure;
    void Awake()
    {
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
            explosionFigure.transform.position = PosCal.GetWorldPos(FightScene.FightScene.target.fxCamera,
                transform.GetComponent<RectTransform>(), 3);
            explosionFigure.Play();
        }
    }
}
