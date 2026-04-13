using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class AbstractShaderMesh : MonoBehaviour
{
    protected Material[] GetMaterials() => Mesh != null ? Mesh.sharedMaterials : null;
    protected Renderer mesh = default;

    private static readonly int kPropertyBaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int kPropertyEmissionColor2 = Shader.PropertyToID("_EmissionColor");
    private static readonly int kPropertyEmissionColor1 = Shader.PropertyToID("_Emissive");

    public Renderer Mesh => mesh;
    public Material[] CurrentMaterials { get; set; } = System.Array.Empty<Material>();

    protected virtual void Awake()
    {
        mesh = GetComponent<Renderer>();
        if (mesh != null)
        {
            var materials = new List<Material>();
            foreach (var m in Mesh.sharedMaterials)
            {
                if (m == null || m.shader == null)
                {
                    continue;
                }
                var new_m = new Material(m.shader);
                new_m.CopyPropertiesFromMaterial(m);
                new_m.EnableKeyword("_EMISSION");
                materials.Add(new_m);
            }
            mesh.sharedMaterials = materials.ToArray();
            CurrentMaterials = GetMaterials() ?? System.Array.Empty<Material>();
        }
        else
        {
            CurrentMaterials = System.Array.Empty<Material>();
        }
    }

    public Color BaseColor
    {
        get
        {
            if (CurrentMaterials == null)
                return Color.clear;
            foreach (var m in CurrentMaterials)
            {
                if (m == null)
                    continue;
                return m.GetColor(kPropertyBaseColor);
            }
            return Color.clear;
        }
        set
        {
            if (CurrentMaterials == null)
                return;
            foreach (var m in CurrentMaterials)
            {
                if (m == null)
                    continue;
                m.SetColor(kPropertyBaseColor, value);
            }
        }
    }

    public Color EmissionColor
    {
        get
        {
            if (CurrentMaterials == null)
                return Color.clear;
            foreach (var m in CurrentMaterials)
            {
                if (m == null)
                    continue;
                if (m.HasProperty(kPropertyEmissionColor1))
                {
                    return m.GetColor(kPropertyEmissionColor1);
                }
                if (m.HasProperty(kPropertyEmissionColor2))
                    return m.GetColor(kPropertyEmissionColor2);
            }
            return Color.clear;
        }
        set
        {
            if (CurrentMaterials == null)
                return;
            foreach (var m in CurrentMaterials)
            {
                if (m == null)
                    continue;
                if (m.HasProperty(kPropertyEmissionColor1))
                {
                    m.SetColor(kPropertyEmissionColor1,value);
                }
                    
                if (m.HasProperty(kPropertyEmissionColor2))
                    m.SetColor(kPropertyEmissionColor2,value);
            }
        }
    }
}
