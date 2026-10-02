using UnityEngine;
using UnityEngine.UI;

/// <summary>A persistent action cue, independent of pooled particle playback.</summary>
public sealed class BattleActionGlyph : MaskableGraphic
{
    public bool DreamCombo;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var rect = rectTransform.rect;
        var size = Mathf.Min(rect.width, rect.height);
        void Triangle(Vector2 a, Vector2 b, Vector2 c)
        {
            int start = vh.currentVertCount;
            foreach (var point in new[] { a, b, c })
                vh.AddVert((Vector3)(point * size + rect.center), color, Vector2.zero);
            vh.AddTriangle(start,start+1,start+2);
        }
        if (DreamCombo)
        {
            Triangle(new Vector2(.04f,.28f),new Vector2(-.19f,-.03f),new Vector2(.12f,-.03f));
            Triangle(new Vector2(-.12f,.03f),new Vector2(.19f,.03f),new Vector2(-.04f,-.28f));
        }
        else
        {
            foreach (float x in new[]{-.16f,.08f})
            {
                Triangle(new Vector2(x-.1f,.19f),new Vector2(x+.1f,0),new Vector2(x-.1f,-.19f));
            }
        }
    }
}
