using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using DummyLayerSystem;

public class ProgressLayer : UILayer
{
    [SerializeField] Slider progressBar;
    [SerializeField] Text percentage;
    [SerializeField] Text info;
    [SerializeField] Image bigCurtain;
    bool _layingOut;
    bool _progressStyled;

    protected override void OnAreasResized() => RefreshLoadingLayout();

    public void RefreshLoadingLayout()
    {
        if (_layingOut || info == null || percentage == null || progressBar == null || !(transform is RectTransform root)) return;
        var safe = LoadingScreenLayout.SafeBounds(root);
        if (safe.width <= 0 || safe.height <= 0) return;
        _layingOut = true;
        try
        {
            if (!_progressStyled)
            {
                // Detach the copy before disabling the fixed-width animated ornament.
                var decoration = info.transform.parent;
                info.transform.SetParent(root, false);
                if (decoration != null && decoration.name == "LoadingIcon") decoration.gameObject.SetActive(false);
                percentage.transform.SetParent(root, false);
                StyleProgressBar();
                _progressStyled = true;
            }
            float margin = LoadingScreenLayout.Margin(safe);
            float width = Mathf.Min(1000f, safe.width - margin * 2);
            float left = safe.center.x - width * 0.5f;
            float footerTop = safe.yMin + LoadingScreenLayout.FooterHeight(safe);
            LoadingScreenLayout.ConfigureText(info, 32, TextAnchor.LowerLeft);
            LoadingScreenLayout.ConfigureText(percentage, 30, TextAnchor.MiddleRight);
            info.lineSpacing = 1.1f;
            percentage.color = new Color(0.62f, 0.76f, 0.81f, 1);
            float descriptionHeight = Mathf.Min(LoadingScreenLayout.TextHeight(info, width - 112), 86);
            LoadingScreenLayout.Place(info.rectTransform, root, new Rect(left, footerTop - 92, width - 112, Mathf.Max(42, descriptionHeight)));
            LoadingScreenLayout.Place(percentage.rectTransform, root, new Rect(left + width - 104, footerTop - 92, 104, 42));
            LoadingScreenLayout.Place((RectTransform)progressBar.transform, root, new Rect(left, footerTop - 128, width, 16));
            info.transform.SetAsLastSibling();
            percentage.transform.SetAsLastSibling();
            progressBar.transform.SetAsLastSibling();
            percentage.gameObject.SetActive(progressBar.gameObject.activeSelf);
        }
        finally { _layingOut = false; }
    }

    void StyleProgressBar()
    {
        progressBar.transition = Selectable.Transition.None;
        progressBar.interactable = false;
        var sprite = Resources.Load<Sprite>("UI/Preparation/PreparationButtonFill");
        foreach (var image in progressBar.GetComponentsInChildren<Image>(true))
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1;
            image.raycastTarget = false;
            image.color = image.transform == progressBar.fillRect
                ? new Color(0.20f, 0.70f, 0.78f, 1) : new Color(0.16f, 0.23f, 0.29f, 1);
        }
        var fillParent = progressBar.fillRect != null ? progressBar.fillRect.parent as RectTransform : null;
        if (fillParent != null && fillParent != progressBar.transform)
        {
            fillParent.anchorMin = Vector2.zero;
            fillParent.anchorMax = Vector2.one;
            fillParent.offsetMin = fillParent.offsetMax = Vector2.zero;
        }
        if (progressBar.fillRect != null) progressBar.fillRect.offsetMin = progressBar.fillRect.offsetMax = Vector2.zero;
    }
    
    // 「正在读取」画面
    public static void Loading(string description, float curtainAlpha = 0.8f)
    {
        var layer = UILayerLoader.Load<ProgressLayer>(true, null, true);
        if (layer != null)
        {
            currentTween?.Kill();
            currentTween = null;
            layer.DarkOff(curtainAlpha,0.5f);
            layer.info.text = description;
            layer.progressBar.gameObject.SetActive(false);
            layer.progressBar.value = 0;
            layer.percentage.text = "0%";
            layer.RefreshLoadingLayout();
        }
    }

    // Downloads must be presentable before any remote resources are available.
    public static void Downloading(string description)
    {
        var layer = UILayerLoader.Load<ProgressLayer>(true, null, true);
        if (layer == null) return;

        currentTween?.Kill();
        currentTween = null;
        layer.bigCurtain.DOKill();
        layer.bigCurtain.color = Color.black;
        layer.bigCurtain.raycastTarget = true;
        layer.bigCurtain.transform.SetAsFirstSibling();
        layer.info.text = description;
        layer.progressBar.gameObject.SetActive(true);
        SetProgressValue(layer, 0);
        layer.RefreshLoadingLayout();
    }

    #region 黑幕
    void DarkOff(float darkness, float duration)
    {
        bigCurtain.DOKill();
        bigCurtain.raycastTarget = true;
        bigCurtain.DOColor(new Color(0,0,0, darkness), duration).SetLink(gameObject);
    }

    public static void LightUp(float duration)
    {
        var popupLayer = UILayerLoader.Get<ProgressLayer>();
        if (popupLayer != null && popupLayer.bigCurtain != null)
        {
            var layer = popupLayer;
            var curtain = popupLayer.bigCurtain;
            curtain.DOKill();
            curtain.DOColor(new Color(0,0,0, 0), duration).SetLink(layer.gameObject).OnComplete(() =>
            {
                if (layer == null || curtain == null || layer.IsClosing || UILayerLoader.Get<ProgressLayer>() != layer)
                {
                    return;
                }

                curtain.raycastTarget = false;
                Close();
            });
        }
    }
    #endregion
    
    private static Tween currentTween;

    static void SetProgressValue(ProgressLayer layer, float progress)
    {
        if (layer == null)
        {
            return;
        }

        layer.progressBar.value = progress;
        layer.percentage.text = ((int)(progress * 100)) + "%";
    }

    // 带进度条的正在读取画面。不会主动打开新的popuplayer
    public static void LoadingPercent(string description, float progress, bool tween = true)
    {
        var layer = UILayerLoader.Get<ProgressLayer>();
        if (layer == null)
        {
            return;
        }
        
        layer.progressBar.gameObject.SetActive(true);
        layer.info.text = description;
        layer.RefreshLoadingLayout();
        progress = Mathf.Clamp01(progress);
        
        currentTween?.Kill();
        currentTween = null;
        
        if (tween)
        {
            currentTween = DOTween.To
            (
                () => layer.progressBar.value,
                x => SetProgressValue(layer, x),
                progress,
                1
            ).SetEase(Ease.OutFlash).SetLink(layer.gameObject);
        }
        else
        {
            var currentValue = layer.progressBar.value;
            var delta = Mathf.Abs(progress - currentValue);
            if (delta <= 0.001f)
            {
                SetProgressValue(layer, progress);
                return;
            }

            var duration = Mathf.Clamp(delta * 0.35f, 0.08f, 0.2f);
            currentTween = DOTween.To(
                    () => layer.progressBar.value,
                    x => SetProgressValue(layer, x),
                    progress,
                    duration)
                .SetEase(Ease.OutCubic)
                .SetLink(layer.gameObject);
        }
    }
    
    public static void Close()
    {
        currentTween?.Kill();
        currentTween = null;
        var layer = UILayerLoader.Get<ProgressLayer>();
        if (layer != null && layer.bigCurtain != null) layer.bigCurtain.DOKill();
        UILayerLoader.Remove<ProgressLayer>();
    }
}
