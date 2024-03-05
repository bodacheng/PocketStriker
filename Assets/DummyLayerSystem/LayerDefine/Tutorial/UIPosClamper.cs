using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIPosClamper : MonoBehaviour
{
    [SerializeField] RectTransform targetUIElement;
    [SerializeField] RectTransform startPoint;
    // Start is called before the first frame update
    void OnEnable()
    {
        targetUIElement.position = startPoint.position;
    }
}
