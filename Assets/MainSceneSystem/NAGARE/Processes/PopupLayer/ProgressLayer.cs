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
        }
    }
    
    #region 黑幕
    void DarkOff(float darkness, float duration)
    {
        bigCurtain.raycastTarget = true;
        bigCurtain.DOColor(new Color(0,0,0, darkness), duration);
    }

    public static void LightUp(float duration)
    {
        var popupLayer = UILayerLoader.Get<ProgressLayer>();
        if (popupLayer != null)
        {
            popupLayer.bigCurtain.DOColor(new Color(0,0,0, 0), duration).OnComplete(() =>
            {
                popupLayer.bigCurtain.raycastTarget = false;
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
        UILayerLoader.Remove<ProgressLayer>();
    }
}
