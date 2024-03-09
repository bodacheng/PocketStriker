using System;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SizeAdjustBySpriteSize : MonoBehaviour
{
    [SerializeField] Image image;
    [SerializeField] bool fixedHeight = true;
    [SerializeField] bool adjustOnStart = false;
    
    void Start()
    {
        if (adjustOnStart)
            AdjustSize();
    }

    public void AdjustSize()
    {
        var sprite = image.sprite;
        var rectTransform = transform.GetComponent<RectTransform>();
        if (fixedHeight)
        {
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, sprite.rect.width * rectTransform.rect.height / sprite.rect.height);
        }
        else
        {
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, sprite.rect.height * rectTransform.rect.width / sprite.rect.width);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SizeAdjustBySpriteSize))]
public class SizeAdjustBySpriteSizeGUI : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var _target = (SizeAdjustBySpriteSize)target;
        if (GUILayout.Button("Adjust Size"))
        {
            _target.AdjustSize();
        }
    }
}
#endif