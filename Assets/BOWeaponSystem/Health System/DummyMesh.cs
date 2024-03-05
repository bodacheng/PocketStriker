using UnityEngine;

public class DummyMesh : AbstractShaderMesh
{
    //property
    private static readonly int kPropertyAlpha = Shader.PropertyToID("_Alpha");

    void SetAlpha(float alpha)
    {
        foreach (var m in GetMaterials())
        {
            m.SetFloat(kPropertyAlpha, alpha);
        }
    }
    
    protected override void Awake()
    {
        base.Awake();
        SetAlpha(0f); // reset to clear
    }
}
