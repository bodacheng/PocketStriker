using UnityEngine;
using UnityEngine.UI;

public static class PosCal
{
    public static Canvas Canvas;
    static CanvasScaler CanvasScaler => Canvas.GetComponent<CanvasScaler>();
    public static float CanvasWidth => Canvas.GetComponent<RectTransform>().rect.width;
    public static float CanvasHeight => Canvas.GetComponent<RectTransform>().rect.height;
    
    /// <summary>
    /// 九宫格的slot特效在比1920x1080更长的设备上并不会出现尺寸变不匹配问题
    /// 但是如果在长宽比例更低的设备上，比如ipad，就是出现尺寸错误
    /// 我们没有理清内部的具体逻辑，但是我们认为更低的长宽比设备上，这个设备长宽比/参考长宽比的数字就是slot特效的scale应该乘以的数字
    /// 而事实证明似乎没错
    /// </summary>
    /// <returns></returns>
    public static float TempRate()
    {
        float screenAspect = (float)Screen.width / Screen.height;
        float refAspect = CanvasScaler.referenceResolution.x / CanvasScaler.referenceResolution.y;
        // if (screenAspect >= refAspect) // 这个处理（分歧，这种情况返回1）在横版项目是需要的，纵版却不需要，原因还没理解
        // {
        //     return 1;
        // }
        return screenAspect / refAspect;
    }
    
    public static float TempToko() // 这完全是个主观数值，目的是让手机比较长的时候立绘更靠中间一点。没有太多道理
    {
        float screenAspect = (float)Screen.width / Screen.height;
        float refAspect = CanvasScaler.referenceResolution.x / CanvasScaler.referenceResolution.y;
        if (screenAspect <= refAspect)
        {
            return 0;
        }
        return (((screenAspect / refAspect) -1)/2) * CanvasScaler.referenceResolution.x;
    }

    public static float VTopSafeAreaHeight => ((Screen.height - Screen.safeArea.size.y - Screen.safeArea.position.y) / Screen.height) * CanvasHeight;
    
    public static float VBottomSafeAreaHeight =>(Screen.safeArea.position.y / Screen.height) * CanvasHeight;

    /// <summary>
    /// 这个函数在目前所用的地方为什么能得到正确的值我们压根不理解。主要不理解rect.transform.position到底是什么
    /// </summary>
    /// <param name="refC"></param>
    /// <param name="rect"></param>
    /// <param name="zOffset"></param>
    /// <returns></returns>
    public static Vector3 GetWorldPos(Camera refC, RectTransform rect, float zOffset)
    {
        var rectPos = rect.transform.position;
        var trueAnchorPos = new Vector2(rectPos.x, rectPos.y);
        var worldPos = refC.ScreenToWorldPoint(trueAnchorPos);
        worldPos = new Vector3(worldPos.x, worldPos.y, refC.transform.position.z + zOffset);
        return worldPos;
    }
    
    /// 这个是一律和ConvertAnchorPos配合使用，rectPos指的是UI元素在Canvas内的anchoredPosition，它并不等同于Screen Position。
    /// 前者的最大值是Canvas的长和宽，后者的最大值是设备分辨率
    /// </param>
    /// <param name="rectPos"> 这个值是UI元素的坐标，指的应该是我们希望把某个特效给定到的位置 </param>
    /// <param name="zOffset"></param>
    /// <returns></returns>
    public static Vector3 GetWorldPos(Camera refC, Vector3 rectPos, float zOffset)
    {
        var screenPos = new Vector2(Screen.width * rectPos.x/ CanvasWidth, Screen.height * rectPos.y/ CanvasHeight);
        var worldPos = refC.ScreenToWorldPoint(screenPos);
        worldPos = new Vector3(worldPos.x, worldPos.y, refC.transform.position.z + zOffset);
        return worldPos;
    }
    
    /// <summary>
    /// 获取某ui元素在某个不同的anchor下，所在位置的anchorPosition值
    /// </summary>
    /// <param name="anchoredPosition">
    ///  formerAnchor下的原RectTransform.anchorPosition
    /// </param>
    /// <param name="formerAnchor">
    /// 之前的Anchor，比如说1，1代表 anchor在右上角
    /// </param>
    /// <param name="targetAnchor">
    /// 目标Anchor，比如说0，0代表 anchor在左下角
    /// </param>
    /// <returns></returns>
    // public static Vector3 ConvertAnchorPos(Vector3 anchoredPosition, Vector2 formerAnchor, Vector2 targetAnchor)
    // {
    //     float newX = canvasWidth - anchoredPosition.x * (targetAnchor.x - formerAnchor.x) / 1;
    //     float newY = canvasHeight - anchoredPosition.y * (targetAnchor.y - formerAnchor.y) / 1;
    //     
    //     Debug.Log("目标屏幕位置："+ new Vector3(newX, newY, 0));
    //     return new Vector3(newX, newY, 0);
    // }
    
    public static Vector2 CalculateAnchoredPositionInNewAnchor(RectTransform uiElement, Vector2 targetAnchor)
    {
        RectTransform parent = uiElement.parent as RectTransform;
        Vector2 parentSize = parent.rect.size;
        
        // Calculate the current position in the parent RectTransform space.
        Vector2 currentPositionInParentSpace = uiElement.anchorMin * parentSize + uiElement.anchoredPosition;

        // Calculate the new anchored position in the target anchor without modifying the RectTransform.
        Vector2 newPositionInTargetAnchor = currentPositionInParentSpace - targetAnchor * parentSize;
        
        return newPositionInTargetAnchor;
    }

    public static float AdjustedViewPortHeight(float originHeight, float itemHeight, float space)
    {
        var linesToShowInViewPort = 0;
        while (true)
        {
            if ((linesToShowInViewPort + 1) * (itemHeight + space) < originHeight)
            {
                linesToShowInViewPort++;
            }
            else
            {
                break;
            }
        }
        var viewPortHeight = linesToShowInViewPort * (itemHeight + space) - space;
        return viewPortHeight;
    }
}
