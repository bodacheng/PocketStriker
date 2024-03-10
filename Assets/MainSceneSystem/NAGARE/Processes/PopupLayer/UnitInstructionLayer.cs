using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;

public class UnitInstructionLayer : UILayer
{
    [SerializeField] private RawImage bgImage;
    [SerializeField] private Text gameTipTitle;
    [SerializeField] private Text gameTip;
    
    private readonly List<TweenerCore<Color, Color, ColorOptions>> _tweenerCores = new List<TweenerCore<Color, Color, ColorOptions>>();
    
    void ChangeUnitTheme()
    {
        bgImage.color = Color.black; // Start from transparent
        _tweenerCores.Add(bgImage.DOColor(Color.white, 0.2f).SetEase(Ease.Linear));
        _tweenerCores.Add(gameTip.DOColor(Color.white, 0.2f).SetEase(Ease.Linear));
        _tweenerCores.Add(gameTipTitle.DOColor(Color.white, 0.2f).SetEase(Ease.Linear));
        
        var tip = Translate.GetRandomGameTip();

        gameTipTitle.text = tip[0];
        gameTip.text = tip[1];
    }
    
    public void LoadUnitImage()
    {
        ChangeUnitTheme();
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
