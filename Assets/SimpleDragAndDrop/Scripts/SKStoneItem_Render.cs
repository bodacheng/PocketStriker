using System;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;

public partial class SKStoneItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public async void Shine(Camera refC)
    {
        var worldPos = PosCal.GetWorldPos(refC, transform.GetComponent<RectTransform>(), 5f);
        var slotEffect = await AddressablesLogic.LoadTOnObject<ParticleSystem>("ButtonEffects/stonePoweredUp");
        slotEffect.gameObject.name = "stoneShine";
        slotEffect.gameObject.transform.position = worldPos;
        slotEffect.Play(true);
        slotEffect.transform.SetParent(transform);
        
        Observable.Timer(TimeSpan.FromSeconds(3)).Subscribe(_ =>
        {
            Destroy(slotEffect.gameObject);
        }).AddTo(slotEffect.gameObject);
    }
    
    public static void SelectedRender(SKStoneItem item, GameObject _Selected)
    {
        if (item != null)
        {
            var cell = item.GetCell();
            StoneCell.SelectedRender(cell, _Selected);
        }
        else
        {
            StoneCell.SelectedRender(null, _Selected);
        }
    }
}