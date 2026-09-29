using UnityEngine;

public class UIPosClamper : MonoBehaviour
{
    [SerializeField] RectTransform targetUIElement;
    [SerializeField] RectTransform startPoint;
    public RectTransform Target => targetUIElement;
    public RectTransform Source => startPoint;
    // Start is called before the first frame update
    void OnEnable()
    {
        targetUIElement.position = startPoint.position;
    }
}
