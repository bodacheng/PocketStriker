using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DummyLayerSystem;
using UnityEngine;
using NoSuchStudio.UI.Highlight;
using UniRx;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class HighLightLayer : UILayer
{
    [SerializeField] Image bigCurtain;
    
    // canvasSortOrder = 100，这个数字无非是想让黑幕变成最上层，
    // 黑幕是靠缕空来实现空区域可点击。
    // PopupLayer 的 sortingOrder为更上一层101，
    // 因为popup出来的东西原则都是高亮
    
    // 这个模块的黑幕系统分两种，一种是部分区域高亮显示，一种是全屏颜色
    // 之所以不得不分两种是因为那么傻逼插件在计算高亮区域的时候不得不延迟两帧，否则计算不对
    // 从而有些需要立刻让全屏变色的地方我们要准备另外一套
    
    #region 高亮显示
    public static void HighLightRect(RectTransform r, Options options = null)
    {
        var highLightLayer = UILayerLoader.Load<HighLightLayer>();
        highLightLayer._HighLightRect(r, options);
    }
    
    async void _HighLightRect(RectTransform r, Options options = null)
    {
        bigCurtain.raycastTarget = true; // 防止下面那点间隔里有点击画面的空间
        await Observable.TimerFrame(2); // 没有这个间隔ShowForUI的计算可能出错
        bigCurtain.raycastTarget = false;
        
        if (r != null)
        {
            HighlightUI.ShowForUI(r,
                options ?? new Options()
                {
                    padding = new Padding(0,0,0,0),
                    fadeDuration = 0.5f,
                    dismissOnClick = false,
                    color = new Color(1,1,1,0f),
                    canvasSortOrder = 100
                }
            );
        }
    }
    #endregion
    
    #region 黑幕
    public static void DarkOff(Color color, float duration)
    {
        var highLightLayer = UILayerLoader.Load<HighLightLayer>();
        highLightLayer.bigCurtain.raycastTarget = true;
        highLightLayer.bigCurtain.DOColor(color, duration);
    }
    
    public static void LightUp(float duration)
    {
        var highLightLayer = UILayerLoader.Get<HighLightLayer>();
        if (highLightLayer != null)
        {
            highLightLayer.bigCurtain.DOColor(new Color(0,0,0, 0), duration).OnComplete(() =>
            {
                highLightLayer.bigCurtain.raycastTarget = false;
                Close();
            });
        }
    }
    
    #endregion
    
    public static void Close()
    {
        HighlightUI.Dismiss();
        UILayerLoader.Remove<HighLightLayer>();
    }
}
