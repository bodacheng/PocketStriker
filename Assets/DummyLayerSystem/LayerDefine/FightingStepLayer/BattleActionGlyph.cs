using UnityEngine;
using UnityEngine.UI;

/// <summary>A persistent action cue, independent of pooled particle playback.</summary>
public sealed class BattleActionGlyph : MaskableGraphic
{
    public bool DreamCombo;
    Texture _sprintTexture;
    Material _sprintMaterial;
    public bool ShowsSprintPerson => !DreamCombo && _sprintTexture != null && _sprintMaterial != null && isActiveAndEnabled;
    public override Texture mainTexture => DreamCombo ? base.mainTexture : _sprintTexture;

    public void Configure(bool dreamCombo)
    {
        DreamCombo=dreamCombo;
        if (!dreamCombo)
        {
            // Reuse the exact authored rushing-person texture; its black JPEG
            // background is removed in the UI shader, without altering the art.
            if (_sprintTexture == null) _sprintTexture=Resources.Load<Texture2D>("BasicSprites/dash-rush");
            if (_sprintMaterial == null) _sprintMaterial=new Material(Resources.Load<Shader>("BasicSprites/DashRush"));
        }
        material=dreamCombo?null:_sprintMaterial;
        SetMaterialDirty();SetVerticesDirty();
    }

    protected override void OnDestroy()
    {
        if (_sprintMaterial != null)
        {
            if(Application.isPlaying) Destroy(_sprintMaterial); else DestroyImmediate(_sprintMaterial);
        }
        base.OnDestroy();
    }
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
            const float half=.34f;
            foreach(var point in new[]{new Vector2(-half,-half),new Vector2(-half,half),new Vector2(half,half),new Vector2(half,-half)})
                vh.AddVert((Vector3)(point*size+rect.center),color,new Vector2(point.x/half*.5f+.5f,point.y/half*.5f+.5f));
            vh.AddTriangle(0,1,2);vh.AddTriangle(2,3,0);
        }
    }
}
