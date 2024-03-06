using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OffsetScrolling : MonoBehaviour {
    
    [SerializeField] float scrollSpeed = 1;
    [SerializeField] RawImage img;
    [SerializeField] float _x, _y;
    
    void Update ()
    {
        img.uvRect = new Rect(img.uvRect.position + new Vector2(_x, _y) * Time.deltaTime * scrollSpeed, img.uvRect.size);
    }
}