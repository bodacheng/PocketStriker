using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using NoSuchStudio.Common;

public class UnitInstructionLayer : UILayer
{
    [SerializeField] private List<string> unitRecordIds;
    [SerializeField] private Image bgImage;
    [SerializeField] private Image unitImage;
    [SerializeField] private Text unitName;
    [SerializeField] private Text unitIntro;
    [SerializeField] private Text gameTipTitle;
    [SerializeField] private Text gameTip;
    [SerializeField] private float unitImageEndPosX = -100f;
    [SerializeField] private float nameEndPosX = 100f;
    [SerializeField] private float emergeDuration = 2f;
    
    private readonly List<TweenerCore<float, float, FloatOptions>> _tweenerCores = new List<TweenerCore<float, float, FloatOptions>>();
    
    async void ChangeUnitTheme(string RECORD_ID)
    {
        var config = Units.GetUnitConfig(RECORD_ID);
        if (config == null)
            return;
        
        var loadTasks = new[]
        {
            AddressablesLogic.LoadT<Sprite>("unit_image/"+RECORD_ID),
            AddressablesLogic.LoadT<Sprite>("unit_bg/"+RECORD_ID)
        };

        var results = await UniTask.WhenAll(loadTasks);
        var value = results[0];
        var bgValue = results[1];
        if (value == null || bgValue == null)
        {
            return;
        }
        
        var targetBgWidth = bgValue.rect.width * (Screen.height/bgValue.rect.height);
        if (bgImage == null)
        {
            return;
        }
        bgImage.rectTransform.sizeDelta = new Vector2(targetBgWidth, bgImage.rectTransform.sizeDelta.y);
        bgImage.rectTransform.anchoredPosition = new Vector2();
        
        bgImage.color = Color.black; // Start from transparent
        bgImage.DOColor(Color.white, 0.2f).SetEase(Ease.Linear);
        bgImage.sprite = bgValue;
        
        var unitImageRect = unitImage.transform.GetComponent<RectTransform>();
        unitImageRect.sizeDelta = new Vector2(value.rect.width * unitImageRect.rect.height / value.rect.height, unitImageRect.rect.height);
        unitImage.sprite = value;
        
        unitName.text = Translate.Get(config.REAL_NAME);
        unitIntro.text = Translate.Get(config.REAL_NAME+ "_intro");

        var tip = Translate.GetRandomGameTip();

        gameTipTitle.text = tip[0];
        gameTip.text = tip[1];
        
        TweenerCore<float, float, FloatOptions> Move(RectTransform targetRect, float endXPos)
        {
            float posX = targetRect.anchoredPosition.x;
            return DOTween.To(()=> posX, (value)=> posX = value, endXPos, emergeDuration).
                OnUpdate(
                    () =>
                    {
                        targetRect.anchoredPosition = new Vector2(posX, targetRect.anchoredPosition.y);
                    }
                ).SetEase(Ease.InSine);
        }
        
        float targetBgEndPosX = -(targetBgWidth - Screen.width);
        _tweenerCores.Add(Move(bgImage.transform.GetComponent<RectTransform>(), targetBgEndPosX));
        _tweenerCores.Add(Move(unitImage.transform.GetComponent<RectTransform>(), unitImageEndPosX));
        _tweenerCores.Add(Move(unitName.transform.GetComponent<RectTransform>(), nameEndPosX));
    }
    
    public void LoadUnitImage()
    {
        ChangeUnitTheme(unitRecordIds.Random());
    }

    void OnDestroy()
    {
        foreach (var tweener in _tweenerCores)
        {
            if (tweener != null && tweener.IsActive())
                tweener.Kill();
        }
    }
}
