using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine.Rendering;

public class ShaderManager : MonoBehaviour
{
    private const int UnitLayer = 3;
    private const int ShadowLayer = 0;

    [SerializeField] List<DummyMesh> meshes;
    private List<TweenerCore<Color, Color, ColorOptions>> _tweenerCores = new List<TweenerCore<Color, Color, ColorOptions>>();
    
    void Start()
    {
        #region outdated

        foreach (var mesh in meshes)
        {
            mesh.gameObject.layer = UnitLayer;
            mesh.EmissionColor = Color.clear;
            if (CommonSetting.ShadowMaterial == null)
            {
                continue;
            }

            var shadowMesh = Instantiate(mesh, mesh.transform, true);
            var transform1 = shadowMesh.transform;
            transform1.localPosition = Vector3.zero;
            transform1.localScale = Vector3.one * 0.9f;
            var o = shadowMesh.gameObject;
            o.name = "shadow_" + o.name;
            SetLayerRecursively(o, ShadowLayer);

            var shadowRenderer = shadowMesh.Mesh;
            if (shadowRenderer == null)
            {
                Destroy(shadowMesh.gameObject);
                continue;
            }

            shadowRenderer.sharedMaterial = CommonSetting.ShadowMaterial;
            shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shadowRenderer.receiveShadows = false;
            shadowRenderer.lightProbeUsage = LightProbeUsage.Off;
            shadowRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            shadowRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            shadowMesh.enabled = false;
        }
        
        // for (int i = 0; i < pOFXes.Count; i++)
        // {
        //     // Rim
        //     RIMlayer = pOFXes[i].GetLayer(0) as POFX_Rim;
        //     if (RIMlayer == null)
        //     {
        //         pOFXes[i].AddLayer(pOFXes[i].gameObject.AddComponent<POFX_Rim>() as POFXLayer);
        //         RIMlayer = pOFXes[i].GetLayer(0) as POFX_Rim;
        //     }
        //     RIMlayer.m_cParams.intensity = 0f;
        //     POFX_RimBase pOFX_RimBase = pOFXes[i].GetComponent<POFX_RimBase>();
        //     if (pOFX_RimBase)
        //     {
        //         pOFX_RimBase.m_params.rimpower = 0.9f;
        //     }
        //     
        //     // flatColor
        //     POFX_FlatColor flatColorLayer = pOFXes[i].GetLayer(1) as POFX_FlatColor;
        //     if (flatColorLayer == null)
        //     {
        //         pOFXes[i].AddLayer(pOFXes[i].gameObject.AddComponent<POFX_FlatColor>() as POFXLayer);
        //         flatColorLayer = pOFXes[i].GetLayer(1) as POFX_FlatColor;
        //     }
        //     flatColorLayer.m_cParams.intensity = 0f;
        //     POFX_FlatColorBase flatColorLayerBase = pOFXes[i].GetComponent<POFX_FlatColorBase>();
        //     
        //     pOFXes[i].enabled = false;
        // }

        #endregion
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    #region Rim
    public void RimEffectsUp(Color color, float duration)
    {
        ClearDoing();
        meshes.ForEach(x =>
        {
            if (x.CurrentMaterials != null)
                _tweenerCores.Add(DOTween.To(() => x.EmissionColor, c => x.EmissionColor = c, color, duration));
        });
    }

    public void RimEffectsClear(float duration)
    {
        ClearDoing();
        bool cleard = false;
        meshes.ForEach(x =>
        {
            if (x.CurrentMaterials != null)
                _tweenerCores.Add(DOTween.To(() => x.EmissionColor, c => x.EmissionColor = c, Color.clear, duration).
                    OnComplete(() =>
                    {
                        if (!cleard)
                        {
                            ClearDoing();
                            cleard = true;
                        }
                    }));
        });
    }

    public bool HasDoing()
    {
        return _tweenerCores.Count > 0;
    }

    void ClearDoing()
    {
        if (_tweenerCores.Count > 0)
        {
            foreach (var tweener in _tweenerCores)
            {
                tweener?.Kill();
            }
            _tweenerCores.Clear();
        }
    }

    public void RimEffectsForAShortTime(Color targetColor, float duration)
    {
        ClearDoing();
        meshes.ForEach(x =>
        {
            if (x.CurrentMaterials != null)
                _tweenerCores.Add(
                    DOTween.To(() => x.EmissionColor, c => x.EmissionColor = c, targetColor, duration).OnComplete(
                    () =>
                    {
                        _tweenerCores.Add(DOTween.To(() => x.EmissionColor, c => x.EmissionColor = c, Color.clear, duration));
                    })
                );
        });
    }
    #endregion

    #region 纯色
    public void FlatColor(Color targetColor, float duration)
    {
        ClearDoing();
        meshes.ForEach(x =>
        {
            if (x.CurrentMaterials != null)
                _tweenerCores.Add(DOTween.To(() => x.BaseColor, c => x.BaseColor = c, targetColor, duration));
        });
    }
    
    public void FlatColorForAShortTime(Color targetColor, float addTime, float fadeTime)
    {
        ClearDoing();
        meshes.ForEach(x =>
        {
            if (x.CurrentMaterials != null)
                _tweenerCores.Add(
                    DOTween.To(() => x.EmissionColor, c => x.EmissionColor = c, targetColor, addTime).OnComplete(
                    () =>
                    {
                        _tweenerCores.Add(DOTween.To(() => x.EmissionColor, c => x.EmissionColor = c, Color.clear, fadeTime));
                    }
                )
            );
        });
    }
    #endregion
}
