using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations;

public static class EffectsManager
{
    // 以下的重点是主界面和战斗界面通用问题
    static readonly ResourcePoolRegistry<DecompositionPool> EffectPools = new ResourcePoolRegistry<DecompositionPool>();
    
    static UniTask<GameObject> TryLoadEffectPrefab(string key)
    {
        if (!AddressablesLogic.CheckKeyExist(EffectResourceKeyUtility.EffectLabel, key))
        {
            return default;
        }
        else
        {
            var returnValue = AddressablesLogic.LoadT<GameObject>(key);
            return returnValue;
        }
    }
    
    public static void Clear()
    {
        EffectPools.Clear(pool => pool.Clear());
    }
    
    public static async UniTask<Decomposition> GenerateEffect(string resourceName, string effectPath, Vector3 pos, Quaternion qua, Transform parentT)
    {
        if (string.IsNullOrEmpty(resourceName))
            return default;
        var effectPool = await IniEffectsPool(resourceName, effectPath, 3);
        if (effectPool == null)
            return default;
        var processingEffectObj = effectPool.Rent();
        var myConstraintSource = new ConstraintSource();
        if (parentT != null)
        {
            myConstraintSource.sourceTransform = parentT;
            myConstraintSource.weight = 1;
            processingEffectObj.GetPositionConstraint().SetSources(new List<ConstraintSource> { myConstraintSource });
            processingEffectObj.GetPositionConstraint().locked = true;
            processingEffectObj.GetPositionConstraint().translationOffset = Vector3.zero;
            processingEffectObj.GetPositionConstraint().constraintActive = true;
        }else{
            myConstraintSource.weight = 0;
            processingEffectObj.GetPositionConstraint().constraintActive = false;
        }
        processingEffectObj.transform.position = pos;
        processingEffectObj.transform.rotation = qua;
        return processingEffectObj;
    }
    
    static async UniTask<DecompositionPool> ConstructEffectPoolWithPrefabAndKey(GameObject prefab, string key, int iniCount)
    {
        if (EffectPools.TryGet(key, out var existingPool))
        {
            var preloadCount = EffectPools.IncrementPreloadCount(key, iniCount);
            await existingPool.PreloadAsync(preloadCount, 1).ToUniTask();
            return existingPool;
        }

        if (prefab != null)
        {
            var poolToConstruct = new DecompositionPool(prefab);
            await poolToConstruct.PreloadAsync(iniCount, 1).ToUniTask();
            if (EffectPools.TryGet(key, out existingPool))
            {
                poolToConstruct.Clear();
                return existingPool;
            }

            EffectPools.TryAdd(key, poolToConstruct, iniCount);
            return poolToConstruct;
        }
        return null;
    }
    
    public static async UniTask<DecompositionPool> IniEffectsPool(string resourceName, string effectPath, int objectCount)
    {
        DecompositionPool effectPool;
        if (effectPath != null)
        {
            var resourceKey = EffectResourceKeyUtility.ResourceKey(effectPath, resourceName);
            if (EffectPools.TryGet(resourceKey, out effectPool))
            {
                return effectPool;
            }
            
            var effectPrefab = await TryLoadEffectPrefab(EffectResourceKeyUtility.PrefabAddress(effectPath, resourceName));
            if (effectPrefab != null)
            {
                effectPool = await ConstructEffectPoolWithPrefabAndKey(effectPrefab, resourceKey, objectCount);
                return effectPool;
            }
            if (effectPath == FightGlobalSetting.EffectPathDefine())
            {
                return null;//防止无限循环
            }
        }
        effectPool = await IniEffectsPool(resourceName, FightGlobalSetting.EffectPathDefine(Element.Null), objectCount);
        return effectPool;
    }
}
