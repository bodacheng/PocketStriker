using System.Collections.Generic;
using UnityEngine;

/// <summary>Keep the pale Ancient Empire floor below the fighters' brightness.</summary>
[DisallowMultipleComponent]
public sealed class AncientEmpireReadability : MonoBehaviour
{
    [SerializeField] Color groundTint = new Color(.64f, .67f, .71f, 1);
    [SerializeField] Color sceneryTint = new Color(.84f, .86f, .89f, 1);
    [SerializeField, Range(0, 1)] float maximumSmoothness = .18f;

    readonly Dictionary<MeshRenderer, Material[]> originals = new Dictionary<MeshRenderer, Material[]>();
    readonly Dictionary<Material, Material> groundMaterials = new Dictionary<Material, Material>();
    readonly Dictionary<Material, Material> sceneryMaterials = new Dictionary<Material, Material>();
    bool applied;

    public bool IsApplied => applied;
    public int RuntimeMaterialCount => groundMaterials.Count + sceneryMaterials.Count;

    void OnEnable() => Apply();
    void OnDisable() => Restore();
    void OnDestroy() => Restore();

    // Explicit entry points also let the stopped-editor render fixture compare
    // the same geometry and lighting without executing combat initialization.
    public void Apply()
    {
        if (applied) return;
        applied = true;
        foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
        {
            bool ground = IsGround(renderer.name);
            var cache = ground ? groundMaterials : sceneryMaterials;
            var authored = renderer.sharedMaterials;
            Material[] adjusted = null;
            for (int index = 0; index < authored.Length; index++)
            {
                var source = authored[index];
                // Water, sky, effects and metal keep their authored appearance.
                // The bright masonry uses URP Lit with no emission or metallic.
                if (!IsMasonry(source)) continue;
                if (!cache.TryGetValue(source, out var material))
                {
                    material = new Material(source) {
                        name = source.name + (ground ? " (Ancient Empire Floor)" : " (Ancient Empire Scenery)"),
                        hideFlags = HideFlags.DontSave
                    };
                    var tint = ground ? groundTint : sceneryTint;
                    Tint(material, "_BaseColor", tint);
                    if (material.HasProperty("_Color")) Tint(material, "_Color", tint);
                    material.SetFloat("_Smoothness", Mathf.Min(source.GetFloat("_Smoothness"), maximumSmoothness));
                    cache.Add(source, material);
                }
                if (adjusted == null) adjusted = (Material[])authored.Clone();
                adjusted[index] = material;
            }
            if (adjusted == null) continue;
            originals.Add(renderer, authored);
            renderer.sharedMaterials = adjusted;
        }
    }

    public void Restore()
    {
        foreach (var pair in originals)
            if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
        originals.Clear();
        Release(groundMaterials);
        Release(sceneryMaterials);
        applied = false;
    }

    static bool IsMasonry(Material material)
    {
        if (material == null || material.shader.name != "Universal Render Pipeline/Lit"
            || !material.HasProperty("_BaseColor") || !material.HasProperty("_Smoothness")) return false;
        if (material.GetFloat("_Surface") > .5f || material.GetFloat("_Metallic") > .1f) return false;
        if (material.IsKeywordEnabled("_EMISSION") && material.GetColor("_EmissionColor").maxColorComponent > .01f)
            return false;
        return true;
    }

    static bool IsGround(string name) => name.StartsWith("SM_Generic_Ground_")
        || name.StartsWith("SM_Env_Ground_") || name.StartsWith("SM_Env_Road_")
        || name.StartsWith("SM_Env_Cobblestone_") || name.StartsWith("SM_Bld_Base_Floor_")
        || name.StartsWith("SM_Bld_Floor_") || name.StartsWith("SM_Bld_Stairs_");

    static void Tint(Material material, string property, Color tint)
    {
        var color = material.GetColor(property);
        material.SetColor(property, new Color(color.r * tint.r, color.g * tint.g, color.b * tint.b, color.a));
    }

    static void Release(Dictionary<Material, Material> materials)
    {
        foreach (var material in materials.Values)
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        materials.Clear();
    }
}
