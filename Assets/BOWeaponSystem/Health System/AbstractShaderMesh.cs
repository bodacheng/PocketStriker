using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class AbstractShaderMesh : MonoBehaviour
{
    protected Material[] GetMaterials() => Mesh != null ? Mesh.sharedMaterials : null;
    protected Renderer mesh = default;

    private static readonly int kPropertyBaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int kPropertyColor = Shader.PropertyToID("_Color");
    private static readonly int kPropertyEmissionColor2 = Shader.PropertyToID("_EmissionColor");
    private static readonly int kPropertyEmissionColor1 = Shader.PropertyToID("_Emissive");

    public Renderer Mesh => mesh;
    public Material[] CurrentMaterials { get; set; } = System.Array.Empty<Material>();
    private readonly List<Material> _ownedMaterials = new List<Material>();

    protected virtual void Awake()
    {
        mesh = GetComponent<Renderer>();
        if (mesh != null)
        {
            var materials = Mesh.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                var m = materials[i];
                if (m == null || m.shader == null)
                {
                    continue;
                }
                // Keep every material slot, texture, keyword and disabled pass.
                // Only URP Lit uses the _EMISSION keyword; OmniShade uses
                // _Emissive directly and must retain its authored keywords.
                var new_m = new Material(m);
                if (new_m.HasProperty(kPropertyEmissionColor2))
                    new_m.EnableKeyword("_EMISSION");
                materials[i] = new_m;
                _ownedMaterials.Add(new_m);
            }
            mesh.sharedMaterials = materials;
            CurrentMaterials = GetMaterials() ?? System.Array.Empty<Material>();
        }
        else
        {
            CurrentMaterials = System.Array.Empty<Material>();
        }
    }

    protected virtual void OnDestroy()
    {
        foreach (var material in _ownedMaterials)
        {
            if (material == null) continue;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }
        _ownedMaterials.Clear();
        CurrentMaterials = System.Array.Empty<Material>();
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
                if (m.HasProperty(kPropertyBaseColor)) return m.GetColor(kPropertyBaseColor);
                if (m.HasProperty(kPropertyColor)) return m.GetColor(kPropertyColor);
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
                if (m.HasProperty(kPropertyBaseColor)) m.SetColor(kPropertyBaseColor, value);
                else if (m.HasProperty(kPropertyColor)) m.SetColor(kPropertyColor, value);
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
