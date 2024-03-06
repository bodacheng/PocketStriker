using UnityEngine;

public class MidAreaSizeHelper : MonoBehaviour
{
    [SerializeField] private bool keepTopPos;
    [SerializeField] private float height;
    [SerializeField] RectTransform rectTransform;
    
    public void Resize()
    {
        if (!keepTopPos)
        {
            SetHeightKeepBottomFixed(height);
        }
        else
        {
            SetHeightKeepTopFixed(height);
        }
    }
    
    // 设置高度，保持底部位置不变
    void SetHeightKeepTopFixed(float newHeight)
    {
        rectTransform.offsetMin = new Vector2(rectTransform.offsetMin.x, PosCal.CanvasHeight - (-rectTransform.offsetMax.y) - newHeight);
    }

    // 设置高度，保持顶部位置不变
    void SetHeightKeepBottomFixed(float newHeight)
    {
        rectTransform.offsetMax = new Vector2(rectTransform.offsetMax.x, -(PosCal.CanvasHeight - rectTransform.offsetMin.y - newHeight));
    }
}
