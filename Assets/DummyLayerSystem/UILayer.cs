using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class UILayer : MonoBehaviour
{
    [SerializeField] private RectTransform top;
    [SerializeField] private RectTransform middle;
    [SerializeField] private RectTransform bottom;
    
    public void ResizeAreas()
    {
        if (top == null ||middle == null || bottom == null)
        {
            return;
        }

        var midAreaSizeHelper = middle.GetComponent<MidAreaSizeHelper>();
        if (midAreaSizeHelper != null)
            midAreaSizeHelper.Resize();

        var offsetMinOffsetMax = CalculateOffsetForFullScreenAnchors(middle);
        
        float topAreaHeight = PosCal.CanvasHeight - offsetMinOffsetMax.Item1.y - middle.rect.height - PosCal.VTopSafeAreaHeight;
        top.anchoredPosition = new Vector2(0, - PosCal.VTopSafeAreaHeight);
        top.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, topAreaHeight);

        float downArenaHeight = PosCal.CanvasHeight + offsetMinOffsetMax.Item2.y - middle.rect.height - PosCal.VBottomSafeAreaHeight;
        bottom.anchoredPosition = new Vector2(0, PosCal.VBottomSafeAreaHeight);
        bottom.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, downArenaHeight);
    }
    
    (Vector2, Vector2) CalculateOffsetForFullScreenAnchors(RectTransform rectTransform)
    {
        // 获取父元素的RectTransform
        RectTransform parentRectTransform = rectTransform.parent as RectTransform;

        // 当前锚点相对于父元素的位置
        Vector2 anchorMin = rectTransform.anchorMin;
        Vector2 anchorMax = rectTransform.anchorMax;

        // 当前RectTransform的尺寸
        Vector2 sizeDelta = rectTransform.sizeDelta;

        // 计算当前锚点对应的偏移量
        Vector2 parentSize = parentRectTransform.rect.size;
        Vector2 offsetMin = rectTransform.offsetMin + new Vector2(anchorMin.x * parentSize.x, anchorMin.y * parentSize.y);
        Vector2 offsetMax = rectTransform.offsetMax - new Vector2((1 - anchorMax.x) * parentSize.x, (1 - anchorMax.y) * parentSize.y);

        // 这里返回的就是在Anchors为(0, 0)和(1, 1)情况下对应的offsetMin和offsetMax
        return (offsetMin, offsetMax);
    }
    
    public string Index { get; set; }
    
    public virtual void OnDestroy()
    {
        
    }
    
    protected void ResizeCameraConnectorRefLeft(RectTransform target, float cameraConnectorRightSpace, float cameraConnectorVerticalSpace)
    {
        var unitViewSize = (PosCal.CanvasWidth - cameraConnectorRightSpace);
        if (unitViewSize > PosCal.CanvasHeight - cameraConnectorVerticalSpace)
            unitViewSize = PosCal.CanvasHeight - cameraConnectorVerticalSpace;
        target.sizeDelta = new Vector2(unitViewSize, unitViewSize);
    }
    
    /// <summary>
    /// 这个使用的前提是privot (0.5,0)
    /// 四周stretch，保持顶部距离和距离屏幕两边距离，整出个正方形。
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cameraConnectorSideMinSpace"></param>
    /// <param name="toTopEdgeSpace"></param>
    protected void ResizeCameraConnectorRefTopAndSideWidth(RectTransform target, float toTopEdgeSpace, float toDownEdgeSpace = 0)
    {
        // 获取父对象的宽度
        float minHeight = PosCal.CanvasHeight - toDownEdgeSpace - toTopEdgeSpace;
        
        // 计算新的高度，这里的70是左边界和右边界的值
        float newHeight = PosCal.CanvasWidth - (target.offsetMin.x + target.offsetMax.x);
        newHeight = Mathf.Min(minHeight, newHeight);
        
        // 设置新的高度
        target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, newHeight);
        target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newHeight);
        target.anchoredPosition = new Vector2(0, -toTopEdgeSpace);
    }

    protected void ResizeCameraConnectorAsMaxSquare(RectTransform target, float maxWidth, float maxHeight)
    {
        if (maxWidth > maxHeight)
        {
            target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,  maxHeight);
        }
        else
        {
            target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,  maxWidth);
        }
    }
    
    protected void SetGridGroupSize(GridLayoutGroup grid, float paddingLeftRight)
    {
        // 获取父对象的宽度
        RectTransform parentRect = grid.transform.parent.GetComponent<RectTransform>();
        float parentWidth = parentRect.rect.width;

        // 根据父对象的宽度，左右padding和格子间距来计算每个格子的大小
        int cellsPerRow = grid.constraintCount; // 每行的格子数量
        float cellWidth =  (parentWidth - paddingLeftRight * 2 - grid.spacing.x * (cellsPerRow - 1)) / cellsPerRow;

        // 确保格子是正方形
        grid.cellSize = new Vector2(cellWidth, cellWidth);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="recordId"></param>
    /// <param name="view2D"></param>
    /// <param name="unitOutAnimator"></param>
    /// <param name="distanceToVerticalEdge"> 图片自身的pivot距离高度上（或下？）边缘的距离 </param>
    /// <param name="seenHeightProportionalOfWhole"> 漏出在画面中的高度是图片实际高度的百分之几 </param>
    /// <returns></returns>
    protected async UniTask<Sprite> Set2DView(string recordId, Image view2D, Animator unitOutAnimator, 
        float distanceToVerticalEdge ,float seenHeightProportionalOfWhole, float originX ,float extraYokoSpace)
    {
        string key = "unit_image/" + recordId;
        if (!AddressablesLogic.CheckKeyExist("unit_image", key))
        {
            unitOutAnimator.SetTrigger("reset");
            return null;
        }
        
        var value = await AddressablesLogic.LoadT<Sprite>(key);
        if (unitOutAnimator == null)
        {
            return null;
        }
        var unitImageRect = view2D.GetComponent<RectTransform>();

        float seenHeight = PosCal.CanvasHeight - distanceToVerticalEdge;
        float wholeHeight = seenHeight / seenHeightProportionalOfWhole;

        var anchoredPosition = unitImageRect.anchoredPosition;
        unitImageRect.anchoredPosition = new Vector2(originX + extraYokoSpace, anchoredPosition.y);
        unitImageRect.sizeDelta = new Vector2(value.rect.width * wholeHeight / value.rect.height, wholeHeight);
        view2D.sprite = value;
        unitOutAnimator.SetTrigger("select");
        return value;
    }

    protected void ToTop()
    {
        gameObject.transform.SetAsLastSibling();
    }
}
