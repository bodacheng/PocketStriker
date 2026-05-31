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
    private List<Sequence> _sequences = new List<Sequence>();
    private bool _destroyed;
    
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

    void OnDestroy()
    {
        _destroyed = true;
        ClearDoing();
    }

    #region Rim
    public void RimEffectsUp(Color color, float duration)
    {
        ClearDoing();
        if (meshes == null)
        {
            return;
        }

        meshes.ForEach(x =>
        {
            if (CanTween(x))
            {
                TweenerCore<Color, Color, ColorOptions> tweener = null;
                tweener = TweenEmission(x, color, duration).OnComplete(() =>
                {
                    if (tweener != null)
                    {
                        _tweenerCores.Remove(tweener);
                    }
                });
                _tweenerCores.Add(tweener);
            }
        });
    }

    public void RimEffectsClear(float duration)
    {
        ClearDoing();
        if (meshes == null)
        {
            return;
        }

        meshes.ForEach(x =>
        {
            if (CanTween(x))
            {
                TweenerCore<Color, Color, ColorOptions> tweener = null;
                tweener = TweenEmission(x, Color.clear, duration).OnComplete(() =>
                {
                    if (tweener != null)
                    {
                        _tweenerCores.Remove(tweener);
                    }
                });
                _tweenerCores.Add(tweener);
            }
        });
    }

    public bool HasDoing()
    {
        return _tweenerCores.Count > 0 || _sequences.Count > 0;
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

        if (_sequences.Count > 0)
        {
            foreach (var sequence in _sequences)
            {
                sequence?.Kill();
            }
            _sequences.Clear();
        }
    }

    public void RimEffectsForAShortTime(Color targetColor, float duration)
    {
        ClearDoing();
        if (meshes == null)
        {
            return;
        }

        foreach (var mesh in meshes)
        {
            if (CanTween(mesh))
            {
                var sequence = DOTween.Sequence().SetLink(gameObject);
                sequence.Append(TweenEmission(mesh, targetColor, duration));
                sequence.Append(TweenEmission(mesh, Color.clear, duration));
                sequence.OnComplete(() =>
                {
                    _sequences.Remove(sequence);
                });
                _sequences.Add(sequence);
            }
        }
    }
    #endregion

    #region 纯色
    public void FlatColor(Color targetColor, float duration)
    {
        ClearDoing();
        if (meshes == null)
        {
            return;
        }

        meshes.ForEach(x =>
        {
            if (CanTween(x))
            {
                TweenerCore<Color, Color, ColorOptions> tweener = null;
                tweener = TweenBaseColor(x, targetColor, duration).OnComplete(() =>
                {
                    if (tweener != null)
                    {
                        _tweenerCores.Remove(tweener);
                    }
                });
                _tweenerCores.Add(tweener);
            }
        });
    }
    
    public void FlatColorForAShortTime(Color targetColor, float addTime, float fadeTime)
    {
        ClearDoing();
        if (meshes == null)
        {
            return;
        }

        foreach (var mesh in meshes)
        {
            if (CanTween(mesh))
            {
                var sequence = DOTween.Sequence().SetLink(gameObject);
                sequence.Append(TweenEmission(mesh, targetColor, addTime));
                sequence.Append(TweenEmission(mesh, Color.clear, fadeTime));
                sequence.OnComplete(() =>
                {
                    _sequences.Remove(sequence);
                });
                _sequences.Add(sequence);
            }
        }
    }
    #endregion

    bool CanTween(DummyMesh mesh)
    {
        return !_destroyed && mesh != null && mesh.CurrentMaterials != null;
    }

    TweenerCore<Color, Color, ColorOptions> TweenEmission(DummyMesh mesh, Color color, float duration)
    {
        return DOTween.To(
            () => CanTween(mesh) ? mesh.EmissionColor : Color.clear,
            c =>
            {
                if (CanTween(mesh))
                {
                    mesh.EmissionColor = c;
                }
            },
            color,
            duration
        ).SetLink(gameObject);
    }

    TweenerCore<Color, Color, ColorOptions> TweenBaseColor(DummyMesh mesh, Color color, float duration)
    {
        return DOTween.To(
            () => CanTween(mesh) ? mesh.BaseColor : Color.clear,
            c =>
            {
                if (CanTween(mesh))
                {
                    mesh.BaseColor = c;
                }
            },
            color,
            duration
        ).SetLink(gameObject);
    }
}
