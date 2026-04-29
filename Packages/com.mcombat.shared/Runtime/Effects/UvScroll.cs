//======================================
/*
@autor ktk.kumamoto
@date 2015.3.6 create
@note UvScroll
*/
//======================================

using UnityEngine;

public class UvScroll : MonoBehaviour
{
    public float scrollSpeed_u = 0.5f;
    public float scrollSpeed_v = 0.5f;

    Renderer rend;

    void Start()
    {
        rend = GetComponent<Renderer>();
    }

    void Update()
    {
        if (rend == null)
        {
            return;
        }

        var offsetU = Time.time * -scrollSpeed_u;
        var offsetV = Time.time * -scrollSpeed_v;
        rend.material.SetTextureOffset("_MainTex", new Vector2(offsetU, offsetV));
    }
}
