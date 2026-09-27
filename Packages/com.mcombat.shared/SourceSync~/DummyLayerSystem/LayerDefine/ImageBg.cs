using UnityEngine;
using UnityEngine.UI;
public class ImageBg : UILayer
{
    [SerializeField] float topAndBottomMargin;  // 在这里设置你想要的上下边距
    [SerializeField] Image p1, p2;

    public void Setup()
    {
        AdjustSize(p1);
        AdjustSize(p2);
    }
    
    void AdjustSize(Image myImage)
    {
        float newHeight = PosCal.CanvasHeight - 2 * topAndBottomMargin;
        float aspectRatio = myImage.sprite.rect.width / myImage.sprite.rect.height;

        float newWidth = newHeight * aspectRatio;

        myImage.rectTransform.sizeDelta = new Vector2(newWidth, newHeight);
    }
}
