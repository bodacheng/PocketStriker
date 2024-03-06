using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UniRx;
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
        var layer = UILayerLoader.Load<ProgressLayer>(true);
        if (layer != null)
        {
            layer.DarkOff(curtainAlpha,0.5f);
            layer.info.text = description;
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
    
    private static IDisposable current;
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
        
        if (current != null)
        {
            current.Dispose();
            current = null;
        }
        
        if (tween)
        {
            current = DOTween.To
            (
                () => layer.progressBar.value,
                (x) =>
                {
                    layer.progressBar.value = x;
                    layer.percentage.text = ((int)(x * 100)) + "%";
                },
                progress,
                1
            ).SetEase(Ease.OutFlash).OnCompleteAsObservable().Subscribe(_ => { }).AddTo(layer.gameObject);
        }
        else
        {
            layer.progressBar.value = progress;
        }
    }
    
    public static void Close()
    {
        UILayerLoader.Remove<ProgressLayer>();
    }
}
