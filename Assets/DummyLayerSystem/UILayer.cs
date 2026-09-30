using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class UILayer : MonoBehaviour
{
    [SerializeField] private RectTransform top;
    [SerializeField] private RectTransform middle;
    [SerializeField] private RectTransform bottom;

    public RectTransform TopArea => top;
    public RectTransform MiddleArea => middle;
    public RectTransform BottomArea => bottom;
    bool _resizingAreas;
    bool _areasInitialized;
    bool _middleLayoutCaptured;
    Vector2 _middleVerticalAnchors;
    Vector2 _middleVerticalOffsets;

    protected virtual void OnRectTransformDimensionsChange()
    {
        if (_areasInitialized && !_resizingAreas) ResizeAreas();
    }
    
    public void ResizeAreas()
    {
        if (_resizingAreas || top == null || middle == null || bottom == null ||
            !(transform is RectTransform root))
        {
            return;
        }

        _resizingAreas = true;
        try
        {
            float height = root.rect.height;
            if (height <= 0) return;
            if (!_middleLayoutCaptured)
            {
                _middleVerticalAnchors = new Vector2(middle.anchorMin.y, middle.anchorMax.y);
                _middleVerticalOffsets = new Vector2(middle.offsetMin.y, middle.offsetMax.y);
                _middleLayoutCaptured = true;
            }

            // Safe-area children already have the inset in their parent geometry.
            // Full-screen layers intersect that geometry once, without double insets.
            float lower = 0, upper = height;
            var safe = PosCal.SafeAreaRect;
            if (safe != null && root != safe && !root.IsChildOf(safe))
            {
                var corners = new Vector3[4];
                safe.GetWorldCorners(corners);
                lower = Mathf.Clamp(root.InverseTransformPoint(corners[0]).y - root.rect.yMin, 0, height);
                upper = Mathf.Clamp(root.InverseTransformPoint(corners[1]).y - root.rect.yMin, lower, height);
            }

            // Apply the authored margins to the available safe height. Merely
            // clipping a full-screen middle would consume the header/footer's
            // space on notched devices. Reuse the original values on every pass
            // so resizing or changing the safe area cannot accumulate offsets.
            float safeHeight = upper - lower;
            SetVerticalEdges(middle,
                lower + _middleVerticalAnchors.x * safeHeight + _middleVerticalOffsets.x,
                lower + _middleVerticalAnchors.y * safeHeight + _middleVerticalOffsets.y, height);
            middle.GetComponent<MidAreaSizeHelper>()?.Resize();
            var offsets = CalculateOffsetForFullScreenAnchors(middle);
            float middleBottom = Mathf.Clamp(offsets.Item1.y, lower, upper);
            float middleTop = Mathf.Clamp(height + offsets.Item2.y, middleBottom, upper);
            SetVerticalEdges(middle, middleBottom, middleTop, height);
            SetVerticalEdges(top, middleTop, upper, height);
            SetVerticalEdges(bottom, lower, middleBottom, height);
            _areasInitialized = true;
            OnAreasResized();
        }
        finally { _resizingAreas = false; }
    }

    protected virtual void OnAreasResized() { }

    static void SetVerticalEdges(RectTransform rect, float lower, float upper, float parentHeight)
    {
        // Preserve horizontal anchors/offsets and the authored pivot.
        var min = rect.offsetMin;
        var max = rect.offsetMax;
        min.y = lower - rect.anchorMin.y * parentHeight;
        max.y = upper - rect.anchorMax.y * parentHeight;
        rect.offsetMin = min;
        rect.offsetMax = max;
    }
    
    (Vector2, Vector2) CalculateOffsetForFullScreenAnchors(RectTransform rectTransform)
    {
        // 获取父元素的RectTransform
        RectTransform parentRectTransform = rectTransform.parent as RectTransform;

        // 当前锚点相对于父元素的位置
        Vector2 anchorMin = rectTransform.anchorMin;
        Vector2 anchorMax = rectTransform.anchorMax;

        // 计算当前锚点对应的偏移量
        Vector2 parentSize = parentRectTransform.rect.size;
        Vector2 offsetMin = rectTransform.offsetMin + new Vector2(anchorMin.x * parentSize.x, anchorMin.y * parentSize.y);
        Vector2 offsetMax = rectTransform.offsetMax - new Vector2((1 - anchorMax.x) * parentSize.x, (1 - anchorMax.y) * parentSize.y);

        // 这里返回的就是在Anchors为(0, 0)和(1, 1)情况下对应的offsetMin和offsetMax
        return (offsetMin, offsetMax);
    }
    
    public string Index { get; set; }
    public bool IsClosing { get; set; }
    
    public virtual void OnDestroy()
    {
        DummyLayerSystem.UILayerLoader.NotifyDestroyed(this);
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
    
    protected float SetGridGroupSizeForUnitBox(GridLayoutGroup grid, int unitsCount)
    {
        // 获取父对象的宽度
        RectTransform rect = grid.transform.GetComponent<RectTransform>();
        float parentWidth = rect.rect.width;
        float parentHeight = rect.rect.height;
        int row = Mathf.CeilToInt((float)unitsCount / grid.constraintCount);
        float cellHeight = (parentHeight / row) - grid.spacing.y;
     
        // 根据父对象的宽度，左右padding和格子间距来计算每个格子的大小
        float cellWidth =  (parentWidth - grid.spacing.x * (grid.constraintCount - 1)) / grid.constraintCount;
        float smaller = Mathf.Min(cellWidth, cellHeight);
        grid.cellSize = new Vector2(smaller, smaller);
        return smaller;
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
