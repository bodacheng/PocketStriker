using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ConfirmBtnColorSwapper : MonoBehaviour
{
    [SerializeField] private List<Text> texts;
    [SerializeField] private List<Image> images;

    public void ChangeColor(Color color)
    {
        foreach (var text in texts)
        {
            text.color = color;
        }

        foreach (var image in images)
        {
            image.color = color;
        }
    }
}
