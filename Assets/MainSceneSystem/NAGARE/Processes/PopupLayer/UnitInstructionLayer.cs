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
    bool _layingOut;

    protected override void OnAreasResized() => RefreshLoadingLayout();

    public void SetTip(string title, string body)
    {
        gameTipTitle.text = title ?? string.Empty;
        gameTip.text = (body ?? string.Empty).Replace("\\n", "\n");
        RefreshLoadingLayout();
    }

    public void RefreshLoadingLayout()
    {
        if (_layingOut || gameTipTitle == null || gameTip == null || !(transform is RectTransform root)) return;
        var safe = LoadingScreenLayout.SafeBounds(root);
        if (safe.width <= 0 || safe.height <= 0) return;
        _layingOut = true;
        try
        {
            float margin = LoadingScreenLayout.Margin(safe);
            float width = Mathf.Min(1000f, safe.width - margin * 2);
            float left = safe.center.x - width * 0.5f;
            float lower = safe.yMin + LoadingScreenLayout.FooterHeight(safe) + margin;
            float upper = safe.yMax - margin;
            float available = Mathf.Max(1, upper - lower);
            LoadingScreenLayout.ConfigureText(gameTipTitle, Mathf.RoundToInt(Mathf.Clamp(width * 0.066f, 40, 64)), TextAnchor.UpperLeft);
            LoadingScreenLayout.ConfigureText(gameTip, Mathf.RoundToInt(Mathf.Clamp(width * 0.048f, 30, 44)), TextAnchor.UpperLeft);
            gameTipTitle.fontStyle = FontStyle.Bold;
            gameTip.lineSpacing = 1.24f;
            const float gap = 32;
            float titleHeight = LoadingScreenLayout.TextHeight(gameTipTitle, width);
            float bodyHeight = LoadingScreenLayout.TextHeight(gameTip, width);
            while (titleHeight + gap + bodyHeight > available && gameTip.fontSize > 24)
            {
                gameTip.fontSize -= 2;
                bodyHeight = LoadingScreenLayout.TextHeight(gameTip, width);
            }
            bodyHeight = Mathf.Min(bodyHeight, Mathf.Max(1, available - titleHeight - gap));
            float blockHeight = titleHeight + gap + bodyHeight;
            float top = upper - Mathf.Max(0, available - blockHeight) * 0.44f;
            LoadingScreenLayout.Place(gameTipTitle.rectTransform, root, new Rect(left, top - titleHeight, width, titleHeight));
            LoadingScreenLayout.Place(gameTip.rectTransform, root, new Rect(left, top - blockHeight, width, bodyHeight));
            gameTipTitle.transform.SetAsLastSibling();
            gameTip.transform.SetAsLastSibling();
        }
        finally { _layingOut = false; }
    }
    
    void ChangeUnitTheme()
    {
        bgImage.color = Color.black; // Start from transparent
        _tweenerCores.Add(bgImage.DOColor(Color.white, 0.2f).SetEase(Ease.Linear));
        _tweenerCores.Add(gameTip.DOColor(Color.white, 0.2f).SetEase(Ease.Linear));
        _tweenerCores.Add(gameTipTitle.DOColor(Color.white, 0.2f).SetEase(Ease.Linear));
        
        var tip = Translate.GetRandomGameTip();

        SetTip(tip.Length > 0 ? tip[0] : string.Empty, tip.Length > 1 ? tip[1] : string.Empty);
    }
    
    public void LoadUnitImage()
    {
        ChangeUnitTheme();
    }

    public override void OnDestroy()
    {
        foreach (var tweener in _tweenerCores)
        {
            if (tweener != null && tweener.IsActive())
                tweener.Kill();
        }

        base.OnDestroy();
    }
}
